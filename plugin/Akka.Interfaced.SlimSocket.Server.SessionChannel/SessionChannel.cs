using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Akka.Actor;
using Akka.Event;
using Akka.Interfaced.SlimServer;
using Common.Logging;

namespace Akka.Interfaced.SlimSocket.Server.SessionChannel
{
    public class SessionChannel : ActorBoundChannelBase
    {
        private SessionGatewayInitiator _initiator;
        private ILog _logger;
        private int _sessionId;
        private ISessionLine _line;
        private IActorRef _self;
        private EventStream _eventStream;

        private readonly List<SessionReliablePacket> _sendPackets = new List<SessionReliablePacket>();
        private int _lastSendMessageId;
        private int _lastSendMessageIdOfCurrentLine;
        private int _lastRecvMessageId;

        private SessionCloseState _closeState;
        private int? _finMessageId;
        private int? _finAckMessageId;
        private bool _isClientFinAckReceived;
        private ICancelable _timeWaitTimeoutCanceler;
        private ICancelable _offlineTimeoutCanceler;

        private ICancelable _keepAliveCanceler;
        private DateTime? _aliveCheckTime;
        private DateTime? _aliveCheckWaitTime;

        private class ReliablePacketMessage
        {
            public ISessionLine SessionLine { get; }
            public SessionReliablePacket ReliablePacket { get; }

            public ReliablePacketMessage(ISessionLine sessionLine, SessionReliablePacket reliablePacket)
            {
                SessionLine = sessionLine;
                ReliablePacket = reliablePacket;
            }
        }

        private class SessionLineClosedMessage
        {
            public ISessionLine SessionLine { get; }

            public SessionLineClosedMessage(ISessionLine sessionLine)
            {
                SessionLine = sessionLine;
            }
        }

        private class OfflineTimedoutMessage
        {
        }

        private class TimeWaitStateTimedoutMessage
        {
        }

        private class KeepAliveCheckMessage
        {
        }

        // For test case
        internal class ChannelCloseRequestMessage
        {
        }

        public int SessionId
        {
            get { return _sessionId; }
        }

        public SessionChannel(SessionGatewayInitiator initiator, int sessionId, EndPoint remoteEndPoint, object tag, Tuple<IActorRef, TaggedType[], ActorBindingFlags> bindingActor)
        {
            _initiator = initiator;
            _sessionId = sessionId;
            _tag = tag;
            _logger = _initiator.CreateChannelLogger(remoteEndPoint, this);

            if (bindingActor != null)
            {
                BindActor(bindingActor.Item1, bindingActor.Item2.Select(t => new BoundType(t)), bindingActor.Item3);
            }
        }

        protected override void PreStart()
        {
            base.PreStart();

            _self = Self;
            _eventStream = Context.System.EventStream;

            // create initial actors and bind them

            if (_initiator.CreateInitialActors != null)
            {
                var actors = _initiator.CreateInitialActors(Context, null);
                if (actors != null)
                {
                    foreach (var actor in actors)
                    {
                        BindActor(actor.Item1, actor.Item2.Select(t => new BoundType(t)));
                    }
                }
            }

            if (_initiator.SessionSettings.AliveCheckInterval != TimeSpan.Zero)
            {
                _keepAliveCanceler = Context.System.Scheduler.ScheduleTellRepeatedlyCancelable(
                    1000,
                    1000,
                    Self,
                    new KeepAliveCheckMessage(),
                    Self);
            }
        }

        protected override void PostStop()
        {
            if (_line != null)
            {
                _line.Received -= OnSessionPacketReceived;
                _line.Closed -= OnSessionLineClosed;
                _line.Close(_closeState == SessionCloseState.Closed);
                _line = null;
            }

            if (_offlineTimeoutCanceler != null)
            {
                _offlineTimeoutCanceler.Cancel();
                _offlineTimeoutCanceler = null;
            }

            if (_timeWaitTimeoutCanceler != null)
            {
                _timeWaitTimeoutCanceler.Cancel();
                _timeWaitTimeoutCanceler = null;
            }

            if (_keepAliveCanceler != null)
            {
                _keepAliveCanceler.Cancel();
                _keepAliveCanceler = null;
            }

            base.PostStop();
        }

