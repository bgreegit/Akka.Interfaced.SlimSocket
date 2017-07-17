using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using Akka.Actor;
using Akka.Interfaced.SlimServer;
using Common.Logging;

namespace Akka.Interfaced.SlimSocket.Server
{
    public class SessionGateway : InterfacedActor, IGatewaySync, IActorBoundGatewaySync
    {
        private readonly GatewayInitiator _initiator;
        private readonly ILog _logger;
        private IActorRef _self;
        private readonly HashSet<ISessionLine> _handshakingSessionLines = new HashSet<ISessionLine>();
        private readonly Dictionary<int, IActorRef> _sessionChannels = new Dictionary<int, IActorRef>();
        private readonly Dictionary<IActorRef, int> _invSessionChannels = new Dictionary<IActorRef, int>();
        private int _lastSessionId;
        private TcpAcceptor _tcpAcceptor;
        private bool _isStopped;

        internal class WaitingItem
        {
            public object Tag;
            public Tuple<IActorRef, TaggedType[], ActorBindingFlags> BindingActor;
            public DateTime Time;
        }
        private readonly Dictionary<string, WaitingItem> _waitingMap = new Dictionary<string, WaitingItem>();
        private ICancelable _timeoutCanceler;

        public GatewayInitiator Initiator
        {
            get { return _initiator; }
        }

        private class TcpAcceptMessage
        {
            public Socket Socket { get; }

            public TcpAcceptMessage(Socket socket)
            {
                Socket = socket;
            }
        }

        internal class SessionCreateMessage
        {
            public ISessionLine SessionLine { get; }
            public string Token { get; }

            public SessionCreateMessage(ISessionLine sessionLine, string token)
            {
                SessionLine = sessionLine;
                Token = token;
            }
        }

        internal class SessionRebindMessage
        {
            public ISessionLine SessionLine { get; }
            public int SessionId { get; }
            public int ClientMessageAck { get; }

            public SessionRebindMessage(ISessionLine sessionLine, int sessionId, int clientMessageAck)
            {
                SessionLine = sessionLine;
                SessionId = sessionId;
                ClientMessageAck = clientMessageAck;
            }
        }

        private class TimeoutTimerMessage
        {
        }

        public SessionGateway(GatewayInitiator initiator)
        {
            _initiator = initiator;
            _logger = initiator.GatewayLogger;

            if (initiator.TokenRequired && initiator.TokenTimeout != TimeSpan.Zero)
            {
                _timeoutCanceler = Context.System.Scheduler.ScheduleTellRepeatedlyCancelable(
                    initiator.TokenTimeout, initiator.TokenTimeout, Self, new TimeoutTimerMessage(), Self);
            }
        }

        protected override void PreStart()
        {
            base.PreStart();

            _self = Self;
            _initiator.GatewayInitialized?.Invoke(_self);
        }

        protected override void PostStop()
        {
            base.PostStop();

            if (_timeoutCanceler != null)
            {
                _timeoutCanceler.Cancel();
            }
        }

        void IGatewaySync.Start()
        {
            _logger?.InfoFormat("Start (EndPoint={0})", _initiator.ListenEndPoint);

            try
            {
                // Prepare tcp acceptor
                _tcpAcceptor = new TcpAcceptor();
                _tcpAcceptor.Accepted += OnConnectionAccept;
                _tcpAcceptor.Listen(_initiator.ListenEndPoint);
            }
            catch (Exception e)
            {
                _logger?.ErrorFormat("Start got exception.", e);
            }
        }

        void IGatewaySync.Stop(bool stopListenOnly)
        {
            _logger?.Info($"Stop (StopListenOnly={stopListenOnly})");

            // stop listening

            _isStopped = true;

            if (_tcpAcceptor != null)
            {
                _tcpAcceptor.Close();
                _tcpAcceptor = null;
            }

            if (stopListenOnly)
            {
                return;
            }

            // stop all running channels

            if (_sessionChannels.Count > 0)
            {
                foreach (var channel in _sessionChannels.Values)
                {
                    channel.Cast<ActorBoundChannelRef>().WithNoReply().Close();
                }
            }
            else
            {
                Self.Tell(InterfacedPoisonPill.Instance);
            }
        }

