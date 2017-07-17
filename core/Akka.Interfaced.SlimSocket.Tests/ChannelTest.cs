using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Interfaced.SlimServer;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Interfaced.SlimSocket
{
    public class ChannelTest : TestKit.Xunit2.TestKit
    {
        private static ChannelTest _current;

        private static readonly IPEndPoint s_lastTestEndPoint = new IPEndPoint(IPAddress.Loopback, 5060);
        private static int s_lastTestPort = 5060;
        private static readonly string s_lastConnectTestUri = "ws://localhost:{0}/ws/";
        private static readonly string s_lastListenTestUri = "http://+:{0}/ws/";

        private static readonly TimeSpan _sec1 = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan _sec5 = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan _sec10 = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan _sec20 = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan _sec30 = TimeSpan.FromSeconds(30);

        private readonly XunitOutputLogger.Source _outputSource;
        private readonly IPEndPoint _testEndPoint;
        private readonly string _testConnectUri0;
        private readonly string _testConnectUri1;
        private readonly string _testListenUri0;
        private readonly string _testListenUri1;
        private readonly EntryActorEnvironment _environment;

        public static class NetAclChecker
        {
            public static void AddAddress(string address)
            {
                AddAddress(address, Environment.UserDomainName, Environment.UserName);
            }

            public static void AddAddress(string address, string domain, string user)
            {
                string args = string.Format(@"http add urlacl url={0} user={1}\{2}", address, domain, user);

                ProcessStartInfo psi = new ProcessStartInfo("netsh", args);
                psi.Verb = "runas";
                psi.CreateNoWindow = true;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                psi.UseShellExecute = true;

                Process.Start(psi).WaitForExit();
            }

            public static void ShowAddress(string address)
            {
                string args = string.Format(@"show urlacl url={0}", address);

                ProcessStartInfo psi = new ProcessStartInfo("netsh", args);
                psi.Verb = "runas";
                psi.CreateNoWindow = true;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                psi.UseShellExecute = true;

                Process.Start(psi).WaitForExit();
            }
        }

        public ChannelTest(ITestOutputHelper output)
            : base(output: output)
        {
            _current = this;

            _outputSource = new XunitOutputLogger.Source { Output = output, Lock = new object(), Active = true };

            _testEndPoint = s_lastTestEndPoint;
            s_lastTestEndPoint.Port += 2;

            _testConnectUri0 = string.Format(s_lastConnectTestUri, s_lastTestPort);
            _testConnectUri1 = string.Format(s_lastConnectTestUri, s_lastTestPort + 1);
            _testListenUri0 = string.Format(s_lastListenTestUri, s_lastTestPort);
            _testListenUri1 = string.Format(s_lastListenTestUri, s_lastTestPort + 1);
            s_lastTestPort += 2;

            _environment = new EntryActorEnvironment();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                lock (_outputSource.Lock)
                    _outputSource.Active = false;
            }

            base.Dispose(disposing);
        }

        [Theory]
        [InlineData(ChannelType.Tcp)]
        [InlineData(ChannelType.Udp)]
        [InlineData(ChannelType.WebSocket)]
        [InlineData(ChannelType.Session)]
        public async Task SlimClientConnectToSlimServer(ChannelType type)
        {
            // Arrange
            var gateway = CreatePrimaryGateway(type);
            var clientChannel = await CreatePrimaryClientChannelAsync(type);
            var entry = clientChannel.CreateRef<EntryRef>();

            // Act
            var reply = await entry.WithTimeout(_sec1).Echo("Test");

            // Assert
            Assert.Equal("Test", reply);
        }

        [Theory]
        [InlineData(ChannelType.Tcp)]
        [InlineData(ChannelType.Udp)]
        [InlineData(ChannelType.WebSocket)]
        [InlineData(ChannelType.Session)]
        public async Task SlimClientFailedToConnectToSlimServer(ChannelType type)
        {
            // Act
            var exception = await Record.ExceptionAsync(() => CreatePrimaryClientChannelAsync(type));

            // Assert
            Assert.NotNull(exception);
        }

        [Theory]
        [InlineData(ChannelType.Tcp)]
        [InlineData(ChannelType.Udp)]
        [InlineData(ChannelType.WebSocket)]
        [InlineData(ChannelType.Session)]
        public async Task SlimClientFailedToRequestWithClosedChannel(ChannelType type)
        {
            // Arrange
            var clientChannel = await CreatePrimaryClientChannelAsync(type, false);
            var entry = clientChannel.CreateRef<EntryRef>();

            // Act
            var exception = await Record.ExceptionAsync(() => entry.WithTimeout(_sec1).Echo("Test"));

            // Assert
            Assert.IsType<RequestChannelException>(exception);
        }

        [Theory]
        [InlineData(ChannelType.Tcp)]
        [InlineData(ChannelType.Udp)]
        [InlineData(ChannelType.WebSocket)]
        [InlineData(ChannelType.Session)]
        public async Task SlimClientGetExceptionOfPendingRequestAfterChannelClosed(ChannelType type)
        {
            // Arrange
            var gateway = CreatePrimaryGateway(type);
            var clientChannel = await CreatePrimaryClientChannelAsync(type);
            var entry = clientChannel.CreateRef<EntryRef>();

            // Act
            var exception = await Record.ExceptionAsync(() => entry.WithTimeout(_sec10).Echo("Close"));

            // Assert
            Assert.IsType<RequestChannelException>(exception);
        }

        [Theory]
        [InlineData(ChannelType.Tcp)]
        [InlineData(ChannelType.Udp)]
        [InlineData(ChannelType.WebSocket)]
        [InlineData(ChannelType.Session)]
        public async Task SlimClientGetSecondBoundActor(ChannelType type)
        {
            // Arrange
            var gateway = CreatePrimaryGateway(type);
            var clientChannel = await CreatePrimaryClientChannelAsync(type);
            var entry = clientChannel.CreateRef<EntryRef>();

            // Act
            var greeter = await entry.GetGreeter();
            var reply = await greeter.Greet("World");
            var count = await greeter.GetCount();

            // Assert
            Assert.Equal("Hello World!", reply);
            Assert.Equal(1, count);
        }

        [Theory]
        [InlineData(ChannelType.Tcp)]
        [InlineData(ChannelType.Udp)]
        [InlineData(ChannelType.WebSocket)]
        [InlineData(ChannelType.Session)]
        public async Task SlimClientGetsNotificationMessages(ChannelType type)
        {
            // Arrange
            var gateway = CreatePrimaryGateway(type);
            var clientChannel = await CreatePrimaryClientChannelAsync(type);
            var entry = clientChannel.CreateRef<EntryRef>();

            // Act
            var greeter = await entry.WithTimeout(_sec1).GetGreeter();
            var greetObserver = new TestGreetObserver();
            var observer = clientChannel.CreateObserver<IGreetObserver>(greetObserver);
            await ((GreeterWithObserverRef)greeter).WithTimeout(_sec1).Subscribe(observer);
            await ((GreeterWithObserverRef)greeter).WithTimeout(_sec1).Greet("World");
            await ((GreeterWithObserverRef)greeter).WithTimeout(_sec1).Greet("Actor");
            await ((GreeterWithObserverRef)greeter).WithTimeout(_sec1).Unsubscribe(observer);
            clientChannel.RemoveObserver(observer);
            await ((GreeterWithObserverRef)greeter).WithTimeout(_sec1).Greet("Akka");

            // Assert
            Assert.Equal(new[] { "Greet(World)", "Greet(Actor)" }, greetObserver.Logs);
        }

        [Theory]
        [InlineData(ChannelType.Tcp)]
        [InlineData(ChannelType.Udp)]
        [InlineData(ChannelType.WebSocket)]
        [InlineData(ChannelType.Session)]
        public async Task SlimClientConnectToSlimServerWithToken(ChannelType type)
        {
            // Arrange
            var gateway = CreatePrimaryGateway(type);
            var gateway2nd = CreateSecondaryGateway(type);
            var clientChannel = await CreatePrimaryClientChannelAsync(type);
            var entry = clientChannel.CreateRef<EntryRef>();

            // Act
            var greeter = await entry.WithTimeout(_sec1).GetGreeterOnAnotherChannel();
            var reply = await ((GreeterWithObserverRef)greeter).WithTimeout(_sec1).Greet("World");
            var count = await ((GreeterWithObserverRef)greeter).WithTimeout(_sec1).GetCount();

            // Assert
            Assert.Equal("Hello World!", reply);
            Assert.Equal(1, count);
        }

        [Theory]
        [InlineData(ChannelType.Tcp)]
        [InlineData(ChannelType.Udp)]
        [InlineData(ChannelType.WebSocket)]
        [InlineData(ChannelType.Session)]
        public async Task SlimClientConnectToSlimServerWithToken_Timeout(ChannelType type)
        {
            // Arrange
            var gateway = CreatePrimaryGateway(type);
            var gateway2nd = CreateSecondaryGateway(type, i => { i.TokenTimeout = TimeSpan.FromSeconds(0.1); });
            var clientChannel = await CreatePrimaryClientChannelAsync(type);
            var entry = clientChannel.CreateRef<EntryRef>();
            clientChannel.ChannelRouter = null;

            // Act
            var greeter = await entry.GetGreeterOnAnotherChannel();
            await Task.Delay(_sec1);
            var greeterTarget = (BoundActorTarget)(((GreeterWithObserverRef)greeter).Target);
            var exception = await Record.ExceptionAsync(() => CreateSecondaryClientChannelAsync(greeterTarget.Address));

            // Assert
            Assert.NotNull(exception);
        }

        [Theory]
        [InlineData(ChannelType.Tcp)]
        [InlineData(ChannelType.Udp)]
        [InlineData(ChannelType.WebSocket)]
        [InlineData(ChannelType.Session)]
        public async Task CloseChannel_ChannelClosed(ChannelType type)
        {
            // Arrange
            var gateway = CreatePrimaryGateway(type);
            var clientChannel = await CreatePrimaryClientChannelAsync(type);
            var entry = clientChannel.CreateRef<EntryRef>();
            Assert.Equal("Test", await entry.WithTimeout(_sec1).Echo("Test"));
            var serverChannel = (await ActorSelection(gateway.CastToIActorRef().Path + "/*").ResolveOne(_sec1)).Cast<ActorBoundChannelRef>();

            // Act
            await serverChannel.Close();

            // Assert
            Watch(serverChannel.CastToIActorRef());
            ExpectTerminated(serverChannel.CastToIActorRef());
        }

        [Theory]
        [InlineData(ChannelType.Tcp, 0)]
        [InlineData(ChannelType.Tcp, 2)]
        [InlineData(ChannelType.Udp, 0)]
        [InlineData(ChannelType.Udp, 2)]
        [InlineData(ChannelType.WebSocket, 0)]
        [InlineData(ChannelType.WebSocket, 2)]
        [InlineData(ChannelType.Session, 0)]
        [InlineData(ChannelType.Session, 2)]
        public async Task GatewayStop_AllChannelClosed_ThenStop(ChannelType type, int clientCount)
        {
            // Arrange
            var gateway = CreatePrimaryGateway(type);
            for (int i = 0; i < clientCount; i++)
            {
                var clientChannel = await CreatePrimaryClientChannelAsync(type);
                var entry = clientChannel.CreateRef<EntryRef>();
                Assert.Equal("Test:" + i, await entry.WithTimeout(_sec1).Echo("Test:" + i));
            }

            // Act
            await gateway.WithTimeout(_sec5).Stop();

            // Assert
            Watch(gateway.CastToIActorRef());
            ExpectTerminated(gateway.CastToIActorRef(), _sec10);
        }

        [Theory]
        [InlineData(ChannelType.Tcp)]
        [InlineData(ChannelType.Udp)]
        [InlineData(ChannelType.WebSocket)]
        [InlineData(ChannelType.Session)]
        public async Task GatewayStopListen_StopListeningAndKeepActiveConnections(ChannelType type)
        {
            // Arrange
            var gateway = CreatePrimaryGateway(type);
            var clientChannel = await CreatePrimaryClientChannelAsync(type);
            var entry = clientChannel.CreateRef<EntryRef>();
            Assert.Equal("Test", await entry.WithTimeout(_sec1).Echo("Test"));
            var serverChannel = (await ActorSelection(gateway.CastToIActorRef().Path + "/*").ResolveOne(_sec1)).Cast<ActorBoundChannelRef>();

            // Act & Assert (Stop listening and further channels cannot be established)
            await gateway.WithTimeout(_sec5).Stop(true);
            var exception = await Record.ExceptionAsync(() => CreatePrimaryClientChannelAsync(type));
            Assert.NotNull(exception);
            Assert.Equal("Test2", await entry.WithTimeout(_sec1).Echo("Test2"));

            // Act & Assert (Stop all and all channels are closed)
            await gateway.WithTimeout(_sec5).Stop();
            Watch(serverChannel.CastToIActorRef());
            ExpectTerminated(serverChannel.CastToIActorRef(), _sec10);
        }

        [Fact]
        public async Task CloseServerSessionLine_ThenLineRebound()
        {
            // Arrange
            var gateway = CreatePrimaryGateway(ChannelType.Session);
            var clientChannel = await CreatePrimaryClientChannelAsync(ChannelType.Session);
            var entry = clientChannel.CreateRef<EntryRef>();

            // Act & Assert
            Assert.Equal("CloseSessionLine", await entry.WithTimeout(_sec20).Echo("CloseSessionLine"));
        }

        [Fact]
        public async Task CloseServerSessionChannelGracefully_ThenClientClosedGracefully()
        {
            // Arrange
            var gateway = CreatePrimaryGateway(ChannelType.Session);
            var clientChannel = await CreatePrimaryClientChannelAsync(ChannelType.Session);
            var sessionClientChannel = clientChannel as Client.SessionChannel;
            var entry = clientChannel.CreateRef<EntryRef>();

            // Act & Assert
            var sessionCloseStates = new List<SessionCloseState>();
            sessionClientChannel.CloseStateChanged += (channel, state) => { sessionCloseStates.Add(state); };
            var exception = await Record.ExceptionAsync(() => entry.WithTimeout(_sec10).Echo("CloseSessionGracefully"));
            Assert.True(
                sessionCloseStates.Count == 3 &&
                sessionCloseStates[0] == SessionCloseState.CloseWait &&
                sessionCloseStates[1] == SessionCloseState.LastAck &&
                sessionCloseStates[2] == SessionCloseState.Closed);
        }

        [Fact]
        public async Task CloseClientSessionChannelGracefully_ThenServerClosedGracefully()
        {
            // Arrange
            var gateway = CreatePrimaryGateway(ChannelType.Session);
            var clientChannel = await CreatePrimaryClientChannelAsync(ChannelType.Session);
            var sessionClientChannel = clientChannel as Client.SessionChannel;
            var entry = clientChannel.CreateRef<EntryRef>();

            // Act
            var sessionCloseStates = new List<SessionCloseState>();
            sessionClientChannel.CloseStateChanged += (channel, state) => { sessionCloseStates.Add(state); };
            sessionClientChannel.Close();
            while (sessionClientChannel.State != Client.ChannelStateType.Closed)
            {
                await Task.Delay(10);
            }

            // Assert
            Assert.True(
                sessionCloseStates.Count == 4 &&
                sessionCloseStates[0] == SessionCloseState.FinWait1 &&
                sessionCloseStates[1] == SessionCloseState.FinWait2 &&
                sessionCloseStates[2] == SessionCloseState.TimeWait &&
                sessionCloseStates[3] == SessionCloseState.Closed);
        }

        [Fact]
        public async Task CheckClientSessionChannelSrttUpdated()
        {
            // Arrange
            var gateway = CreatePrimaryGateway(ChannelType.Session);
            var clientChannel = await CreatePrimaryClientChannelAsync(ChannelType.Session);
            var sessionClientChannel = clientChannel as Client.SessionChannel;
            var entry = clientChannel.CreateRef<EntryRef>();

            // Act
            for (int i = 0; i < 100; ++i)
            {
                if (sessionClientChannel.SmoothedRoundTripTicks.HasValue)
                {
                    break;
                }
                await Task.Delay(10);
            }

            // Assert
            Assert.True(sessionClientChannel.SmoothedRoundTripTicks.HasValue);
        }

        private Server.GatewayRef CreatePrimaryGateway(ChannelType type, Action<Server.GatewayInitiator> initiatorSetup = null)
        {
            if (type == ChannelType.WebSocket)
                NetAclChecker.AddAddress(_testListenUri0);

            return ChannelHelper.CreateGateway(Sys, type, "1", _testEndPoint, _testListenUri0, _testConnectUri0, _outputSource, initiator =>
            {
                initiator.GatewayInitialized = a => { _environment.Gateway = a.Cast<ActorBoundGatewayRef>(); };
                initiator.CreateInitialActors = (IActorContext context, object socket) => new[]
                {
                    Tuple.Create(
                        context.ActorOf(Props.Create(() => new EntryActor(_environment, context.Self.Cast<ActorBoundChannelRef>()))),
                        new TaggedType[] { typeof(IEntry) },
                        (ActorBindingFlags)0)
                };

                initiatorSetup?.Invoke(initiator);
            });
        }

        private Server.GatewayRef CreateSecondaryGateway(ChannelType type, Action<Server.GatewayInitiator> initiatorSetup = null)
        {
            if (type == ChannelType.WebSocket)
                NetAclChecker.AddAddress(_testListenUri1);

            return ChannelHelper.CreateGateway(Sys, type, "2", new IPEndPoint(_testEndPoint.Address, _testEndPoint.Port + 1),
                                               _testListenUri1, _testConnectUri1, _outputSource,
            initiator =>
            {
                initiator.TokenRequired = true;
                initiator.GatewayInitialized = a => { _environment.Gateway2nd = a.Cast<ActorBoundGatewayRef>(); };

                initiatorSetup?.Invoke(initiator);
            });
        }

        private async Task<Client.IChannel> CreatePrimaryClientChannelAsync(ChannelType type, bool connected = true)
        {
            var channel = ChannelHelper.CreateClientChannel("1", type, _testEndPoint, _testConnectUri0, _outputSource);

            channel.ChannelRouter = (_, address) =>
            {
                try
                {
                    return CreateSecondaryClientChannelAsync(address, true).Result;
                }
                catch (Exception)
                {
                    return null;
                }
            };

            if (connected)
                await channel.ConnectAsync();

            return channel;
        }

        private async Task<Client.IChannel> CreateSecondaryClientChannelAsync(string address, bool connected = true)
        {
            var channel = ChannelHelper.CreateClientChannel("2", address, _outputSource);

            if (connected)
                await channel.ConnectAsync();

            return channel;
        }

        // Close method 확인
        // Stop(listenonly) 확인
    }
}
