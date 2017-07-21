using System.Net;
using Akka.Interfaced.SlimSocket.Server.TcpChannel;

namespace Akka.Interfaced.SlimSocket.Server.SessionChannel
{
    public class SessionGatewayInitiator : GatewayInitiator
    {
        public IPEndPoint ListenEndPoint { get; set; }
        public IPEndPoint ConnectEndPoint { get; set; }
        public SessionSettings SessionSettings { get; set; }
        public TcpConnectionSettings TcpConnectionSettings { get; set; }

        public override IPEndPoint GetRemoteEndPoint(object obj)
        {
            return null;
        }
    }
}
