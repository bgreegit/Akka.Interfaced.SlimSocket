using Akka.Interfaced.SlimSocket.Client;
using Akka.Interfaced.SlimSocket.Client.SessionChannel;
using Akka.Interfaced.SlimSocket.Client.TcpChannel;
using Akka.Interfaced.SlimSocket.Client.UdpChannel;
using Akka.Interfaced.SlimSocket.Client.WebSocketChannel;
using HelloWorld.Interface;
using System;
using System.Net;
using System.Threading.Tasks;

namespace HelloWorld.Program.Client
{
    internal class TestDriver : IGreetObserver
    {
        public async Task Run(IChannel channel)
        {
            await channel.ConnectAsync();

            // get HelloWorld from Entry

            var entry = channel.CreateRef<EntryRef>(1);
            var greeter = await entry.GetGreeter();
            if (greeter == null)
            {
                throw new InvalidOperationException("Cannot obtain GreetingActor");
            }

            // add observer

            var observer = channel.CreateObserver<IGreetObserver>(this);
            await greeter.Subscribe(observer);

            // make some noise

            Console.WriteLine(await greeter.Greet("World"));
            Console.WriteLine(await greeter.Greet("Actor"));
            Console.WriteLine(await greeter.GetCount());

            await greeter.Unsubscribe(observer);
            channel.RemoveObserver(observer);
            channel.Close();
        }

        void IGreetObserver.Event(string message)
        {
            Console.WriteLine($"<- {message}");
        }
    }

    internal class Program
    {
        private static void Main(string[] args)
        {
            var port = 5001;
            var channelFactory = new ChannelFactory
            {
                CreateChannelLogger = () => null,
                CreateObserverRegistry = () => new ObserverRegistry(),
                PacketSerializer = PacketSerializer.CreatePacketSerializer()
            };
            var driver = new TestDriver();

            // TCP
            var tcpChannelType = new TcpClientChannelType()
            {
                ConnectEndPoint = new IPEndPoint(IPAddress.Loopback, port),
            };
            driver.Run(channelFactory.CreateByType(tcpChannelType)).Wait();

            // UDP
            var udpChannelType = new UdpClientChannelType()
            {
                ConnectEndPoint = new IPEndPoint(IPAddress.Loopback, port),
            };
            driver.Run(channelFactory.CreateByType(udpChannelType)).Wait();

            // Session
            var sessionChannelType = new SessionClientChannelType()
            {
                ConnectEndPoint = new IPEndPoint(IPAddress.Loopback, port + 1),
            };
            driver.Run(channelFactory.CreateByType(sessionChannelType)).Wait();

            // WebSocket
            var webSocketChannelType = new WebSocketClientChannelType()
            {
                ConnectUri = string.Format("ws://localhost:{0}/ws/", port + 2),
            };
            driver.Run(channelFactory.CreateByType(webSocketChannelType)).Wait();
        }
    }
}
