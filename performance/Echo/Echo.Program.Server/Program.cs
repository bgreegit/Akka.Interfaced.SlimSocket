using Akka.Actor;
using Akka.Interfaced;
using Akka.Interfaced.SlimServer;
using Akka.Interfaced.SlimSocket;
using Akka.Interfaced.SlimSocket.Server;
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
                var channelType = (ChannelType)Enum.Parse(typeof(ChannelType), config.ChannelType, true);
                var sessionGateway = StartGateway(system, channelType, config.Ip, config.Port);

                Console.WriteLine("Please enter key to quit.");
                Console.ReadLine();

                sessionGateway.Stop().Wait();
            }
        }

        private static GatewayRef StartGateway(ActorSystem system, ChannelType type, string ip, int port)
        {
            var serializer = PacketSerializer.CreatePacketSerializer();

            var initiator = new GatewayInitiator
            {
                ListenEndPoint = new IPEndPoint(IPAddress.Parse(ip), port),
                ListenUri = string.Format("http://+:{0}/ws/", port),
                GatewayLogger = LogManager.GetLogger("Gateway"),
                CreateChannelLogger = (ep, _) => LogManager.GetLogger($"Channel({ep})"),
                SessionSettings = new SessionSettings(),
                TcpConnectionSettings = new TcpConnectionSettings { PacketSerializer = serializer },
                WebSocketConnectionSettings = new WebSocketConnectionSettings { PacketSerializer = serializer },
                PacketSerializer = serializer,
                CreateInitialActors = (context, connection) => new[]
                {
                    Tuple.Create(context.ActorOf(Props.Create(() => new EchoActor())),
                                 new TaggedType[] { typeof(IEcho) },
                                 ActorBindingFlags.StopThenCloseChannel)
                }
            };

            GatewayRef gateway = null;
            if (type == ChannelType.Tcp)
            {
                gateway = system.ActorOf(Props.Create(() => new TcpGateway(initiator))).Cast<GatewayRef>();
            }
            else if (type == ChannelType.Session)
            {
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
