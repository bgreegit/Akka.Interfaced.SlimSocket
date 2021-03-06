﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Interfaced.SlimServer;
using Xunit;
using Xunit.Abstractions;
using Akka.Interfaced.SlimSocket.Server.WebSocketChannel;
using Akka.Interfaced.SlimSocket.Server.SessionChannel;
using Akka.Interfaced.SlimSocket.Server.TcpChannel;
using Akka.Interfaced.SlimSocket.Server.UdpChannel;

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
                {
                    _outputSource.Active = false;
                }
            }

            base.Dispose(disposing);
        }

        [Theory]
        [InlineData(TcpChannelType.TypeName)]
        [InlineData(UdpChannelType.TypeName)]
        [InlineData(WebSocketChannelType.TypeName)]
        [InlineData(SessionChannelType.TypeName)]
        public async Task SlimClientConnectToSlimServer(string channelType)
        {
            // Arrange
            var gateway = CreatePrimaryGateway(channelType);
            var clientChannel = await CreatePrimaryClientChannelAsync(channelType);
            var entry = clientChannel.CreateRef<EntryRef>();

            // Act
            var reply = await entry.WithTimeout(_sec1).Echo("Test");

            // Assert
            Assert.Equal("Test", reply);
        }

        [Theory]
        [InlineData(TcpChannelType.TypeName)]
        [InlineData(UdpChannelType.TypeName)]
        [InlineData(WebSocketChannelType.TypeName)]
        [InlineData(SessionChannelType.TypeName)]
        public async Task SlimClientFailedToConnectToSlimServer(string channelType)
        {
            // Act
            var exception = await Record.ExceptionAsync(() => CreatePrimaryClientChannelAsync(channelType));

            // Assert
            Assert.NotNull(exception);
        }

        [Theory]
        [InlineData(TcpChannelType.TypeName)]
        [InlineData(UdpChannelType.TypeName)]
        [InlineData(WebSocketChannelType.TypeName)]
        [InlineData(SessionChannelType.TypeName)]
        public async Task SlimClientFailedToRequestWithClosedChannel(string channelType)
        {
            // Arrange
            var clientChannel = await CreatePrimaryClientChannelAsync(channelType, false);
            var entry = clientChannel.CreateRef<EntryRef>();

            // Act
            var exception = await Record.ExceptionAsync(() => entry.WithTimeout(_sec1).Echo("Test"));

            // Assert
            Assert.IsType<RequestChannelException>(exception);
        }

        [Theory]
        [InlineData(TcpChannelType.TypeName)]
        [InlineData(UdpChannelType.TypeName)]
        [InlineData(WebSocketChannelType.TypeName)]
        [InlineData(SessionChannelType.TypeName)]
        public async Task SlimClientGetExceptionOfPendingRequestAfterChannelClosed(string channelType)
        {
            // Arrange
            var gateway = CreatePrimaryGateway(channelType);
            var clientChannel = await CreatePrimaryClientChannelAsync(channelType);
            var entry = clientChannel.CreateRef<EntryRef>();

            // Act
            var exception = await Record.ExceptionAsync(() => entry.WithTimeout(_sec10).Echo("Close"));

            // Assert
            Assert.IsType<RequestChannelException>(exception);
        }

        [Theory]
        [InlineData(TcpChannelType.TypeName)]
        [InlineData(UdpChannelType.TypeName)]
        [InlineData(WebSocketChannelType.TypeName)]
        [InlineData(SessionChannelType.TypeName)]
        public async Task SlimClientGetSecondBoundActor(string channelType)
        {
            // Arrange
            var gateway = CreatePrimaryGateway(channelType);
            var clientChannel = await CreatePrimaryClientChannelAsync(channelType);
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
        [InlineData(TcpChannelType.TypeName)]
        [InlineData(UdpChannelType.TypeName)]
        [InlineData(WebSocketChannelType.TypeName)]
        [InlineData(SessionChannelType.TypeName)]
        public async Task SlimClientGetsNotificationMessages(string channelType)
        {
            // Arrange
            var gateway = CreatePrimaryGateway(channelType);
            var clientChannel = await CreatePrimaryClientChannelAsync(channelType);
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
        [InlineData(TcpChannelType.TypeName)]
        [InlineData(UdpChannelType.TypeName)]
        [InlineData(WebSocketChannelType.TypeName)]
        [InlineData(SessionChannelType.TypeName)]
        public async Task SlimClientConnectToSlimServerWithToken(string channelType)
        {
            // Arrange
            var gateway = CreatePrimaryGateway(channelType);
            var gateway2nd = CreateSecondaryGateway(channelType);
            var clientChannel = await CreatePrimaryClientChannelAsync(channelType);
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
        [InlineData(TcpChannelType.TypeName)]
        [InlineData(UdpChannelType.TypeName)]
        [InlineData(WebSocketChannelType.TypeName)]
        [InlineData(SessionChannelType.TypeName)]
        public async Task SlimClientConnectToSlimServerWithToken_Timeout(string channelType)
        {
            // Arrange
            var gateway = CreatePrimaryGateway(channelType);
            var gateway2nd = CreateSecondaryGateway(channelType, i => { i.TokenTimeout = TimeSpan.FromSeconds(0.1); });
            var clientChannel = await CreatePrimaryClientChannelAsync(channelType);
            var entry = clientChannel.CreateRef<EntryRef>();
            clientChannel.ChannelRouter = null;

            // Act
            var greeter = await entry.GetGreeterOnAnotherChannel();
            await Task.Delay(_sec1);
            var greeterTarget = (BoundActorTarget)(((GreeterWithObserverRef)greeter).Target);
            var exception = await Record.ExceptionAsync(() => CreateSecondaryClientChannelAsync(channelType, greeterTarget.Address));

            // Assert
            Assert.NotNull(exception);
        }

        [Theory]
        [InlineData(TcpChannelType.TypeName)]
        [InlineData(UdpChannelType.TypeName)]
        [InlineData(WebSocketChannelType.TypeName)]
        [InlineData(SessionChannelType.TypeName)]
        public async Task CloseChannel_ChannelClosed(string channelType)
        {
            // Arrange
            var gateway = CreatePrimaryGateway(channelType);
            var clientChannel = await CreatePrimaryClientChannelAsync(channelType);
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
        [InlineData(TcpChannelType.TypeName, 0)]
        [InlineData(TcpChannelType.TypeName, 2)]
        [InlineData(UdpChannelType.TypeName, 0)]
        [InlineData(UdpChannelType.TypeName, 2)]
        [InlineData(WebSocketChannelType.TypeName, 0)]
        [InlineData(WebSocketChannelType.TypeName, 2)]
        [InlineData(SessionChannelType.TypeName, 0)]
        [InlineData(SessionChannelType.TypeName, 2)]
        public async Task GatewayStop_AllChannelClosed_ThenStop(string channelType, int clientCount)
        {
            // Arrange
            var gateway = CreatePrimaryGateway(channelType);
            for (int i = 0; i < clientCount; i++)
            {
                var clientChannel = await CreatePrimaryClientChannelAsync(channelType);
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
        [InlineData(TcpChannelType.TypeName)]
        [InlineData(UdpChannelType.TypeName)]
        [InlineData(WebSocketChannelType.TypeName)]
        [InlineData(SessionChannelType.TypeName)]
        public async Task GatewayStopListen_StopListeningAndKeepActiveConnections(string channelType)
        {
            // Arrange
            var gateway = CreatePrimaryGateway(channelType);
            var clientChannel = await CreatePrimaryClientChannelAsync(channelType);
            var entry = clientChannel.CreateRef<EntryRef>();
            Assert.Equal("Test", await entry.WithTimeout(_sec1).Echo("Test"));
            var serverChannel = (await ActorSelection(gateway.CastToIActorRef().Path + "/*").ResolveOne(_sec1)).Cast<ActorBoundChannelRef>();

            // Act & Assert (Stop listening and further channels cannot be established)
            await gateway.WithTimeout(_sec5).Stop(true);
            var exception = await Record.ExceptionAsync(() => CreatePrimaryClientChannelAsync(channelType));
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
            var gateway = CreatePrimaryGateway(SessionChannelType.TypeName);
            var clientChannel = await CreatePrimaryClientChannelAsync(SessionChannelType.TypeName);
            var entry = clientChannel.CreateRef<EntryRef>();

            // Act & Assert
            Assert.Equal("CloseSessionLine", await entry.WithTimeout(_sec20).Echo("CloseSessionLine"));
        }

        [Fact]
        public async Task CloseServerSessionChannelGracefully_ThenClientClosedGracefully()
        {
            // Arrange
            var gateway = CreatePrimaryGateway(SessionChannelType.TypeName);
            var clientChannel = await CreatePrimaryClientChannelAsync(SessionChannelType.TypeName);
            var sessionClientChannel = clientChannel as Client.SessionChannel.SessionChannel;
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
            var gateway = CreatePrimaryGateway(SessionChannelType.TypeName);
            var clientChannel = await CreatePrimaryClientChannelAsync(SessionChannelType.TypeName);
            var sessionClientChannel = clientChannel as Client.SessionChannel.SessionChannel;
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
            var gateway = CreatePrimaryGateway(SessionChannelType.TypeName);
            var clientChannel = await CreatePrimaryClientChannelAsync(SessionChannelType.TypeName);
            var sessionClientChannel = clientChannel as Client.SessionChannel.SessionChannel;
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

        private Server.GatewayRef CreatePrimaryGateway(string channelType, Action<Server.GatewayInitiator> initiatorSetup = null)
        {
            if (channelType == WebSocketChannelType.TypeName)
            {
                NetAclChecker.AddAddress(_testListenUri0);
            }

            return ChannelHelper.CreateGateway(Sys, channelType, "1", _testEndPoint, _testListenUri0, _testConnectUri0, _outputSource, initiator =>
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

        private Server.GatewayRef CreateSecondaryGateway(string channelType, Action<Server.GatewayInitiator> initiatorSetup = null)
        {
            if (channelType == WebSocketChannelType.TypeName)
            {
                NetAclChecker.AddAddress(_testListenUri1);
            }

            return ChannelHelper.CreateGateway(Sys, channelType, "2", new IPEndPoint(_testEndPoint.Address, _testEndPoint.Port + 1),
                                               _testListenUri1, _testConnectUri1, _outputSource,
            initiator =>
            {
                initiator.TokenRequired = true;
                initiator.GatewayInitialized = a => { _environment.Gateway2nd = a.Cast<ActorBoundGatewayRef>(); };

                initiatorSetup?.Invoke(initiator);
            });
        }

        private async Task<Client.IChannel> CreatePrimaryClientChannelAsync(string channelType, bool connected = true)
        {
            var channel = ChannelHelper.CreateClientChannel("1", channelType, _testEndPoint, _testConnectUri0, _outputSource);

            channel.ChannelRouter = (_, address) =>
            {
                try
                {
                    return CreateSecondaryClientChannelAsync(channelType, address, true).Result;
                }
                catch (Exception)
                {
                    return null;
                }
            };

            if (connected)
            {
                await channel.ConnectAsync();
            }

            return channel;
        }

        private async Task<Client.IChannel> CreateSecondaryClientChannelAsync(string channelType, string address, bool connected = true)
        {
            var channel = ChannelHelper.CreateClientChannel("2", channelType, address, _outputSource);

            if (connected)
            {
                await channel.ConnectAsync();
            }

            return channel;
        }

        // Close method 확인
        // Stop(listenonly) 확인
    }
}
