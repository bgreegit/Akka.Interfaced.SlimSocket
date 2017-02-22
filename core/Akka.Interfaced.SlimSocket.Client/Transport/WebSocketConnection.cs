using System;
using System.IO;
using Common.Logging;

namespace Akka.Interfaced.SlimSocket.Client
{
    public class WebSocketConnection
    {
        public enum WebSocketState
        {
            Closed,
            Closing,
            Connected,
            Connecting,
        }

        private WebSocketState _state;
        private int _closeReason;
        private Func<IWebSocket> _createWebSocket;
        private IWebSocket _webSocket;
        private ILog _logger;

        private DateTime _lastReceiveTime;

        private readonly IPacketSerializer _packetSerializer;

        public WebSocketState State
        {
            get { return _state; }
        }

        public IPacketSerializer PacketSerializer
        {
            get { return _packetSerializer; }
        }

        public DateTime LastReceiveTime
        {
            get { return _lastReceiveTime; }
        }

        public event Action<object> Connected;
        public event Action<object, int> Closed;
        public event Action<object, object> Received;

        public readonly int MaxSendPacketLength = 131072;

        public WebSocketConnection(IPacketSerializer serializer, ILog logger, Func<IWebSocket> createWebSocket)
        {
            _state = WebSocketState.Closed;
            _logger = logger;
            _createWebSocket = createWebSocket;
            _packetSerializer = serializer;

            if (_createWebSocket == null)
                throw new ArgumentNullException("webSocketCreator");
        }

        public void Connect(string uri)
        {
            if (_state != WebSocketState.Closed)
            {
                throw new Exception("Closed Only");
            }

            _state = WebSocketState.Connecting;

            _webSocket = _createWebSocket();
            _webSocket.Connected += OnConnected;
            _webSocket.Closed += OnClosed;
            _webSocket.Received += OnReceived;
            _webSocket.Connect(uri);
        }

        public void Close(int reason = 0)
        {
            if (_state == WebSocketState.Connected)
            {
                _state = WebSocketState.Closing;
                _closeReason = reason;

                _webSocket.Close();
            }
        }

        public bool SendPacket(object packet)
        {
            if (_state != WebSocketState.Connected)
            {
                return false;
            }

            int length;
            using (var ms = new MemoryStream())
            {
                _packetSerializer.Serialize(ms, packet);
                length = (int)ms.Length;
                if (length > MaxSendPacketLength)
                {
                    _logger?.ErrorFormat("SendPacket got too large packet. Length={0}", length);
                    Close();
                    return false;
                }

                _webSocket.Send(ms.GetBuffer(), length, 0);
                return true;
            }
        }

        private void OnConnected(object sender)
        {
            _state = WebSocketState.Connected;

            if (Connected != null)
                Connected(this);
        }

        private void OnClosed(object sender, int code)
        {
            _state = WebSocketState.Closed;

            if (_closeReason == 0)
                _closeReason = code;
            if (Closed != null)
                Closed(this, _closeReason);
        }

        private void OnReceived(object sender, byte[] data)
        {
            using (var ms = new MemoryStream(data, 0, data.Length, false, true))
            {
                var length = _packetSerializer.PeekLength(ms);
                if (length == 0)
                {
                    _logger?.ErrorFormat("OnReceived got unknown length packet.");
                    Close();
                    return;
                }

                if (length != data.Length)
                {
                    _logger?.ErrorFormat("OnReceived got mismatched length packet.");
                    Close();
                    return;
                }

                try
                {
                    var packet = _packetSerializer.Deserialize(ms);
                    _lastReceiveTime = DateTime.UtcNow;
                    if (Received != null)
                        Received(this, packet);
                }
                catch (Exception e)
                {
                    _logger?.Error("Deserialize Error", e);
                    Close();
                    return;
                }
            }
        }
    }
}
