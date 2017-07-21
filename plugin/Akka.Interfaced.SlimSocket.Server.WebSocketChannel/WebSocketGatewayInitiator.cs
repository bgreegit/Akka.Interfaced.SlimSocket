using System.Net;

namespace Akka.Interfaced.SlimSocket.Server.WebSocketChannel
{
    public class WebSocketGatewayInitiator : GatewayInitiator
    {
        public string ListenUri { get; set; }
        public string ConnectUri { get; set; }
        public WebSocketConnectionSettings WebSocketConnectionSettings { get; set; }
        public object WebSocketConfig { get; set; } // For future use

        public override IPEndPoint GetRemoteEndPoint(object obj)
        {
            var webSocketConnection = obj as WebSocketConnection;
            return webSocketConnection?.RemoteEndPoint;
        }
    }
}