        // BEWARE: Called by Network Thread
        private TcpAcceptor.AcceptResult OnConnectionAccept(TcpAcceptor sender, Socket socket)
        {
            _self.Tell(new TcpAcceptMessage(socket), _self);
            return TcpAcceptor.AcceptResult.Accept;
        }

        [MessageHandler]
        private void Handle(TcpAcceptMessage m)
        {
            if (_isStopped)
            {
                return;
            }

            if (_initiator.CheckCreateChannel != null)
            {
                if (_initiator.CheckCreateChannel(m.Socket.RemoteEndPoint, m.Socket) == false)
                {
                    m.Socket.Close();
                    return;
                }
            }

            var sessionLine = new TcpSessionLine(_initiator, m.Socket);
            sessionLine.Closed += OnSessionLineClosed;
            sessionLine.Received += OnFirstSessionPacketReceived;
            sessionLine.Open();
        }

        // BEWARE: Called by Network Thread
        private void OnSessionLineClosed(ISessionLine sessionLine)
        {
            sessionLine.Received -= OnFirstSessionPacketReceived;
        }

        // BEWARE: Called by Network Thread
        private void OnFirstSessionPacketReceived(ISessionLine sessionLine, SessionPacket sp)
        {
            _handshakingSessionLines.Remove(sessionLine);
            sessionLine.Received -= OnFirstSessionPacketReceived;

            if (_isStopped)
            {
                return;
            }

            if (sp is SqSessionCreate)
            {
                var p = (SqSessionCreate)sp;
                _self.Tell(new SessionCreateMessage(sessionLine, p.Token));
            }
            else if (sp is SqSessionRebind)
            {
                var p = (SqSessionRebind)sp;
                sessionLine.SessionId = p.SessionId;
                sessionLine.LineIndex = p.LineIndex;
                _self.Tell(new SessionRebindMessage(sessionLine, sessionLine.SessionId, p.ClientMessageAck));
            }
            else
            {
                _logger?.Warn($"SessionLine's first packet is non-expected packet t={sp.GetType().FullName}");
                sessionLine.Close();
            }
        }

        [MessageHandler]
        private void Handle(SessionCreateMessage m)
        {
            if (TryCreateSession(m) == false)
            {
                m.SessionLine.Close();
            }
        }

        private bool TryCreateSession(SessionCreateMessage m)
        {
            var sessionLine = m.SessionLine;
            if (sessionLine.SessionId != 0)
            {
                return false;
            }

            WaitingItem waitingItem = null;
            if (_initiator.TokenRequired)
            {
                waitingItem = EstablishChannel(m.Token);
                if (waitingItem == null)
                {
                    _logger?.TraceFormat($"Receives wrong token({m.Token}) from {sessionLine.RemoteEndPoint})", sessionLine.RemoteEndPoint);
                    return false;
                }
            }

            var sessionId = IssueSessionId();
            var tag = waitingItem?.Tag;
            var bindingActor = waitingItem?.BindingActor;
            var channel = Context.ActorOf(Props.Create(() => new SessionChannel(_initiator, sessionId, sessionLine.RemoteEndPoint, tag, bindingActor)));
            if (channel == null)
            {
                _logger?.TraceFormat("Deny a connection. (EndPoint={0})", sessionLine.RemoteEndPoint);
                return false;
            }
            _logger?.TraceFormat("Accept a connection. (EndPoint={0})", sessionLine.RemoteEndPoint);

            sessionLine.SessionId = sessionId;
            _sessionChannels.Add(sessionId, channel);
            _invSessionChannels.Add(channel, sessionId);
            Context.Watch(channel);

            // Forward to newly created Channel
            channel.Tell(m, _self);
            return true;
        }

