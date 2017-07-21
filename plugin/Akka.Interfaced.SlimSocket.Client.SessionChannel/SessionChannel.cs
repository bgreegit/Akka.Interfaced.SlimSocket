using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Common.Logging;

namespace Akka.Interfaced.SlimSocket.Client.SessionChannel
{
    public class SessionChannel : ChannelBase
    {
        private IPEndPoint _remoteEndPoint;
        private IPacketSerializer _packetSerializer;
        private SessionPacketSerializer _sessionPacketSerializer;
        private SessionSettings _settings;
        private string _token;
        private ISlimTaskCompletionSource<bool> _connectTcs;

        private object _lock = new object();

        private int _sessionId;
        private int _lastLineIndex;
        private IReliableSessionLine _bindingLine;
        private IReliableSessionLine _line;

        private readonly List<SessionReliablePacket> _sendPackets = new List<SessionReliablePacket>();
        private int _lastSendMessageId;
        private int _lastSendMessageIdOfCurrentLine;
        private int _lastRecvMessageId;

        private SessionCloseState _closeState;
        private int? _finMessageId;
        private int? _finAckMessageId;
        private bool _isServerFinAckReceived;

        private TimeSpan? _offineElapsed;
        private TimeSpan? _rebindElapsed;
        private TimeSpan? _rebindCoolTimeElapsed;
        private TimeSpan? _timeWaitElapsed;

        private TimeSpan? _aliveCheckElapsed;
        private TimeSpan? _aliveCheckWaitElapsed;
        private int _aliveCheckTicks;
        private long? _smoothedRoundTripTicks;

        // For internal test
        internal event Action<IChannel, SessionCloseState> CloseStateChanged;

        public TimeSpan? SmoothedRoundTripTicks
        {
            get { return _smoothedRoundTripTicks.HasValue ? TimeSpan.FromTicks(_smoothedRoundTripTicks.Value) : (TimeSpan?)null; }
        }

        public SessionChannel(ILog logger, IPEndPoint remoteEndPoint, string token, IPacketSerializer packetSerializer, SessionSettings settings)
            : base(logger)
        {
            if (settings == null)
            {
                throw new ArgumentException("settings is null", nameof(settings));
            }

            _remoteEndPoint = remoteEndPoint;
            _token = token;
            _packetSerializer = packetSerializer;
            _settings = settings;
            _sessionPacketSerializer = new SessionPacketSerializer(_packetSerializer);
        }

        public override Task<bool> ConnectAsync()
        {
            if (State != ChannelStateType.Closed)
            {
                throw new InvalidOperationException("Should be closed to connect.");
            }

            lock (_lock)
            {
                var tcs = _connectTcs = TaskFactory.Create<bool>();
                _logger?.Info("Connect.");

                SetState(ChannelStateType.Connecting);

                _bindingLine = new TcpSessionLine(_logger, _remoteEndPoint, _sessionPacketSerializer, _lastLineIndex);
                _bindingLine.Created += OnLineCreated;
                _bindingLine.Closed += OnCreatingLineClose;
                _bindingLine.Create(_token);

                return tcs.Task;
            }
        }

        public override void Close()
        {
            lock (_lock)
            {
                var state = _state;

                if (state == ChannelStateType.Closed)
                {
                    return;
                }

                _logger?.Info("Close.");

                if (state == ChannelStateType.Connecting ||
                    state == ChannelStateType.TokenChecking)
                {
                    _line?.Close();
                    _bindingLine?.Close();

                    SetState(ChannelStateType.Closed);
                }
                else if (state == ChannelStateType.Connected)
                {
                    if (_closeState == SessionCloseState.None)
                    {
                        // Try to close gracefully
                        SendFinPacket();
                    }
                }
            }
        }

