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
                var tcpGateway = StartGateway(system, ChannelType.Tcp, port);
                var udpGateway = StartGateway(system, ChannelType.Udp, port);
                var webSocketGateway = StartGateway(system, ChannelType.WebSocket, port + 1);

                Console.WriteLine("Please enter key to quit.");
                Console.ReadLine();

                tcpGateway.Stop().Wait();
                udpGateway.Stop().Wait();
                webSocketGateway.Stop().Wait();
            }
        }

        private static GatewayRef StartGateway(ActorSystem system, ChannelType type, int port)
        {
            var serializer = PacketSerializer.CreatePacketSerializer();

            var initiator = new GatewayInitiator
            {
                ListenEndPoint = new IPEndPoint(IPAddress.Any, port),
                ListenUri = string.Format("http://+:{0}/ws/", port),
                GatewayLogger = LogManager.GetLogger("Gateway"),
                CreateChannelLogger = (ep, _) => LogManager.GetLogger($"Channel({ep})"),
                TcpConnectionSettings = new TcpConnectionSettings { PacketSerializer = serializer },
                WebSocketConnectionSettings = new WebSocketConnectionSettings { PacketSerializer = serializer },
                PacketSerializer = serializer,
                CreateInitialActors = (context, connection) => new[]
                {
                    Tuple.Create(context.ActorOf(Props.Create(() => new EntryActor(context.Self.Cast<ActorBoundChannelRef>()))),
                                 new TaggedType[] { typeof(IEntry) },
                                 (ActorBindingFlags)0)
                }
            };

            GatewayRef gateway = null;
            if (type == ChannelType.Tcp)
            {
                gateway = system.ActorOf(Props.Create(() => new TcpGateway(initiator))).Cast<GatewayRef>();
            }
            else if (type == ChannelType.Udp)
            {
                gateway = system.ActorOf(Props.Create(() => new UdpGateway(initiator))).Cast<GatewayRef>();
            }
            else
            {
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