        private int IssueSessionId()
        {
            while (true)
            {
                ++_lastSessionId;
                int id = _lastSessionId;
                if (_sessionChannels.ContainsKey(id) == false)
                {
                    return id;
                }
            }
        }

        private WaitingItem EstablishChannel(string token)
        {
            WaitingItem item;
            if (_waitingMap.TryGetValue(token, out item) == false)
            {
                return null;
            }
            _waitingMap.Remove(token);
            return item;
        }

        [MessageHandler]
        private void Handle(SessionRebindMessage m)
        {
            IActorRef actorRef;
            if (_sessionChannels.TryGetValue(m.SessionId, out actorRef))
            {
                _logger?.Trace($"SessionRebindMessage forwarded");
                actorRef.Forward(m);
            }
            else
            {
                _logger?.Trace($"SessionRebindMessage cannot find session");
                m.SessionLine.Close();
            }
        }

        [ResponsiveExceptionAll]
        InterfacedActorRef IActorBoundGatewaySync.OpenChannel(InterfacedActorRef actor, object tag, ActorBindingFlags bindingFlags)
        {
            var targetActor = actor.CastToIActorRef();
            if (targetActor == null)
            {
                throw new ArgumentNullException(nameof(actor));
            }

            var target = ((IActorBoundGatewaySync)this).OpenChannel(targetActor, new TaggedType[] { actor.InterfaceType }, bindingFlags);

            var actorRef = (InterfacedActorRef)Activator.CreateInstance(actor.GetType());
            InterfacedActorRefModifier.SetTarget(actorRef, target);
            return actorRef;
        }

        [ResponsiveExceptionAll]
        IRequestTarget IActorBoundGatewaySync.OpenChannel(IActorRef actor, TaggedType[] types, object tag, ActorBindingFlags bindingFlags)
        {
            if (actor == null)
            {
                throw new ArgumentNullException(nameof(actor));
            }

            if (_isStopped)
            {
                return null;
            }

            // create token and add to waiting list

            string token;
            while (true)
            {
                token = Guid.NewGuid().ToString();
                if (_waitingMap.ContainsKey(token) == false)
                {
                    _waitingMap.Add(token, new WaitingItem
                    {
                        Tag = tag,
                        BindingActor = Tuple.Create(actor, types, bindingFlags),
                        Time = DateTime.UtcNow
                    });
                    break;
                }
            }

            var address = string.Join("|", ChannelType.Session.ToString(),
                                           _initiator.ConnectEndPoint.ToString(),
                                           token);
            return new BoundActorTarget(1, address);
        }

        [MessageHandler]
        private void Handle(TimeoutTimerMessage m)
        {
            var now = DateTime.UtcNow;
            var timeoutItems = _waitingMap.Where(i => (now - i.Value.Time) > _initiator.TokenTimeout).ToList();
            foreach (var i in timeoutItems)
            {
                _waitingMap.Remove(i.Key);
                if (i.Value.BindingActor.Item3.HasFlag(ActorBindingFlags.OpenThenNotification))
                {
                    i.Value.BindingActor.Item1.Tell(new NotificationMessage
                    {
                        InvokePayload = new IActorBoundChannelObserver_PayloadTable.ChannelOpenTimeout_Invoke
                        {
                            tag = i.Value.Tag
                        },
                    });
                }
            }
        }

        [MessageHandler]
        private void Handle(Terminated m)
        {
            int sessionId = 0;
            if (_invSessionChannels.TryGetValue(m.ActorRef, out sessionId))
            {
                _invSessionChannels.Remove(m.ActorRef);
                _sessionChannels.Remove(sessionId);

                if (_isStopped && _sessionChannels.Count == 0)
                {
                    Self.Tell(InterfacedPoisonPill.Instance);
                }
            }
        }
    }
}
