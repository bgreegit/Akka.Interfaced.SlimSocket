using Common.Logging;
using System;

namespace Akka.Interfaced.SlimSocket.Client.WebSocketChannel
{
    public class WebSocketClientChannelType : IClientChannelType
    {
        public string ConnectUri { get; set; }
        public string ConnectToken { get; set; }
        public Func<IWebSocket> CreateWebSocket { get; set; }

        public string Name => TypeName;
        public const string TypeName = "WebSocket";

        public ChannelBase CreateChannel(string address, ILog channelLogger, IPacketSerializer packetSerializer)
        {
            var connectUri = ConnectUri;
            var connectToken = ConnectToken;

            if (string.IsNullOrEmpty(address) == false)
            {
                // type|endpoint|{token}
                var parts = address.Split('|');
                if (parts.Length < 2)
                {
                    return null;
                }
                if (parts[0] != Name)
                {
                    return null;
                }

                connectUri = parts[1];
                connectToken = parts.Length > 2 ? parts[2] : null;
            }

            var createWebSocket = CreateWebSocket;
            if (createWebSocket == null)
            {
                // Default WebSocket implementation
                createWebSocket = () => { return new WebSocket(channelLogger); };
            }

            return new WebSocketChannel(channelLogger, connectUri, connectToken, packetSerializer, createWebSocket);
        }
    }
}
