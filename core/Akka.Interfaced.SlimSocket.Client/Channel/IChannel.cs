﻿using System;
using System.Threading.Tasks;

namespace Akka.Interfaced.SlimSocket.Client
{
    public interface IChannel : IRequestWaiter
    {
        ChannelStateType State { get; }

        event Action<IChannel, ChannelStateType> StateChanged;

        Func<IChannel, string, IChannel> ChannelRouter { get; set; }

        Task<bool> ConnectAsync();

        void Close();

        void Update(TimeSpan span);

        TRef CreateRef<TRef>(int actorId = 1)
            where TRef : InterfacedActorRef, new();

        TObserver CreateObserver<TObserver>(TObserver observer, bool startPending = false)
            where TObserver : IInterfacedObserver;

        void RemoveObserver<TObserver>(TObserver observer)
            where TObserver : IInterfacedObserver;
    }
}
