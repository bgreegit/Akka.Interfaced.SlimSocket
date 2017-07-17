using System;
using System.Net;
using Akka.Actor;
using Akka.Interfaced.SlimServer;
using Common.Logging;
using Lidgren.Network;

namespace Akka.Interfaced.SlimSocket.Server
{
    public class GatewayInitiator
    {
        public IPEndPoint ListenEndPoint { get; set; } // for Tcp or Udp
        public IPEndPoint ConnectEndPoint { get; set; } // for Tcp or Udp
        public string ListenUri { get; set; } // for WebSocket
        public string ConnectUri { get; set; } // for WebSocket
        public ILog GatewayLogger { get; set; }
        public bool TokenRequired { get; set; }
        public TimeSpan TokenTimeout { get; set; }
        public Action<IActorRef> GatewayInitialized { get; set; }
        public Func<EndPoint, object, ILog> CreateChannelLogger { get; set; }
        public Func<EndPoint, object, bool> CheckCreateChannel { get; set; }
        public SessionSettings SessionSettings { get; set; }
        public TcpConnectionSettings TcpConnectionSettings { get; set; }
        public WebSocketConnectionSettings WebSocketConnectionSettings { get; set; }
        public IPacketSerializer PacketSerializer { get; set; }
        public object UdpConfig { get; set; }
        public object WebSocketConfig { get; set; } // For future use
        public Func<IActorContext, object, Tuple<IActorRef, TaggedType[], ActorBindingFlags>[]> CreateInitialActors { get; set; }

        public GatewayInitiator()
        {
            TokenTimeout = TimeSpan.FromSeconds(30);

            var udpConfig = new NetPeerConfiguration("SlimSocket");
            udpConfig.AutoExpandMTU = true;
            UdpConfig = udpConfig;
        }

        public static IPEndPoint GetRemoteEndPoint(object connection)
        {
            var tcpConnection = connection as TcpConnection;
            if (tcpConnection != null)
                return tcpConnection.RemoteEndPoint;

            var udpConnection = connection as NetConnection;
            if (udpConnection != null)
                return udpConnection.RemoteEndPoint;

            var webSocketConnection = connection as WebSocketConnection;
            if (webSocketConnection != null)
                return webSocketConnection.RemoteEndPoint;

            return null;
        }
    }
}
