using System;
using System.Diagnostics;
using System.Net;
using Akka.Actor;
using Akka.Interfaced;
using Akka.Interfaced.SlimServer;
using Akka.Interfaced.SlimSocket;
using Akka.Interfaced.SlimSocket.Server;
using Common.Logging;
using HelloWorld.Interface;
using Akka.Interfaced.SlimSocket.Server.TcpChannel;
using Akka.Interfaced.SlimSocket.Server.UdpChannel;
using Akka.Interfaced.SlimSocket.Server.WebSocketChannel;
using Akka.Interfaced.SlimSocket.Server.SessionChannel;

namespace HelloWorld.Program.Server
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            if (typeof(IGreeter) == null)
            {
                throw new Exception("Force interface module to be loaded");
            }

            using (var system = ActorSystem.Create("MySystem", "akka.loglevel = DEBUG \n akka.actor.debug.lifecycle = on"))
            {
                DeadRequestProcessingActor.Install(system);

                int port = 5001;
                var tcpGateway = StartGateway(system, TcpChannelType.TypeName, port);
                var udpGateway = StartGateway(system, UdpChannelType.TypeName, port);
                var sessionGateway = StartGateway(system, SessionChannelType.TypeName, port + 1);
                var webSocketGateway = StartGateway(system, WebSocketChannelType.TypeName, port + 2);

                Console.WriteLine("Please enter key to quit.");
                Console.ReadLine();

                tcpGateway.Stop().Wait();
                udpGateway.Stop().Wait();
                sessionGateway.Stop().Wait();
                webSocketGateway.Stop().Wait();
            }
        }

        private static void InitializeGatewayInitiator(GatewayInitiator initiator, string channelType)
        {
            initiator.GatewayLogger = LogManager.GetLogger($"Gateway({channelType})");
            initiator.CreateChannelLogger = (ep, _) => LogManager.GetLogger($"Channel({ep})");
            initiator.PacketSerializer = PacketSerializer.CreatePacketSerializer();
            initiator.CreateInitialActors = (context, connection) => new[]
            {
                Tuple.Create(context.ActorOf(Props.Create(() => new EntryActor(context.Self.Cast<ActorBoundChannelRef>()))),
                            new TaggedType[] { typeof(IEntry) },
                            (ActorBindingFlags)0)
            };
        }

        private static GatewayRef StartGateway(ActorSystem system, string channelType, int port)
        {
            var serializer = PacketSerializer.CreatePacketSerializer();

            GatewayRef gateway = null;
            if (channelType == TcpChannelType.TypeName)
            {
                var initiator = new TcpGatewayInitiator()
                {
                    ListenEndPoint = new IPEndPoint(IPAddress.Any, port),
                    TcpConnectionSettings = new TcpConnectionSettings { PacketSerializer = serializer },
                };
                InitializeGatewayInitiator(initiator, channelType);
                gateway = system.ActorOf(Props.Create(() => new TcpGateway(initiator))).Cast<GatewayRef>();
            }
            else if (channelType == UdpChannelType.TypeName)
            {
                var initiator = new UdpGatewayInitiator()
                {
                    ListenEndPoint = new IPEndPoint(IPAddress.Any, port),
                };
                InitializeGatewayInitiator(initiator, channelType);
                gateway = system.ActorOf(Props.Create(() => new UdpGateway(initiator))).Cast<GatewayRef>();
            }
            else if (channelType == SessionChannelType.TypeName)
            {
                var initiator = new SessionGatewayInitiator()
                {
                    ListenEndPoint = new IPEndPoint(IPAddress.Any, port),
                    TcpConnectionSettings = new TcpConnectionSettings { PacketSerializer = serializer },
                    SessionSettings = new SessionSettings()
                };
                InitializeGatewayInitiator(initiator, channelType);
                gateway = system.ActorOf(Props.Create(() => new SessionGateway(initiator))).Cast<GatewayRef>();
            }
            else if (channelType == WebSocketChannelType.TypeName)
            {
                var initiator = new WebSocketGatewayInitiator()
                {
                    ListenUri = string.Format("http://+:{0}/ws/", port),
                    WebSocketConnectionSettings = new WebSocketConnectionSettings { PacketSerializer = serializer },
                };
                InitializeGatewayInitiator(initiator, channelType);
                AddNetAclAddress(initiator.ListenUri);
                gateway = system.ActorOf(Props.Create(() => new WebSocketGateway(initiator))).Cast<GatewayRef>();
            }
            gateway.Start().Wait();
            return gateway;
        }

        public static void AddNetAclAddress(string address, string domain = null, string user = null)
        {
            if (string.IsNullOrEmpty(domain))
            {
                domain = Environment.UserDomainName;
            }

            if (string.IsNullOrEmpty(user))
            {
                user = Environment.UserName;
            }

            string args = string.Format(@"http add urlacl url={0} user={1}\{2}", address, domain, user);

            ProcessStartInfo psi = new ProcessStartInfo("netsh", args);
            psi.Verb = "runas";
            psi.CreateNoWindow = true;
            psi.WindowStyle = ProcessWindowStyle.Hidden;
            psi.UseShellExecute = true;

            Process.Start(psi).WaitForExit();
        }
    }
}
