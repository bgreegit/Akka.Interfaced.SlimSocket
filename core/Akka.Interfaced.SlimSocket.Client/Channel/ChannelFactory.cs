using System;
using System.Net;
using Common.Logging;
using Lidgren.Network;

namespace Akka.Interfaced.SlimSocket.Client
{
    public class ChannelFactory
    {
        public ChannelType Type { get; set; }
        public IPEndPoint ConnectEndPoint { get; set; }
        public string ConnectUri { get; set; }
        public string ConnectToken { get; set; }
        public Func<ILog> CreateChannelLogger { get; set; }
        public ISlimTaskFactory TaskFactory { get; set; }
        public Func<IObserverRegistry> CreateObserverRegistry { get; set; }
        public Func<IChannel, string, IChannel> ChannelRouter { get; set; }
        public IPacketSerializer PacketSerializer { get; set; }
        public object UdpConfig { get; set; }
        public Func<IWebSocket> CreateWebSocket { get; set; }

        public ChannelFactory()
        {
            TaskFactory = new SlimTaskFactory();
            UdpConfig = new NetPeerConfiguration("SlimSocket");
        }

        public IChannel Create()
        {
            return Create(null);
        }

        public IChannel Create(string address)
        {
            var type = Type;
            var connectEndPoint = ConnectEndPoint;
            var connectUri = ConnectUri;
            var connectToken = ConnectToken;

            if (string.IsNullOrEmpty(address) == false)
            {
                var parts = address.Split('|'); // type|endpoint|{token}
                if (parts.Length < 2)
                    throw new ArgumentException(nameof(address));
                type = (ChannelType)Enum.Parse(typeof(ChannelType), parts[0], true);
                if (type == ChannelType.Tcp || type == ChannelType.Udp)
                    connectEndPoint = IPEndPointHelper.Parse(parts[1]);
                else if (type == ChannelType.WebSocket)
                    connectUri = parts[1];
                else
                    throw new ArgumentException(nameof(address));
                connectToken = parts.Length > 2 ? parts[2] : null;
            }

            switch (type)
            {
                case ChannelType.Tcp:
                    var tcpChannel = new TcpChannel(CreateChannelLogger(), connectEndPoint, connectToken, PacketSerializer);
                    InitializeChannel(tcpChannel);
                    return tcpChannel;

                case ChannelType.Udp:
                    var udpChannel = new UdpChannel(CreateChannelLogger(), connectEndPoint, connectToken, PacketSerializer, (NetPeerConfiguration)UdpConfig);
                    InitializeChannel(udpChannel);
                    return udpChannel;

                case ChannelType.WebSocket:
                    var logger = CreateChannelLogger();

                    var createWebSocket = CreateWebSocket;
                    if (createWebSocket == null)
                        createWebSocket = () => { return new WebSocket(logger); };

                    var webSocketChannel = new WebSocketChannel(logger, connectUri, connectToken, PacketSerializer, createWebSocket);
                    InitializeChannel(webSocketChannel);
                    return webSocketChannel;

                default:
                    throw new InvalidOperationException("Unknown TransportType");
            }
        }

        private void InitializeChannel(ChannelBase channel)
        {
            channel.TaskFactory = TaskFactory;
            channel.ObserverRegistry = CreateObserverRegistry?.Invoke();
            channel.ChannelRouter = ChannelRouter;
        }
    }
}
