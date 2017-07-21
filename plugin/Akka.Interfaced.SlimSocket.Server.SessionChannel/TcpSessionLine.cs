using System;
using System.Net;
using System.Net.Sockets;
using Akka.Interfaced.SlimSocket.Server.TcpChannel;
using Common.Logging;

namespace Akka.Interfaced.SlimSocket.Server.SessionChannel
{
    public class TcpSessionLine : ISessionLine
    {
        private SessionGatewayInitiator _initiator;
        private ILog _logger;
        private Socket _socket;
        private EndPoint _remoteEndPoint;
        private TcpConnection _connection;
        private int _sessionId;
        private int _lineIndex;

        public int SessionId
        {
            get { return _sessionId; }
            set { _sessionId = value; }
        }

        public int LineIndex
        {
            get { return _lineIndex; }
            set { _lineIndex = value; }
        }

        public EndPoint RemoteEndPoint
        {
            get { return _remoteEndPoint; }
        }

        public event Action<ISessionLine> Closed;
        public event Action<ISessionLine, SessionPacket> Received;

        public TcpSessionLine(SessionGatewayInitiator initiator, Socket socket)
        {
            _initiator = initiator;
            _logger = _initiator.CreateChannelLogger(socket.RemoteEndPoint, socket);
            _socket = socket;
            _remoteEndPoint = socket.RemoteEndPoint;

            _connection = new TcpConnection(_logger, _socket)
            {
                Settings = _initiator.TcpConnectionSettings
            };
            _connection.CustomPacketSerializer = new SessionPacketSerializer(initiator.PacketSerializer);
            _connection.Closed += OnConnectionClose;
            _connection.Received += OnConnectionReceive;
        }

        public void Open()
        {
            _connection.Open();
        }

        public void Send(object packet)
        {
            _connection.Send(packet);
        }

        public void Close(bool isGracefulPreferred)
        {
            if (isGracefulPreferred)
            {
                _connection.FlushAndClose();
            }
            else
            {
                _connection.Close();
            }
        }

        protected void OnConnectionClose(TcpConnection connection, int reason)
        {
            Closed?.Invoke(this);
        }

        protected void OnConnectionReceive(TcpConnection connection, object packet)
        {
            var sp = packet as SessionPacket;
            if (sp == null)
            {
                _logger?.Warn($"Not session packet t={packet.GetType().FullName}");
                return;
            }
            Received?.Invoke(this, sp);
        }
    }
}