        protected override void OnNotificationMessage(NotificationMessage message)
        {
            SendPacket(new Packet
            {
                Type = PacketType.Notification,
                ActorId = message.ObserverId,
                RequestId = message.NotificationId,
                Message = message.InvokePayload,
            });
        }

        protected override void OnResponseMessage(ResponseMessage message)
        {
            var actorId = GetBoundActorId(Sender);
            if (actorId != 0)
            {
                SendPacket(new Packet
                {
                    Type = PacketType.Response,
                    ActorId = actorId,
                    RequestId = message.RequestId,
                    Message = message.ReturnPayload,
                    Exception = message.Exception
                });
            }
            else
            {
                _logger.WarnFormat("Not bound actorId owned by ReponseMessage. (ActorId={0})", actorId);
            }
        }

        protected override void OnCloseRequest()
        {
            // Try to close gracefully
            if (_closeState != SessionCloseState.None)
            {
                return;
            }

            SendFinPacket();
        }

        private void InvokeClose()
        {
            // Close session forcibly
            RunTask(() => Close(), _self);
        }

        private void SetCurrentLine(ISessionLine newLine)
        {
            var oldLine = _line;
            if (oldLine != null)
            {
                oldLine.Close();
                oldLine.Received -= OnSessionPacketReceived;
                oldLine.Closed -= OnSessionLineClosed;
            }

            _line = newLine;
            _lastSendMessageIdOfCurrentLine = _sendPackets.Count > 0 ? _sendPackets[0].MessageId - 1 : _lastSendMessageId;
            if (_line != null)
            {
                _logger?.Trace("Received event handler bound");
                _line.Received += OnSessionPacketReceived;
                _line.Closed += OnSessionLineClosed;
            }

            if (oldLine != null && _line == null)
            {
                if (_initiator.SessionSettings.OfflineTimeout != TimeSpan.Zero)
                {
                    _logger?.Trace($"Offline timer set to={_initiator.SessionSettings.OfflineTimeout}");
                    _offlineTimeoutCanceler = Context.System.Scheduler.ScheduleTellOnceCancelable(
                        _initiator.SessionSettings.OfflineTimeout,
                        Self,
                        new OfflineTimedoutMessage(),
                        Self);
                }

                _aliveCheckTime = null;
                _aliveCheckWaitTime = null;
            }
            else if (oldLine == null && _line != null)
            {
                if (_offlineTimeoutCanceler != null)
                {
                    _offlineTimeoutCanceler.Cancel();
                    _offlineTimeoutCanceler = null;
                }

                _aliveCheckTime = DateTime.UtcNow;
                _aliveCheckWaitTime = null;
            }
        }

        private void SendPacket(Packet packet)
        {
            if (_finMessageId.HasValue)
            {
                return;
            }

            SendReliablePacket(new ScSessionInnerPacket()
            {
                InnerPacket = packet,
            });
        }

        private void SendReliablePacket(SessionReliablePacket packet)
        {
            _lastSendMessageId++;
            packet.MessageId = _lastSendMessageId;
            _sendPackets.Add(packet);
            TrySendToLine();
        }

        private void TrySendToLine()
        {
            if (_line == null)
            {
                return;
            }

            int sendCount = _lastSendMessageId - _lastSendMessageIdOfCurrentLine;
            if (sendCount < 0)
            {
                _logger?.Error($"sendCount < 0. sc={sendCount} id={_lastSendMessageId} idOfLine={_lastSendMessageIdOfCurrentLine}");
                Close();
                return;
            }

            if (sendCount > _sendPackets.Count)
            {
                _logger?.Error($"sendCount > _sendPackets.Count sc={sendCount} id={_lastSendMessageId} idOfLine={_lastSendMessageIdOfCurrentLine} spc={_sendPackets.Count}");
                Close();
                return;
            }

            if (sendCount > 0)
            {
                for (int i = _sendPackets.Count - sendCount; i < _sendPackets.Count; ++i)
                {
                    // Update ack up to date
                    var p = _sendPackets[i];
                    p.MessageAck = _lastRecvMessageId;
                    _line.Send(p);
                }
                _lastSendMessageIdOfCurrentLine = _lastSendMessageId;
            }
        }

