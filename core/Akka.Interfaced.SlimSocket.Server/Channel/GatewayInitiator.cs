using System;
using System.Net;
using Akka.Actor;
using Akka.Interfaced.SlimServer;
using Common.Logging;

namespace Akka.Interfaced.SlimSocket.Server
{
    public abstract class GatewayInitiator
    {
        public ILog GatewayLogger { get; set; }
        public bool TokenRequired { get; set; }
        public TimeSpan TokenTimeout { get; set; }
        public Action<IActorRef> GatewayInitialized { get; set; }
        public Func<EndPoint, object, ILog> CreateChannelLogger { get; set; }
        public Func<EndPoint, object, bool> CheckCreateChannel { get; set; }
        public IPacketSerializer PacketSerializer { get; set; }
        public Func<IActorContext, object, Tuple<IActorRef, TaggedType[], ActorBindingFlags>[]> CreateInitialActors { get; set; }

        public GatewayInitiator()
        {
            TokenTimeout = TimeSpan.FromSeconds(30);
        }

        public abstract IPEndPoint GetRemoteEndPoint(object obj);
    }
}
