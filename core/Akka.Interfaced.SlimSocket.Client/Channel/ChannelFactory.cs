using Common.Logging;
using System;
using System.Collections.Generic;

namespace Akka.Interfaced.SlimSocket.Client
{
    public class ChannelFactory
    {
        public Func<ILog> CreateChannelLogger { get; set; }
        public ISlimTaskFactory TaskFactory { get; set; }
        public Func<IObserverRegistry> CreateObserverRegistry { get; set; }
        public Func<IChannel, string, IChannel> ChannelRouter { get; set; }
        public IPacketSerializer PacketSerializer { get; set; }

        private List<IClientChannelType> _channelTypes = new List<IClientChannelType>();

        public ChannelFactory()
        {
            TaskFactory = new SlimTaskFactory();
        }

        public void RegisterChannelType(IClientChannelType channelType)
        {
            _channelTypes.Add(channelType);
        }

        public void UnregisterChannelType(string name)
        {
            _channelTypes.RemoveAll((ct) => ct.Name == name);
        }

        public IChannel CreateByType(IClientChannelType channelType)
        {
            var channelLogger = CreateChannelLogger();
            var channel = channelType.CreateChannel(null, channelLogger, PacketSerializer);
            if (channel != null)
            {
                InitializeChannel(channel);
                return channel;
            }

            return null;
        }

        public IChannel CreateByType(string channelTypeName)
        {
            var channelLogger = CreateChannelLogger();
            foreach (var channelType in _channelTypes)
            {
                if (channelType.Name == channelTypeName)
                {
                    var channel = channelType.CreateChannel(null, channelLogger, PacketSerializer);
                    if (channel != null)
                    {
                        InitializeChannel(channel);
                        return channel;
                    }
                }
            }

            return null;
        }

        public IChannel CreateByAddress(string address)
        {
            var channelLogger = CreateChannelLogger();
            foreach (var channelType in _channelTypes)
            {
                var channel = channelType.CreateChannel(address, channelLogger, PacketSerializer);
                if (channel != null)
                {
                    InitializeChannel(channel);
                    return channel;
                }
            }

            return null;
        }

        private void InitializeChannel(ChannelBase channel)
        {
            channel.TaskFactory = TaskFactory;
            channel.ObserverRegistry = CreateObserverRegistry?.Invoke();
            channel.ChannelRouter = ChannelRouter;
        }
    }
}
