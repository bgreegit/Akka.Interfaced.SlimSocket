using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Interfaced;
using Akka.Interfaced.SlimServer;
using Akka.Interfaced.SlimSocket;
using Akka.Interfaced.SlimSocket.Server;
using Common.Logging;
using UnityBasic.Interface;
using Akka.Interfaced.SlimSocket.Server.TcpChannel;
using Akka.Interfaced.SlimSocket.Server.UdpChannel;
using Akka.Interfaced.SlimSocket.Server.SessionChannel;
using Akka.Interfaced.SlimSocket.Server.WebSocketChannel;

namespace UnityBasic.Program.Server
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            if (typeof(ICalculator) == null)
            {
                throw new Exception("Force interface module to be loaded");
            }

            using (var system = ActorSystem.Create("MySystem", "akka.loglevel = DEBUG \n akka.actor.debug.lifecycle = on"))
            {
                DeadRequestProcessingActor.Install(system);

                var gateways = new List<GatewayRef>();
                gateways.AddRange(StartGateway(system, TcpChannelType.TypeName, 5001, 5002));
                gateways.AddRange(StartGateway(system, UdpChannelType.TypeName, 5001, 5002));

                Console.WriteLine("Please enter key to quit.");
                Console.ReadLine();

                Task.WaitAll(gateways.Select(g => g.Stop()).ToArray());
            }
        }

        private static void InitializeGateway1Initiator(GatewayInitiator initiator, EntryActorEnvironment environment)
        {
            initiator.GatewayLogger = LogManager.GetLogger($"Gateway1");
            initiator.GatewayInitialized = a => { environment.Gateway = a.Cast<ActorBoundGatewayRef>(); };
            initiator.CreateChannelLogger = (ep, _) => LogManager.GetLogger($"Channel({ep})");
            initiator.PacketSerializer = PacketSerializer.CreatePacketSerializer();
            initiator.CreateInitialActors = (context, connection) => new[]
            {
                Tuple.Create(context.ActorOf(Props.Create(() => new EntryActor(environment, context.Self.Cast<ActorBoundChannelRef>()))),
                                new TaggedType[] { typeof(IEntry) },
                                (ActorBindingFlags)0)
            };
        }

        private static void InitializeGateway2Initiator(GatewayInitiator initiator, EntryActorEnvironment environment)
        {
            initiator.GatewayLogger = LogManager.GetLogger($"Gateway2");
            initiator.TokenRequired = true;
            initiator.GatewayInitialized = a => { environment.Gateway2nd = a.Cast<ActorBoundGatewayRef>(); };
            initiator.CreateChannelLogger = (ep, _) => LogManager.GetLogger($"Channel2({ep})");
            initiator.PacketSerializer = PacketSerializer.CreatePacketSerializer();
        }

        private static GatewayRef[] StartGateway(ActorSystem system, string channelType, int port, int port2)
        {
            var serializer = PacketSerializer.CreatePacketSerializer();
            var environment = new EntryActorEnvironment();

            // First gateway

            GatewayRef gateway1 = null;
            if (channelType == TcpChannelType.TypeName)
            {
                var initiator = new TcpGatewayInitiator()
                {
                    ListenEndPoint = new IPEndPoint(IPAddress.Any, port),
                    TcpConnectionSettings = new TcpConnectionSettings { PacketSerializer = serializer },
                };
                InitializeGateway1Initiator(initiator, environment);
                gateway1 = system.ActorOf(Props.Create(() => new TcpGateway(initiator))).Cast<GatewayRef>();
            }
            else if (channelType == UdpChannelType.TypeName)
            {
                var initiator = new UdpGatewayInitiator()
                {
                    ListenEndPoint = new IPEndPoint(IPAddress.Any, port),
                };
                InitializeGateway1Initiator(initiator, environment);
                gateway1 = system.ActorOf(Props.Create(() => new UdpGateway(initiator))).Cast<GatewayRef>();
            }
            else if (channelType == SessionChannelType.TypeName)
            {
                var initiator = new SessionGatewayInitiator()
                {
                    ListenEndPoint = new IPEndPoint(IPAddress.Any, port),
                    SessionSettings = new SessionSettings(),
                    TcpConnectionSettings = new TcpConnectionSettings { PacketSerializer = serializer },
                };
                InitializeGateway1Initiator(initiator, environment);
                gateway1 = system.ActorOf(Props.Create(() => new SessionGateway(initiator))).Cast<GatewayRef>();
            }
            else if (channelType == WebSocketChannelType.TypeName)
            {
                var initiator = new WebSocketGatewayInitiator()
                {
                    ListenUri = string.Format("http://+:{0}/ws/", port),
                };
                InitializeGateway1Initiator(initiator, environment);
                gateway1 = system.ActorOf(Props.Create(() => new WebSocketGateway(initiator))).Cast<GatewayRef>();
            }
            gateway1.Start().Wait();

            // Second gateway

            GatewayRef gateway2 = null;
            if (channelType == TcpChannelType.TypeName)
            {
                var initiator = new TcpGatewayInitiator()
                {
                    ListenEndPoint = new IPEndPoint(IPAddress.Any, port),
                    ConnectEndPoint = new IPEndPoint(IPAddress.Loopback, port2),
                    TcpConnectionSettings = new TcpConnectionSettings { PacketSerializer = serializer },
                };
                InitializeGateway2Initiator(initiator, environment);
                gateway2 = system.ActorOf(Props.Create(() => new TcpGateway(initiator))).Cast<GatewayRef>();
            }
            else if (channelType == UdpChannelType.TypeName)
            {
                var initiator = new UdpGatewayInitiator()
                {
                    ListenEndPoint = new IPEndPoint(IPAddress.Any, port),
                    ConnectEndPoint = new IPEndPoint(IPAddress.Loopback, port2),
                };
                InitializeGateway2Initiator(initiator, environment);
                gateway2 = system.ActorOf(Props.Create(() => new UdpGateway(initiator))).Cast<GatewayRef>();
            }
            else if (channelType == SessionChannelType.TypeName)
            {
                var initiator = new SessionGatewayInitiator()
                {
                    ListenEndPoint = new IPEndPoint(IPAddress.Any, port),
                    ConnectEndPoint = new IPEndPoint(IPAddress.Loopback, port2),
                    SessionSettings = new SessionSettings(),
                    TcpConnectionSettings = new TcpConnectionSettings { PacketSerializer = serializer },
                };
                InitializeGateway2Initiator(initiator, environment);
                gateway2 = system.ActorOf(Props.Create(() => new SessionGateway(initiator))).Cast<GatewayRef>();
            }
            else if (channelType == WebSocketChannelType.TypeName)
            {
                var initiator = new WebSocketGatewayInitiator()
                {
                    ListenUri = string.Format("http://+:{0}/ws/", port),
                    ConnectUri = string.Format("http://127.0.0.1:{0}/ws/", port),
                };
                InitializeGateway2Initiator(initiator, environment);
                gateway2 = system.ActorOf(Props.Create(() => new WebSocketGateway(initiator))).Cast<GatewayRef>();
            }
            gateway2.Start().Wait();

            return new[] { gateway1, gateway2 };
        }
    }
}
