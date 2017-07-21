using Akka.Actor;
using Akka.Interfaced;
using Akka.Interfaced.SlimServer;
using Akka.Interfaced.SlimSocket.Server;
using Akka.Interfaced.SlimSocket.Server.SessionChannel;
using Akka.Interfaced.SlimSocket.Server.TcpChannel;
using Common.Logging;
using Echo.Interface;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Net;
using System.Threading;

namespace Echo.Program.Server
{
    internal class Program
    {
        private class Config
        {
            public string ChannelType { get; set; }
            public string Ip { get; set; }
            public int Port { get; set; }
        }

        private static Timer _timer;

        private static void Main(string[] args)
        {
            if (typeof(IEcho) == null)
            {
                throw new Exception("Force interface module to be loaded");
            }

            var config = LoadConfig<Config>(args[0]);
            if (config == null)
            {
                Console.WriteLine($"Fail to read config {args[0]}");
                return;
            }

            // sample)
            //config = new Config()
            //{
            //    Ip = "127.0.0.1",
            //    Port = 5001,
            //};

            var s = JsonConvert.SerializeObject(config);
            Console.WriteLine(s);

            DoTest(config);
        }

        private static void DoTest(Config config)
        {
            _timer = new Timer(new TimerCallback(ShowStat), null, 1000, 1000);

            using (var system = ActorSystem.Create("MySystem", "akka.loglevel = DEBUG "))
            {
                DeadRequestProcessingActor.Install(system);
                var sessionGateway = StartGateway(system, config.ChannelType, config.Ip, config.Port);

                Console.WriteLine("Please enter key to quit.");
                Console.ReadLine();

                sessionGateway.Stop().Wait();
            }
        }

        private static GatewayRef StartGateway(ActorSystem system, string channelType, string ip, int port)
        {
            var serializer = PacketSerializer.CreatePacketSerializer();

            GatewayRef gateway = null;
            if (channelType == TcpChannelType.TypeName)
            {
                var initiator = new TcpGatewayInitiator
                {
                    // Common
                    GatewayLogger = LogManager.GetLogger($"Gateway(channelType)"),
                    CreateChannelLogger = (ep, _) => LogManager.GetLogger($"Channel({ep})"),
                    PacketSerializer = serializer,
                    CreateInitialActors = (context, connection) => new[]
                    {
                        Tuple.Create(context.ActorOf(Props.Create(() => new EchoActor())),
                                     new TaggedType[] { typeof(IEcho) },
                                     ActorBindingFlags.StopThenCloseChannel)
                    },
                    // Tcp
                    ListenEndPoint = new IPEndPoint(IPAddress.Parse(ip), port),
                    TcpConnectionSettings = new TcpConnectionSettings { PacketSerializer = serializer },
                };
                gateway = system.ActorOf(Props.Create(() => new TcpGateway(initiator))).Cast<GatewayRef>();
            }
            else if (channelType == SessionChannelType.TypeName)
            {
                var initiator = new SessionGatewayInitiator
                {
                    // Common
                    GatewayLogger = LogManager.GetLogger($"Gateway(channelType)"),
                    CreateChannelLogger = (ep, _) => LogManager.GetLogger($"Channel({ep})"),
                    PacketSerializer = serializer,
                    CreateInitialActors = (context, connection) => new[]
                    {
                        Tuple.Create(context.ActorOf(Props.Create(() => new EchoActor())),
                                     new TaggedType[] { typeof(IEcho) },
                                     ActorBindingFlags.StopThenCloseChannel)
                    },
                    // Session
                    ListenEndPoint = new IPEndPoint(IPAddress.Parse(ip), port),
                    TcpConnectionSettings = new TcpConnectionSettings { PacketSerializer = serializer },
                };
                gateway = system.ActorOf(Props.Create(() => new SessionGateway(initiator))).Cast<GatewayRef>();
            }
            gateway.Start().Wait();
            return gateway;
        }

        private static void ShowStat(object state)
        {
            var count = Interlocked.Exchange(ref EchoActor.EchoCount, 0);
            Console.WriteLine($"EchoCount={count}");
        }

        public static T LoadConfig<T>(string path) where T : class
        {
            try
            {
                using (StreamReader r = new StreamReader(path))
                {
                    return JsonConvert.DeserializeObject<T>(r.ReadToEnd());
                }
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
