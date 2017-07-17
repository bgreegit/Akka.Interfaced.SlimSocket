using System;
using System.Threading;
using System.Threading.Tasks;
using Akka.Interfaced.SlimSocket.Client;

namespace Akka.Interfaced.SlimSocket
{
    public class TaskBasedChannelUpdator
    {
        private CancellationTokenSource _cts = new CancellationTokenSource();

        public void StartUpdate(IChannel channel)
        {
            channel.StateChanged += OnChannelStateChanged;

            WaitCallback waitCallback = async (s) =>
            {
                DateTime lastUpdateTime = DateTime.UtcNow;
                while (_cts.IsCancellationRequested == false)
                {
                    var now = DateTime.UtcNow;
                    var span = now - lastUpdateTime;
                    lastUpdateTime = now;
                    channel.Update(span);
                    await Task.Delay(10);
                }
            };
            ThreadPool.UnsafeQueueUserWorkItem(waitCallback, channel);
        }

        private void OnChannelStateChanged(IChannel channel, ChannelStateType state)
        {
            if (state == ChannelStateType.Closed)
            {
                _cts.Cancel();
            }
        }
    }
}