        public override void Update(TimeSpan span)
        {
            lock (_lock)
            {
                // Offline check
                if (_offineElapsed.HasValue)
                {
                    _offineElapsed = _offineElapsed + span;
                    if (_offineElapsed > _settings.OfflineTimeout)
                    {
                        _logger?.Trace($"line offline timed out e={_offineElapsed} to={_settings.OfflineTimeout}");
                        CloseImmediately();
                        return;
                    }
                }

                // Rebind check
                {
                    if (_rebindElapsed.HasValue)
                    {
                        _rebindElapsed = _rebindElapsed + span;
                    }
                    if (_rebindCoolTimeElapsed.HasValue)
                    {
                        _rebindCoolTimeElapsed = _rebindCoolTimeElapsed + span;
                    }

                    bool isRebindTimedOut = _rebindElapsed.HasValue && _rebindElapsed.Value > _settings.RebindTimeout;
                    bool isRebindCoolTimeTimedOut = _rebindCoolTimeElapsed.HasValue && _rebindCoolTimeElapsed.Value > _settings.RebindCoolTimeTimeout;
                    if (isRebindTimedOut || isRebindCoolTimeTimedOut)
                    {
                        _logger?.Trace($"Try rebound rebindElapsed={_rebindElapsed} rebindCoolTimeElapsed={_rebindCoolTimeElapsed}");
                        TryRebindLine();
                        return;
                    }
                }

                // Alive check
                if (_aliveCheckElapsed.HasValue)
                {
                    _aliveCheckElapsed = _aliveCheckElapsed + span;

                    if (_aliveCheckElapsed.Value > _settings.AliveCheckInterval)
                    {
                        _aliveCheckElapsed = null;
                        _aliveCheckWaitElapsed = TimeSpan.Zero;

                        var ticks = (int)(DateTime.UtcNow.Ticks >> 32);
                        _aliveCheckTicks = ticks;
                        _line.Send(new SqEcho()
                        {
                            Ticks = ticks
                        });
                    }
                }
                else if (_aliveCheckWaitElapsed.HasValue)
                {
                    _aliveCheckWaitElapsed = _aliveCheckWaitElapsed + span;

                    if (_aliveCheckWaitElapsed.Value > _settings.AliveCheckWaitInterval)
                    {
                        _aliveCheckElapsed = null;
                        _aliveCheckWaitElapsed = null;

                        _line.Close();
                    }
                }

                // TimeWait check
                {
                    if (_closeState == SessionCloseState.TimeWait)
                    {
                        _timeWaitElapsed = _timeWaitElapsed + span;
                        if (_timeWaitElapsed > _settings.TimeWaitTimeout)
                        {
                            _logger?.Trace($"TimeWait timed out e={_timeWaitElapsed} to={_settings.TimeWaitTimeout}");
                            SetCloseState(SessionCloseState.Closed);
                            return;
                        }
                    }
                }
            }
        }

        protected override void SendRequestPacket(Packet packet)
        {
            lock (_lock)
            {
                if (_finMessageId.HasValue)
                {
                    return;
                }

                var p = new ScSessionInnerPacket()
                {
                    InnerPacket = packet
                };
                SendReliablePacket(p);
            }
        }

        private void TryRebindLine()
        {
            _lastLineIndex++;
            var lineIndex = _lastLineIndex;

            var newBindingLine = new TcpSessionLine(_logger, _remoteEndPoint, _sessionPacketSerializer, _lastLineIndex);
            newBindingLine.Rebound += OnLineRebound;
            newBindingLine.Closed += OnRebindingLineClose;
            newBindingLine.Rebind(_sessionId, _lastRecvMessageId);

            var oldLine = _bindingLine;
            _bindingLine = newBindingLine;

            if (oldLine != null)
            {
                oldLine.Close();
            }

            _rebindElapsed = TimeSpan.Zero;
            _rebindCoolTimeElapsed = null;
        }

