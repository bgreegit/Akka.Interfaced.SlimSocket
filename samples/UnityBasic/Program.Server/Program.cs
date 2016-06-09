﻿using System;
using System.Net;
using System.Net.Sockets;
using Akka.Actor;
using Akka.Interfaced;
using Akka.Interfaced.SlimSocket.Base;
using Akka.Interfaced.SlimSocket.Server;
using Common.Logging;
using ProtoBuf.Meta;
using TypeAlias;
using UnityBasic.Interface;

namespace UnityBasic.Program.Server
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            if (typeof(ICalculator) == null)
                throw new Exception("Force interface module to be loaded");

            var system = ActorSystem.Create("MySystem");
            DeadRequestProcessingActor.Install(system);

            StartListen(system, 5000);

            Console.WriteLine("Please enter key to quit.");
            Console.ReadLine();
        }

        private static TcpConnectionSettings _tcpConnectionSettings;

        private static void StartListen(ActorSystem system, int port)
        {
            var logger = LogManager.GetLogger("ClientGateway");

            _tcpConnectionSettings = new TcpConnectionSettings
            {
                PacketSerializer = new PacketSerializer(
                    new PacketSerializerBase.Data(
                        new ProtoBufMessageSerializer(PacketSerializer.CreateTypeModel()),
                        new TypeAliasTable()))
            };

            var clientGateway = system.ActorOf(Props.Create(() => new ClientGateway(logger, CreateSession)));
            clientGateway.Tell(new ClientGatewayMessage.Start(new IPEndPoint(IPAddress.Any, port)));
        }

        private static IActorRef CreateSession(IActorContext context, Socket socket)
        {
            var logger = LogManager.GetLogger($"Client({socket.RemoteEndPoint})");
            return context.ActorOf(Props.Create(() => new ClientSession(
                logger, socket, _tcpConnectionSettings, CreateInitialActor)));
        }

        private static Tuple<IActorRef, ActorBoundSessionMessage.InterfaceType[]>[] CreateInitialActor(IActorContext context, Socket socket) =>
            new[]
            {
                Tuple.Create(context.ActorOf(Props.Create(() => new EntryActor(context.Self))),
                              new[] { new ActorBoundSessionMessage.InterfaceType(typeof(IEntry)) })
            };
    }
}
