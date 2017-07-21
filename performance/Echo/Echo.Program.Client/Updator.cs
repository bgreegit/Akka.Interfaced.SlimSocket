using Akka.Interfaced.SlimSocket.Client;
using Echo.Interface;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Echo.Program.Client
{
    internal class Updator
    {
        private Timer _timer;
        private TimeSpan _interval;
        private DateTime _lastUpdate;

        public event Action<TimeSpan> Update;

        private void OnTimer(object state)
        {
            var now = DateTime.UtcNow;
            var elapsed = now - _lastUpdate;
            if (elapsed < _interval)
            {
                return;
            }

            _lastUpdate += _interval;
            Update?.Invoke(_interval);
        }

        public void StartUpdate(int interval)
        {
            _lastUpdate = DateTime.UtcNow;
            _interval = TimeSpan.FromMilliseconds(interval);
            _timer = new Timer(OnTimer, null, 10, 10);
        }

        public void Stop()
        {
            _timer?.Dispose();
        }
    }

    internal class ChannelUpdator : Updator
    {
        private event Action<TimeSpan> ChannelUpdate;

        public void AddChannel(IChannel channel)
        {
            ChannelUpdate += channel.Update;

            channel.StateChanged += (c, state) =>
            {
                if (state == ChannelStateType.Closed)
                {
                    OnChannelClosed(c);
                }
            };
        }

        public void Start(int interval)
        {
            Update += UpdateChannel;
            StartUpdate(interval);
        }

        private void UpdateChannel(TimeSpan span)
        {
            ChannelUpdate?.Invoke(span);
        }

        private void OnChannelClosed(IChannel channel)
        {
            ChannelUpdate -= channel.Update;
        }
    }
    
    internal class EchoSender
    {
        private EchoRef _echo;
        private byte[] _data;
        private Statistics _statistics;
        private int _waitDelay;
        private Stopwatch _stopwatch = new Stopwatch();
        private CancellationTokenSource _cts = new CancellationTokenSource(); 

        public EchoSender(EchoRef echo, byte[] data, Statistics statistics, int waitDelay)
        {
            _echo = echo;
            _data = data;
            _statistics = statistics;
            _waitDelay = waitDelay;
        }

        public void Start()
        {
            WaitCallback waitCallback = async (o) =>
            {
                while (_cts.IsCancellationRequested == false && _echo.IsChannelConnected())
                {
                    _stopwatch.Start();
                    try
                    {
                        await _echo.Echo(_data);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Echo exception e={e.ToString()}");
                    }
                    var elapsed = _stopwatch.ElapsedTicks;
                    _statistics.IncSendCount(elapsed);
                    _stopwatch.Reset();

                    if (_waitDelay > 0)
                    {
                        await Task.Delay(_waitDelay);
                    }
                }
            };
            ThreadPool.UnsafeQueueUserWorkItem(waitCallback, null);
        }

        public void Stop()
        {
            _cts.Cancel();
        }
    }
}
