using Akka.Actor;
using Akka.Interfaced.SlimSocket.Client;
using Akka.Interfaced.SlimSocket.Client.SessionChannel;
using Akka.Interfaced.SlimSocket.Client.TcpChannel;
using Akka.Interfaced.SlimSocket.Client.UdpChannel;
using Akka.Interfaced.SlimSocket.Client.WebSocketChannel;
using Akka.Interfaced.SlimSocket.Server;
using Akka.Interfaced.SlimSocket.Server.SessionChannel;
using Akka.Interfaced.SlimSocket.Server.TcpChannel;
using Akka.Interfaced.SlimSocket.Server.UdpChannel;
using Akka.Interfaced.SlimSocket.Server.WebSocketChannel;
using System;
using System.Net;

namespace Akka.Interfaced.SlimSocket
{
    public static class ChannelHelper
    {
        private static readonly Server.PacketSerializer s_serverSerializer = Server.PacketSerializer.CreatePacketSerializer();
        private static readonly Client.PacketSerializer s_clientSerializer = Client.PacketSerializer.CreatePacketSerializer();

        public static Server.GatewayRef CreateGateway(ActorSystem system, string channelType, string name, IPEndPoint endPoint,
                                                      string listenUri, string connectUri,
                                                      XunitOutputLogger.Source outputSource,
                                                      Action<Server.GatewayInitiator> clientInitiatorSetup = null)
        {
            // initialize gateway initiator

            GatewayInitiator initiator = null;
            if (channelType == TcpChannelType.TypeName)
            {
                initiator = new TcpGatewayInitiator()
                {
                    ListenEndPoint = endPoint,
                    ConnectEndPoint = endPoint,
                    TcpConnectionSettings = new TcpConnectionSettings { PacketSerializer = s_serverSerializer },
                };
            }
            else if (channelType == UdpChannelType.TypeName)
            {
                initiator = new UdpGatewayInitiator()
                {
                    ListenEndPoint = endPoint,
                    ConnectEndPoint = endPoint,
                };
            }
            else if (channelType == SessionChannelType.TypeName)
            {
                initiator = new SessionGatewayInitiator()
                {
                    ListenEndPoint = endPoint,
                    ConnectEndPoint = endPoint,
                    SessionSettings = new Server.SessionChannel.SessionSettings()
                    {
                        OfflineTimeout = TimeSpan.FromSeconds(5),
                        TimeWaitTimeout = TimeSpan.FromSeconds(1), // to fail faster
                        AliveCheckInterval = TimeSpan.FromSeconds(0.1), // to fail faster
                        AliveCheckWaitInterval = TimeSpan.FromSeconds(0.5), // to fail faster
                    },
                    TcpConnectionSettings = new TcpConnectionSettings
                    {
                        PacketSerializer = s_serverSerializer
                    },
                };
            }
            else if (channelType == WebSocketChannelType.TypeName)
            {
                initiator = new WebSocketGatewayInitiator()
                {
                    ListenUri = listenUri,
                    ConnectUri = connectUri,
                    WebSocketConnectionSettings = new WebSocketConnectionSettings()
                    {
                        PacketSerializer = s_serverSerializer
                    },
                };
            }
            else
            {
                return null;
            }

            initiator.GatewayLogger = new XunitOutputLogger($"Gateway({name})", outputSource);
            initiator.TokenRequired = false;
            initiator.CreateChannelLogger = (_, o) => new XunitOutputLogger($"ServerChannel({name})", outputSource);
            initiator.CheckCreateChannel = (_, o) => true;
            initiator.PacketSerializer = s_serverSerializer;

            clientInitiatorSetup?.Invoke(initiator);

            // create gateway and start it

            GatewayRef gateway = null;
            if (channelType == TcpChannelType.TypeName)
            {
                gateway = system.ActorOf(Props.Create(() => new TcpGateway(initiator as TcpGatewayInitiator))).Cast<GatewayRef>();
            }
            else if (channelType == UdpChannelType.TypeName)
            {
                gateway = system.ActorOf(Props.Create(() => new UdpGateway(initiator as UdpGatewayInitiator))).Cast<GatewayRef>();
            }
            else if (channelType == SessionChannelType.TypeName)
            {
                gateway = system.ActorOf(Props.Create(() => new SessionGateway(initiator as SessionGatewayInitiator))).Cast<GatewayRef>();
            }
            else if (channelType == WebSocketChannelType.TypeName)
            {
                gateway = system.ActorOf(Props.Create(() => new WebSocketGateway(initiator as WebSocketGatewayInitiator))).Cast<GatewayRef>();
            }

            gateway.Start().Wait();
            return gateway;
        }

        public static Client.IChannel CreateClientChannel(string name, string channelType, IPEndPoint endPoint, string uri,
                                                          XunitOutputLogger.Source outputSource)
        {
            if (channelType == TcpClientChannelType.TypeName ||
                channelType == UdpClientChannelType.TypeName ||
                channelType == SessionClientChannelType.TypeName)
            {
                return CreateClientChannel(name, $"{channelType}", $"{channelType}|{endPoint}|", outputSource);
            }
            else
            {
                return CreateClientChannel(name, $"{channelType}", $"{channelType}|{uri}|", outputSource);
            }
        }

        public static Client.IChannel CreateClientChannel(string name, string channelType, string address, XunitOutputLogger.Source outputSource)
        {
            // create channel and start it

            var logger = new XunitOutputLogger($"ClientChannel({name})", outputSource);

            var factory = new ChannelFactory
            {
                CreateChannelLogger = () => logger,
                CreateObserverRegistry = () => new ObserverRegistry(),
                PacketSerializer = s_clientSerializer,
            };

            var tcpChannelType = new TcpClientChannelType();
            var udpChannelType = new UdpClientChannelType();
            var sessionChannelType = new SessionClientChannelType();
            var webSocketChannelType = new WebSocketClientChannelType();

            udpChannelType.UdpConfig.MaximumHandshakeAttempts = 1; // to fail faster
            sessionChannelType.SessionSettings = new Client.SessionChannel.SessionSettings()
            {
                OfflineTimeout = TimeSpan.FromSeconds(5), // to fail faster
                RebindCoolTimeTimeout = TimeSpan.FromSeconds(0.1), // to fail faster
                RebindTimeout = TimeSpan.FromSeconds(1), // to fail faster

                AliveCheckInterval = TimeSpan.FromSeconds(0.2),
                AliveCheckWaitInterval = TimeSpan.FromSeconds(1)
            };

            factory.RegisterChannelType(tcpChannelType);
            factory.RegisterChannelType(udpChannelType);
            factory.RegisterChannelType(sessionChannelType);
            factory.RegisterChannelType(webSocketChannelType);

            var channel = factory.CreateByAddress(address);
            var updator = new TaskBasedChannelUpdator();
            updator.StartUpdate(channel);
            return channel;
        }
    }
}