        [MessageHandler]
        private void Handle(SessionGateway.SessionCreateMessage m)
        {
            SetCurrentLine(m.SessionLine);
            _line.Send(new SrSessionCreate()
            {
                SessionId = _sessionId
            });
        }

        [MessageHandler]
        private void Handle(SessionGateway.SessionRebindMessage m)
        {
            if (TryRebind(m))
            {
                _line.Send(new SrSessionRebind()
                {
                    ServerMessageAck = _lastRecvMessageId
                });

                TrySendToLine();
            }
            else
            {
                m.SessionLine.Close();
            }
        }

        private bool TryRebind(SessionGateway.SessionRebindMessage m)
        {
            if (_line != null && _line.LineIndex > m.SessionLine.LineIndex)
            {
                _logger?.Trace($"Lower lineIndex for session={_sessionId} {_line.LineIndex}<{m.SessionLine.LineIndex}");
                return false;
            }

            // Check ack
            var clientAck = m.ClientMessageAck;
            var firstSendMessageId = _sendPackets.Count > 0 ? _sendPackets[0].MessageId : _lastSendMessageId;
            if (clientAck - firstSendMessageId > 1)
            {
                _logger?.Error($"fail to resend to client f={firstSendMessageId} ack={clientAck}");
                Close();
                return false;
            }

            RemoveToAck(clientAck);
            SetCurrentLine(m.SessionLine);
            _logger?.Trace($"line rebound {_line.LineIndex}");
            return true;
        }

        // BEWARE: Called by Network Thread
        private void OnSessionPacketReceived(ISessionLine line, SessionPacket packet)
        {
            if (packet is SessionReliablePacket)
            {
                RunTask(() => HandleReliablePacket(line, (SessionReliablePacket)packet), _self);
            }
            else if (packet is ScSessionAck)
            {
                RunTask(() => HandleScSessionAckPacket(line, (ScSessionAck)packet), _self);
            }
            else if (packet is SqEcho)
            {
                _line.Send(new SrEcho()
                {
                    Ticks = ((SqEcho)packet).Ticks
                });
            }
            else if (packet is SrEcho)
            {
                RunTask(() =>
                {
                    var p = (SrEcho)packet;
                    if (_aliveCheckWaitTime.HasValue)
                    {
                        var waitTicks = (int)(_aliveCheckWaitTime.Value.Ticks >> 32);
                        if (waitTicks == p.Ticks)
                        {
                            _aliveCheckTime = DateTime.UtcNow;
                            _aliveCheckWaitTime = null;
                        }
                        else
                        {
                            _logger?.Trace($"waitTicks not matched wt={waitTicks} t={p.Ticks}");
                        }
                    }
                }, _self);
            }
            else
            {
                _logger?.Warn($"SessionChannel receives non reliable packet");
            }
        }

        private void HandleReliablePacket(ISessionLine sessionLine, SessionReliablePacket reliablePacket)
        {
            if (sessionLine != _line)
            {
                return;
            }

            var p = reliablePacket;

            // Check if received already
            if (_lastRecvMessageId - p.MessageId >= 0)
            {
                _logger?.Trace($"duplicated received packet");
                return;
            }

            // Check if mismatched message index.
            // For reliable line, message index should be _lastRecvMessageId + 1
            if (p.MessageId != _lastRecvMessageId + 1)
            {
                _logger?.Warn($"Session recv message index mismatch. {p.MessageId} != {_lastRecvMessageId + 1}");
                _line.Close();
                return;
            }

            _lastRecvMessageId++;
            RemoveToAck(reliablePacket.MessageAck);

            if (p is ScSessionInnerPacket)
            {
                HandleDataPacket(sessionLine, ((ScSessionInnerPacket)p).InnerPacket);
            }
            else if (p is ScSessionFin)
            {
                HandleFinPacket();
            }
            else if (p is ScSessionFinAck)
            {
                HandleFinAckPacket();
            }
        }

