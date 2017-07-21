using Akka.Interfaced.SlimSocket;
using Akka.Interfaced.SlimSocket.Client;
using Akka.Interfaced.SlimSocket.Client.SessionChannel;
using Akka.Interfaced.SlimSocket.Client.TcpChannel;
using Common.Logging;
using Echo.Interface;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;

namespace Echo.Program.Client
{
    internal class EchoDriver
    {
        private ChannelFactory _channelFactory;
        private IClientChannelType _channelType;
        private ChannelUpdator _channelUpdator;

        private class ChannelEntry
        {
            public IChannel Channel { get; set; }
            public EchoRef Echo { get; set; }
            public TimeSpan Elapsed { get; set; }
            public EchoSender Sender { get; set; }
        }
        private List<ChannelEntry> _entries = new List<ChannelEntry>();
        private byte[] _echoData;

        public class Config
        {
            public string ChannelType { get; set; }
            public string RemoteIp { get; set; }
            public int RemotePort { get; set; }
            public int RequestInterval { get; set; }
            public int RequestLength { get; set; }
            public int RequestWaitDelay { get; set; }
            public int ChannelCount { get; set; }
        }
        private Config _config;

        private Statistics _statistics = new Statistics();

        private Timer _statisticsTimer;
        private DateTime _lastShowStatistics;

        public EchoDriver(Config config)
        {
            _config = config;

            _channelFactory = new ChannelFactory
            {
                CreateChannelLogger = () => LogManager.GetLogger("Channel"),
                CreateObserverRegistry = () => new ObserverRegistry(),
                PacketSerializer = PacketSerializer.CreatePacketSerializer(),
            };

            if (config.ChannelType == TcpClientChannelType.TypeName)
            {
                _channelType = new TcpClientChannelType()
                {
                    ConnectEndPoint = new IPEndPoint(IPAddress.Parse(_config.RemoteIp), _config.RemotePort),
                };
            }
            else if (config.ChannelType == SessionClientChannelType.TypeName)
            {
                _channelType = new SessionClientChannelType()
                {
                    ConnectEndPoint = new IPEndPoint(IPAddress.Parse(_config.RemoteIp), _config.RemotePort),
                    SessionSettings = new SessionSettings()
                };
            }
            else
            {
                throw new InvalidOperationException("Invalid config.ChannelType");
            }

            _channelUpdator = new ChannelUpdator();
            _channelUpdator.Start(100);

            _echoData = new byte[_config.RequestLength];

            _statisticsTimer = new Timer(ShowStatistics, null, 1000, 1000);
            _lastShowStatistics = DateTime.UtcNow;
        }

        private async void CreateChannel(int id)
        {
            var channel = _channelFactory.CreateByType(_channelType);
            channel.StateChanged += ChannelStateChanged;

            try
            {
                await channel.ConnectAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine($"ConnectAsync exception e={e.ToString()}");
                _statistics.IncClosedCount();
                return;
            }

            var echo = channel.CreateRef<EchoRef>(1);
            var sender = new EchoSender(echo, _echoData, _statistics, _config.RequestWaitDelay);
            var entry = new ChannelEntry()
            {
                Channel = channel,
                Echo = echo,
                Sender = sender
            };

            _channelUpdator.AddChannel(channel);
            lock (_entries)
            {
                _entries.Add(entry);
            }

            sender.Start();
        }

        private void ChannelStateChanged(IChannel channel, ChannelStateType state)
        {
            if (state == ChannelStateType.Connected)
            {
                _statistics.IncConnectedCount();
            }
            else if (state == ChannelStateType.Closed)
            {
                _statistics.IncClosedCount();
            }
        }

        public void Start()
        {
            var c = _config.ChannelCount;
            for (int i = 0; i < c; ++i)
            {
                CreateChannel(i);
            }
        }

        public void Stop()
        {
            _channelUpdator.Stop();

            List<ChannelEntry> entries = _entries;
            _entries.Clear();
            foreach (var e in entries)
            {
                e.Channel.Close();
            }
        }

        private void ShowStatistics(object state)
        {
            var now = DateTime.UtcNow;
            var elapsed = now - _lastShowStatistics;
            var s = _statistics.GetSnapshotAndReset();
            Console.WriteLine(s.GetStatisticsString((int)elapsed.TotalMilliseconds));
        }
    }

    public class Statistics
    {
        public int ConnectedCount;
        public int ClosedCount;
        public int SendCount;
        public long SendTime;

        public void IncConnectedCount() { Interlocked.Increment(ref ConnectedCount); }
        public void IncClosedCount() { Interlocked.Increment(ref ClosedCount); }
        public void IncSendCount(long elapsed)
        {
            Interlocked.Increment(ref SendCount);
            Interlocked.Add(ref SendTime, elapsed);
        }

        public Statistics GetSnapshotAndReset()
        {
            var s = new Statistics()
            {
                ConnectedCount = Interlocked.Exchange(ref ConnectedCount, 0),
                ClosedCount = Interlocked.Exchange(ref ClosedCount, 0),
                SendCount = Interlocked.Exchange(ref SendCount, 0),
                SendTime = Interlocked.Exchange(ref SendTime, 0),
            };
            return s;
        }

        public string GetStatisticsString(int elapsed)
        {
            return $"Connected={ConnectedCount} Closed={ClosedCount} SendCount={SendCount} AvgTime={((double)SendTime / SendCount) / TimeSpan.TicksPerMillisecond}";
        }
    }
}
