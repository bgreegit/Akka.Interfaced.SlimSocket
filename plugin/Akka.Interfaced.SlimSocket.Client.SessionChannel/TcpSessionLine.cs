using Akka.Interfaced.SlimSocket.Client.TcpChannel;
using Common.Logging;
using System;
using System.Net;

namespace Akka.Interfaced.SlimSocket.Client.SessionChannel
{
    public class TcpSessionLine : IReliableSessionLine
    {
        private ILog _logger;
        private IPEndPoint _localEndPoint;
        private IPEndPoint _remoteEndPoint;
        private TcpConnection _tcpConnection;
        private IPacketSerializer _packetSerializer;
        private int _lineIndex;

        private string _token;
        private int _sessionId;
        private int _clientMessageAck;
        private bool _isHandshaking;

        public int LineIndex { get { return _lineIndex; } }

        public event Action<object, int> Created;
        public event Action<object, int> Rebound;
        public event Action<object, int> Closed;
        public event Action<object, object> Received;

        public TcpSessionLine(ILog logger, IPEndPoint remoteEndPoint, IPacketSerializer packetSerializer, int lineIndex)
        {
            _logger = logger;
            _remoteEndPoint = remoteEndPoint;
            _packetSerializer = packetSerializer;
            _lineIndex = lineIndex;
        }

        public void Create(string token)
        {
            _token = token;

            var connection = new TcpConnection(_packetSerializer, _logger);
            _tcpConnection = connection;
            _tcpConnection.Connected += OnConnect;
            _tcpConnection.Received += OnReceive;
            _tcpConnection.Closed += OnClose;
            _tcpConnection.Connect(_remoteEndPoint);
        }

        public void Rebind(int sessionId, int clientMessageAck)
        {
            _sessionId = sessionId;
            _clientMessageAck = clientMessageAck;

            var connection = new TcpConnection(_packetSerializer, _logger);
            _tcpConnection = connection;
            _tcpConnection.Connected += OnConnect;
            _tcpConnection.Received += OnReceive;
            _tcpConnection.Closed += OnClose;
            _tcpConnection.Connect(_remoteEndPoint);
        }

        public void Send(object packet)
        {
            _tcpConnection.SendPacket(packet);
        }

        public void Close()
        {
            _tcpConnection?.Close();
        }

        // BEWARE: CALLED BY WORK THREAD
        private void OnConnect(object sender)
        {
            _isHandshaking = true;
            _localEndPoint = _tcpConnection.LocalEndPoint as IPEndPoint;

            if (_lineIndex == 0)
            {
                var sq = new SqSessionCreate()
                {
                    Token = _token
                };
                _tcpConnection.SendPacket(sq);
            }
            else
            {
                var sq = new SqSessionRebind()
                {
                    SessionId = _sessionId,
                    LineIndex = _lineIndex,
                    ClientMessageAck = _clientMessageAck
                };
                _tcpConnection.SendPacket(sq);
            }
        }

        // BEWARE: CALLED BY WORK THREAD
        private void OnReceive(object sender, object packet)
        {
            if (_isHandshaking)
            {
                _isHandshaking = false;

                if (_sessionId == 0)
                {
                    var sr = packet as SrSessionCreate;
                    if (sr == null)
                    {
                        _tcpConnection.Close();
                        return;
                    }

                    Created?.Invoke(this, sr.SessionId);
                }
                else
                {
                    var sr = packet as SrSessionRebind;
                    if (sr == null)
                    {
                        _tcpConnection.Close();
                        return;
                    }

                    Rebound?.Invoke(this, sr.ServerMessageAck);
                }
            }
            else
            {
                Received?.Invoke(this, packet);
            }
        }

        // BEWARE: CALLED BY WORK THREAD
        private void OnClose(object sender, int reason)
        {
            // No further receiving
            _tcpConnection.Received -= OnReceive;
            Closed?.Invoke(this, reason);
        }
    }
}
