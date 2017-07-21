namespace Akka.Interfaced.SlimSocket.Server.WebSocketChannel
{
    public class WebSocketChannelType : IChannelType
    {
        public string Name => TypeName;
        public const string TypeName = "WebSocket";
    }
}
