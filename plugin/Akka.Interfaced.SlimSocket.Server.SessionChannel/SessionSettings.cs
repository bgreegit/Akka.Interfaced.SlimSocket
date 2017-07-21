using System;

namespace Akka.Interfaced.SlimSocket.Server.SessionChannel
{
    public class SessionSettings
    {
        // For channel
        public TimeSpan OfflineTimeout { get; set; }
        public TimeSpan TimeWaitTimeout { get; set; }

        // For keep alive
        public TimeSpan AliveCheckInterval { get; set; }
        public TimeSpan AliveCheckWaitInterval { get; set; }

        public SessionSettings()
        {
            OfflineTimeout = TimeSpan.FromSeconds(20);
            TimeWaitTimeout = TimeSpan.FromSeconds(1);

            AliveCheckInterval = TimeSpan.FromSeconds(4);
            AliveCheckWaitInterval = TimeSpan.FromSeconds(2);
        }
    }
}
