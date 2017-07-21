using System.Net;

namespace Akka.Interfaced.SlimSocket.Server.TcpChannel
{
    public class TcpGatewayInitiator : GatewayInitiator
    {
        public IPEndPoint ListenEndPoint { get; set; }
        public IPEndPoint ConnectEndPoint { get; set; }
        public TcpConnectionSettings TcpConnectionSettings { get; set; }

        public override IPEndPoint GetRemoteEndPoint(object obj)
        {
            var tcpConnection = obj as TcpConnection;
            return tcpConnection?.RemoteEndPoint;
        }
    }
}
