using Lidgren.Network;
using System.Net;

namespace Akka.Interfaced.SlimSocket.Server.UdpChannel
{
    public class UdpGatewayInitiator : GatewayInitiator
    {
        public IPEndPoint ListenEndPoint { get; set; }
        public IPEndPoint ConnectEndPoint { get; set; }
        public NetPeerConfiguration UdpConfig { get; set; }

        public UdpGatewayInitiator()
        {
            var udpConfig = new NetPeerConfiguration("SlimSocket");
            udpConfig.AutoExpandMTU = true;
            UdpConfig = udpConfig;
        }

        public override IPEndPoint GetRemoteEndPoint(object obj)
        {
            var udpConnection = obj as NetConnection;
            return udpConnection?.RemoteEndPoint;
        }
    }
}
