using System;
using System.Collections.Generic;
using System.Linq;

namespace Akka.Interfaced.SlimSocket.Client
{
    public class Communicator
    {
        public ChannelFactory ChannelFactory { get; }
        public IList<IChannel> Channels { get; }
        public IObserverRegistry ObserverRegistry { get; }

        private DateTime _lastUpdateTime;
        private event Action<TimeSpan> Updated;

        public Communicator()
        {
            ChannelFactory = new ChannelFactory()
            {
                CreateObserverRegistry = () => ObserverRegistry,
                ChannelRouter = OnChannelRouting
            };
            Channels = new List<IChannel>();
            ObserverRegistry = new ObserverRegistry();

            _lastUpdateTime = DateTime.UtcNow;
        }

        public IChannel CreateChannel(string address = null)
        {
            var newChannel = ChannelFactory.Create(address);
            OnChannelCreated(newChannel);
            return newChannel;
        }

        public void CloseAllChannels()
        {
            foreach (var channel in Channels.ToList())
            {
                channel.Close();
            }
        }

        public void Update()
        {
            var now = DateTime.UtcNow;
            var span = now - _lastUpdateTime;
            _lastUpdateTime = now;
            Updated?.Invoke(span);
        }

        private IChannel OnChannelRouting(IChannel parentChannel, string address)
        {
            return CreateChannel(address);
        }

        private void OnChannelCreated(IChannel newChannel)
        {
            newChannel.StateChanged += (channel, state) =>
            {
                if (state == ChannelStateType.Closed)
                {
                    OnChannelClosed(channel);
                }
            };

            Updated += newChannel.Update;

            lock (Channels)
            {
                Channels.Add(newChannel);
            }
        }

        private void OnChannelClosed(IChannel channel)
        {
            Updated -= channel.Update;

            lock (Channels)
            {
                Channels.Remove(channel);
            }
        }
    }
}
