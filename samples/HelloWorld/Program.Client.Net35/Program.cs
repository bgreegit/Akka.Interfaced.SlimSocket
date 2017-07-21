using Akka.Interfaced.SlimSocket.Client;
using Akka.Interfaced.SlimSocket.Client.SessionChannel;
using Akka.Interfaced.SlimSocket.Client.TcpChannel;
using Akka.Interfaced.SlimSocket.Client.UdpChannel;
using Akka.Interfaced.SlimSocket.Client.WebSocketChannel;
using HelloWorld.Interface;
using System;
using System.Net;

namespace HelloWorld.Program.Client
{
    internal class TestDriver : IGreetObserver
    {
        public void Run(IChannel channel)
        {
            var connected = channel.ConnectAsync().Result;

            // get HelloWorld from Entry

            var entry = channel.CreateRef<EntryRef>(1);
            var greeter = entry.GetGreeter().Result;
            if (greeter == null)
            {
                throw new InvalidOperationException("Cannot obtain GreetingActor");
            }

            // add observer

            var observer = channel.CreateObserver<IGreetObserver>(this);
            greeter.Subscribe(observer);

            // make some noise

            Console.WriteLine(greeter.Greet("World").Result);
            Console.WriteLine(greeter.Greet("Actor").Result);
            Console.WriteLine(greeter.GetCount().Result);

            greeter.Unsubscribe(observer);
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

            // TCP
            var driver = new TestDriver();
            var tcpChannelType = new TcpClientChannelType()
            {
                ConnectEndPoint = new IPEndPoint(IPAddress.Loopback, port),
            };
            driver.Run(channelFactory.CreateByType(tcpChannelType));

            // UDP
            var udpChannelType = new UdpClientChannelType()
            {
                ConnectEndPoint = new IPEndPoint(IPAddress.Loopback, port),
            };
            driver.Run(channelFactory.CreateByType(udpChannelType));

            // Session
            var sessionChannelType = new SessionClientChannelType()
            {
                ConnectEndPoint = new IPEndPoint(IPAddress.Loopback, port + 1),
            };
            driver.Run(channelFactory.CreateByType(sessionChannelType));

            // WebSocket
            var webSocketChannelType = new WebSocketClientChannelType()
            {
                ConnectUri = string.Format("ws://localhost:{0}/ws/", port + 2),
            };
            driver.Run(channelFactory.CreateByType(webSocketChannelType));
        }
    }
}
