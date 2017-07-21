using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using Common.Logging;

namespace Akka.Interfaced.SlimSocket.Client.WebSocketChannel
{
    public class WebSocketChannel : ChannelBase
    {
        private string _uri;
        private IPacketSerializer _packetSerializer;
        private string _token;
        private WebSocketConnection _webSocketConnection;
        private Func<IWebSocket> _createWebSocket;
        private ISlimTaskCompletionSource<bool> _connectTcs;

        public WebSocketChannel(ILog logger, string uri, string token, IPacketSerializer packetSerializer, Func<IWebSocket> createWebSocket)
            : base(logger)
        {
            _uri = uri;
            _token = token;
            _packetSerializer = packetSerializer;
            _createWebSocket = createWebSocket;
        }

        public override Task<bool> ConnectAsync()
        {
            if (State != ChannelStateType.Closed)
            {
                throw new InvalidOperationException("Should be closed to connect.");
            }

            var tcs = _connectTcs = TaskFactory.Create<bool>();
            _logger?.Info("Connect.");

            SetState(ChannelStateType.Connecting);

            var connection = new WebSocketConnection(_packetSerializer, _logger, _createWebSocket);
            _webSocketConnection = connection;
            _webSocketConnection.Connected += OnConnect;
            _webSocketConnection.Received += OnReceive;
            _webSocketConnection.Closed += OnClose;
            _webSocketConnection.Connect(_uri);

            return tcs.Task;
        }

        public override void Close()
        {
            _logger?.Info("Close.");

            SetState(ChannelStateType.Closed);
            _webSocketConnection?.Close();
        }

        public override void Update(TimeSpan span)
        {
        }

        private void OnConnect(object sender)
        {
            _logger?.Trace("Connected.");

            if (string.IsNullOrEmpty(_token))
            {
                SetConnected();
            }
            else
            {
                _webSocketConnection.SendPacket(new Packet
                {
                    Type = PacketType.System,
                    Message = _token
                });

                SetState(ChannelStateType.TokenChecking);
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

        private void OnReceive(object sender, object packet)
        {
            var p = (Packet)packet;
            if (p.Type == PacketType.System)
            {
                if (_state == ChannelStateType.TokenChecking)
                {
                    SetConnected();
                }
                else
                {
                    _logger.WarnFormat("System packet in wrong state({0}", _state);
                }
            }
            else
            {
                OnPacket(p);
            }
        }

        private void OnClose(object sender, int reason)
        {
            _logger?.TraceFormat("Closed. (reason={0})", reason);

            if (_connectTcs != null)
            {
                _connectTcs.TrySetException(new SocketException(reason));
                _connectTcs = null;
            }

            SetState(ChannelStateType.Closed);
        }

        protected override void SendRequestPacket(Packet packet)
        {
            if (_state == ChannelStateType.Connected)
            {
                _webSocketConnection.SendPacket(packet);
            }
        }
    }
}