        private void HandleScSessionAckPacket(ISessionLine sessionLine, ScSessionAck packet)
        {
            RemoveToAck(packet.MessageAck);
        }

        private void RemoveToAck(int ack)
        {
            int removeCount = 0;
            foreach (var p in _sendPackets)
            {
                if (ack - p.MessageId >= 0)
                {
                    removeCount++;
                }
                else
                {
                    break;
                }
            }

            if (removeCount > 0)
            {
                _sendPackets.RemoveRange(0, removeCount);

                // Check _finAckMessageId
                if (_finAckMessageId.HasValue && ack - _finAckMessageId.Value >= 0)
                {
                    _isClientFinAckReceived = true;
                    if (_closeState == SessionCloseState.TimeWait)
                    {
                        SetCloseState(SessionCloseState.Closed);
                    }
                }
            }
        }

        private void HandleDataPacket(ISessionLine line, Packet packet)
        {
            var p = packet as Packet;
            if (p == null)
            {
                _eventStream.Publish(new Warning(
                    _self.Path.ToString(), GetType(),
                    $"Receives null packet from {line?.RemoteEndPoint}"));
                return;
            }

            var msg = p.Message as IInterfacedPayload;
            if (msg == null)
            {
                _eventStream.Publish(new Warning(
                    _self.Path.ToString(), GetType(),
                    $"Receives a bad packet without a message from {line?.RemoteEndPoint}"));
                return;
            }

            var actor = GetBoundActor(p.ActorId);
            if (actor == null)
            {
                if (p.RequestId != 0)
                {
                    line.Send(new Packet
                    {
                        Type = PacketType.Response,
                        ActorId = p.ActorId,
                        RequestId = p.RequestId,
                        Message = null,
                        Exception = new RequestTargetException()
                    });
                }
                return;
            }

            var boundType = actor.FindBoundType(msg.GetInterfaceType());
            if (boundType == null)
            {
                if (p.RequestId != 0)
                {
                    line.Send(new Packet
                    {
                        Type = PacketType.Response,
                        ActorId = p.ActorId,
                        RequestId = p.RequestId,
                        Message = null,
                        Exception = new RequestHandlerNotFoundException()
                    });
                }
                return;
            }

            if (boundType.IsTagOverridable)
            {
                var tagOverridable = (IPayloadTagOverridable)p.Message;
                tagOverridable.SetTag(boundType.TagValue);
            }

            var observerUpdatable = p.Message as IPayloadObserverUpdatable;
            if (observerUpdatable != null)
            {
                observerUpdatable.Update(o =>
                {
                    var observer = (InterfacedObserver)o;
                    if (observer != null)
                    {
                        observer.Channel = new AkkaReceiverNotificationChannel(_self);
                    }
                });
            }

            actor.Actor.Tell(new RequestMessage
            {
                RequestId = p.RequestId,
                InvokePayload = (IInterfacedPayload)p.Message
            }, _self);
        }

        // BEWARE: Called by Network Thread
        private void OnSessionLineClosed(ISessionLine line)
        {
            line.Closed -= OnSessionLineClosed;
            _self.Tell(new SessionLineClosedMessage(line));
        }

        [MessageHandler]
        private void Handle(SessionLineClosedMessage m)
        {
            if (m.SessionLine == _line)
            {
                SetCurrentLine(null);
            }
        }

        [MessageHandler]
        private void Handle(OfflineTimedoutMessage m)
        {
            _logger?.Trace($"Offline timed out");
            Close();
        }