        private void CloseImmediately()
        {
            SetState(ChannelStateType.Closed);

            _line?.Close();
            _bindingLine?.Close();

            _offineElapsed = null;
            _rebindElapsed = null;
            _rebindCoolTimeElapsed = null;
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
            var line = _line;
            if (line == null)
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
                    line.Send(p);
                }
                _lastSendMessageIdOfCurrentLine = _lastSendMessageId;
            }
        }

        // BEWARE: CALLED BY WORK THREAD
        private void OnLineCreated(object line, int sessionId)
        {
            lock (_lock)
            {
                var newLine = line as TcpSessionLine;
                if (newLine != _bindingLine)
                {
                    newLine.Close();
                    return;
                }

                _bindingLine = null;

                if (_state == ChannelStateType.Connecting)
                {
                    _logger?.Trace($"client line created");

                    _aliveCheckElapsed = TimeSpan.Zero;
                    _aliveCheckWaitElapsed = null;

                    newLine.Closed -= OnCreatingLineClose;
                    newLine.Closed += OnLineClose;
                    newLine.Received += OnLineReceive;
                    _sessionId = sessionId;
                    _line = newLine;

                    SetConnected();
                }
                else
                {
                    newLine.Close();
                }
            }
        }

        // BEWARE: CALLED BY WORK THREAD
        private void OnLineRebound(object line, int serverMessageAck)
        {
            lock (_lock)
            {
                var newLine = line as TcpSessionLine;
                if (newLine != _bindingLine)
                {
                    newLine.Close();
                    return;
                }

                // Check ack
                var serverAck = serverMessageAck;
                var firstSendMessageId = _sendPackets.Count > 0 ? _sendPackets[0].MessageId : _lastSendMessageId;
                if (serverAck - firstSendMessageId > 1)
                {
                    _logger?.Error($"fail to resend to server f={firstSendMessageId} ack={serverAck}");
                    CloseImmediately();
                    return;
                }

                _logger?.Trace($"client line rebound");

                // Adjust timers
                _offineElapsed = null;
                _rebindElapsed = null;
                _rebindCoolTimeElapsed = null;
                _aliveCheckElapsed = TimeSpan.Zero;
                _aliveCheckWaitElapsed = null;

                // Replace line
                newLine.Closed -= OnRebindingLineClose;
                newLine.Closed += OnLineClose;
                newLine.Received += OnLineReceive;
                _line = newLine;
                _bindingLine = null;

                // Try resend packets
                RemoveToAck(serverMessageAck);
                _lastSendMessageIdOfCurrentLine = serverMessageAck;
                TrySendToLine();
            }
        }

        // BEWARE: CALLED BY WORK THREAD
        private void OnLineReceive(object line, object packet)
        {
            lock (_lock)
            {
                var newLine = line as TcpSessionLine;
                if (_line != newLine)
                {
                    return;
                }

                if (packet is SessionPacket)
                {
                    HandleSessionPacket((SessionPacket)packet);
                }
            }
        }

        // BEWARE: CALLED BY WORK THREAD
        private void OnCreatingLineClose(object sender, int reason)
        {
            lock (_lock)
            {
                var line = sender as ISessionLine;
                if (_bindingLine != line)
                {
                    return;
                }

                _logger?.TraceFormat("Creating line closed. (reason={0})", reason);

                if (_connectTcs != null)
                {
                    _connectTcs.TrySetException(new SocketException(reason));
                    _connectTcs = null;
                }

                SetState(ChannelStateType.Closed);
            }
        }

        // BEWARE: CALLED BY WORK THREAD
        private void OnRebindingLineClose(object sender, int reason)
        {
            lock (_lock)
            {
                var line = sender as ISessionLine;
                if (_bindingLine != line)
                {
                    return;
                }

                _logger?.TraceFormat("Rebinding line closed. (reason={0})", reason);

                _bindingLine = null;
                _rebindElapsed = null;
                _rebindCoolTimeElapsed = TimeSpan.Zero;
            }
        }

        // BEWARE: CALLED BY WORK THREAD
        private void OnLineClose(object sender, int reason)
        {
            lock (_lock)
            {
                var line = sender as ISessionLine;
                if (_line != line)
                {
                    return;
                }

                _logger?.TraceFormat("Current line closed. (reason={0})", reason);

                if (_state == ChannelStateType.Closed)
                {
                    return;
                }

                _line = null;
                _offineElapsed = TimeSpan.Zero;
                _rebindElapsed = null;
                _rebindCoolTimeElapsed = TimeSpan.Zero;
                _aliveCheckElapsed = null;
                _aliveCheckWaitElapsed = null;
            }
        }

        private void SetConnected()
        {
            SetState(ChannelStateType.Connected);

            if (_connectTcs != null)
            {
                _connectTcs.TrySetResult(true);
                _connectTcs = null;
            }
        }

        private void HandleSessionPacket(SessionPacket packet)
        {
            if (packet == null)
            {
                _logger?.Warn($"packet is not SessionPacket t={packet.GetType().FullName}");
                return;
            }

            if (packet is SessionReliablePacket)
            {
                HandleReliablePacket((SessionReliablePacket)packet);
            }
            else if (packet is ScSessionAck)
            {
                HandleSessionAckPacket((ScSessionAck)packet);
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
                var echoTicks = ((SrEcho)packet).Ticks;
                if (_aliveCheckWaitElapsed.HasValue && _aliveCheckTicks == echoTicks)
                {
                    _aliveCheckElapsed = TimeSpan.Zero;
                    _aliveCheckWaitElapsed = null;

                    var now = (int)(DateTime.UtcNow.Ticks >> 32);
                    var elapsed = (long)(now - ((SrEcho)packet).Ticks);
                    var oldSrtt = _smoothedRoundTripTicks;
                    var newSrtt = oldSrtt.HasValue ? ((oldSrtt.Value * 875) + (elapsed * 125)) / 1000 : elapsed;
                    _smoothedRoundTripTicks = newSrtt;

                    _logger?.Trace($"Srtt updated v={_smoothedRoundTripTicks.Value}");
                }
            }
            else
            {
                _logger?.Warn($"SessionChannel receives non acceptable packet");
            }
        }

        private void HandleReliablePacket(SessionReliablePacket packet)
        {
            var p = packet;

            // Check if received already
            if (_lastRecvMessageId - p.MessageId >= 0)
            {
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
            RemoveToAck(packet.MessageAck);

            if (p is ScSessionInnerPacket)
            {
                OnPacket(((ScSessionInnerPacket)packet).InnerPacket);
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

        private void HandleSessionAckPacket(ScSessionAck packet)
        {
            RemoveToAck(packet.MessageAck);
        }

        private void RemoveToAck(int ack)
        {
            bool isServerFinAckReceivedNow = false;

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

                if (_finAckMessageId.HasValue && ack - _finAckMessageId.Value >= 0)
                {
                    isServerFinAckReceivedNow = true;
                }
            }

            if (isServerFinAckReceivedNow && _isServerFinAckReceived == false)
            {
                _isServerFinAckReceived = true;
                if (_closeState == SessionCloseState.TimeWait)
                {
                    SetCloseState(SessionCloseState.Closed);
                }
            }
        }

        private void SetCloseState(SessionCloseState newState)
        {
            _closeState = newState;
            CloseStateChanged?.Invoke(this, newState);

            if (newState == SessionCloseState.Closed)
            {
                SetState(ChannelStateType.Closed);
            }
            else if (newState == SessionCloseState.TimeWait)
            {
                if (_timeWaitElapsed.HasValue == false)
                {
                    _timeWaitElapsed = TimeSpan.Zero;
                }
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
                if (_isServerFinAckReceived == false)
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
            }
        }

        private void SendFinPacket()
        {
            if (_closeState != SessionCloseState.None &&
                _closeState != SessionCloseState.CloseWait)
            {
                _logger?.Warn($"Fin requested again cs={_closeState}");
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
    }
}
