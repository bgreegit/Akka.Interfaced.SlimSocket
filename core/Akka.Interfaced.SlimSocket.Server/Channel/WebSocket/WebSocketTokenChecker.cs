﻿using System;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using Akka.Actor;
using Akka.Event;
using Common.Logging;

namespace Akka.Interfaced.SlimSocket.Server
{
    public class WebSocketTokenChecker : UntypedActor
    {
        private GatewayInitiator _initiator;
        private WebSocketGateway _gateway;
        private ILog _logger;
        private IActorRef _self;
        private EventStream _eventStream;
        private AcceptedWebSocket _acceptedWebSocket;
        private WebSocketConnection _connection;
        private ICancelable _timeoutCanceler;

        public WebSocketTokenChecker(GatewayInitiator initiator, WebSocketGateway gateway, AcceptedWebSocket acceptedWebSocket)
        {
            var aws = acceptedWebSocket;

            _initiator = initiator;
            _gateway = gateway;
            _logger = initiator.CreateChannelLogger(aws.RemoteEndPoint, aws);
            _acceptedWebSocket = aws;
            _connection = new WebSocketConnection(_logger, aws.WebSocket, aws.LocalEndpoint, aws.RemoteEndPoint) { Settings = initiator.WebSocketConnectionSettings };
        }

        protected override void PreStart()
        {
            base.PreStart();

            _self = Self;
            _eventStream = Context.System.EventStream;

            _connection.Closed += OnConnectionClose;
            _connection.Received += OnConnectionReceive;
            _connection.Open();

            if (_initiator.TokenTimeout != TimeSpan.Zero)
            {
                _timeoutCanceler = Context.System.Scheduler.ScheduleTellOnceCancelable(
                    _initiator.TokenTimeout, Self, PoisonPill.Instance, Self);
            }
        }

        protected override void PostStop()
        {
            if (_connection != null)
            {
                _connection.Close();
            }

            if (_timeoutCanceler != null)
            {
                _timeoutCanceler.Cancel();
            }

            base.PostStop();
        }

        protected override void OnReceive(object message)
        {
            Unhandled(message);
        }

        protected void OnConnectionClose(WebSocketConnection connection, int reason)
        {
            _self.Tell(PoisonPill.Instance);
        }

        protected void OnConnectionReceive(WebSocketConnection connection, object packet)
        {
            _connection.Closed -= OnConnectionClose;
            _connection.Received -= OnConnectionReceive;

            try
            {
                // Before token is validated

                var p = packet as Packet;
                if (p == null)
                {
                    _eventStream.Publish(new Warning(
                        _self.Path.ToString(), GetType(),
                        $"Receives null packet from {_connection?.RemoteEndPoint}"));
                    return;
                }

                if (p.Type != PacketType.System || (p.Message is string) == false)
                {
                    _eventStream.Publish(new Warning(
                        _self.Path.ToString(), GetType(),
                        $"Receives a bad packet without a message from {_connection?.RemoteEndPoint}"));
                    return;
                }

                // Try to open

                var token = (string)p.Message;
                var succeeded = _gateway.EstablishChannel(token, _connection);
                if (succeeded)
                {
                    // set null to avoid closing connection in PostStop
                    _connection = null;
                }
                else
                {
                    _eventStream.Publish(new Warning(
                        _self.Path.ToString(), GetType(),
                        $"Receives wrong token({token}) from {_connection?.RemoteEndPoint}"));
                    return;
                }
            }
            finally
            {
                // This actor will be destroyed anyway after this call.
                _self.Tell(PoisonPill.Instance);
            }
        }
    }
}