        [MessageHandler]
        private void Handle(TimeWaitStateTimedoutMessage m)
        {
            if (_closeState == SessionCloseState.TimeWait)
            {
                SetCloseState(SessionCloseState.Closed);
            }
        }

        [MessageHandler]
        private void Handle(KeepAliveCheckMessage m)
        {
            if (_aliveCheckTime.HasValue)
            {
                var now = DateTime.UtcNow;
                if (now - _aliveCheckTime.Value > _initiator.SessionSettings.AliveCheckInterval)
                {
                    _aliveCheckTime = null;
                    _aliveCheckWaitTime = now;

                    _line.Send(new SqEcho()
                    {
                        Ticks = (int)(now.Ticks >> 32)
                    });
                }
            }
            else if (_aliveCheckWaitTime.HasValue)
            {
                var now = DateTime.UtcNow;
                if (now - _aliveCheckWaitTime.Value > _initiator.SessionSettings.AliveCheckWaitInterval)
                {
                    SetCurrentLine(null);
                }
            }
        }

        private void SetCloseState(SessionCloseState newState)
        {
            _closeState = newState;

            if (newState == SessionCloseState.Closed)
            {
                InvokeClose();
            }
            else if (newState == SessionCloseState.TimeWait)
            {
                _timeWaitTimeoutCanceler = Context.System.Scheduler.ScheduleTellOnceCancelable(
                    _initiator.SessionSettings.TimeWaitTimeout,
                    Self,
                    new TimeWaitStateTimedoutMessage(),
                    Self);
            }
        }

        private void HandleFinPacket()
        {
            SendFinAckPacket();

            if (_closeState == SessionCloseState.None)
            {
                SetCloseState(SessionCloseState.CloseWait);
                SendFinPacket();
            }
            else if (_closeState == SessionCloseState.FinWait1)
            {
                SetCloseState(SessionCloseState.Closing);
            }
            else if (_closeState == SessionCloseState.FinWait2)
            {
                SetCloseState(SessionCloseState.TimeWait);
            }
            else
            {
                _logger?.Warn($"Not expected closestate on Fin. state={_closeState}");
                Close();
            }
        }

        private void HandleFinAckPacket()
        {
            if (_closeState == SessionCloseState.FinWait1)
            {
                SetCloseState(SessionCloseState.FinWait2);
            }
            else if (_closeState == SessionCloseState.Closing)
            {
                if (_isClientFinAckReceived == false)
                {
                    SetCloseState(SessionCloseState.TimeWait);
                }
                else
                {
                    SetCloseState(SessionCloseState.Closed);
                }
            }
            else if (_closeState == SessionCloseState.LastAck)
            {
                _line?.Send(new ScSessionAck() { MessageAck = _lastRecvMessageId });
                SetCloseState(SessionCloseState.Closed);
            }
            else
            {
                _logger?.Warn($"FinAck received in wrong state[{_closeState}]");
                Close();
            }
        }

        private void SendFinPacket()
        {
            if (_closeState != SessionCloseState.None &&
                _closeState != SessionCloseState.CloseWait)
            {
                _logger?.Warn($"Fin requested again cs={_closeState}");
                Close();
                return;
            }

            var finPacket = new ScSessionFin();
            SendReliablePacket(finPacket);
            _finMessageId = finPacket.MessageId;

            if (_closeState == SessionCloseState.None)
            {
                SetCloseState(SessionCloseState.FinWait1);
            }
            else if (_closeState == SessionCloseState.CloseWait)
            {
                SetCloseState(SessionCloseState.LastAck);
            }
        }

        private void SendFinAckPacket()
        {
            if (_finAckMessageId.HasValue)
            {
                return;
            }

            var finAckPacket = new ScSessionFinAck();
            SendReliablePacket(finAckPacket);
            _finAckMessageId = finAckPacket.MessageId;
        }

        [MessageHandler]
        private void Handle(ChannelCloseRequestMessage m)
        {
            _line?.Close();
        }
    }
}
