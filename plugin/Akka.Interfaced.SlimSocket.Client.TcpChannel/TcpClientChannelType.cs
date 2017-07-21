using Common.Logging;
using System;
using System.Net;

namespace Akka.Interfaced.SlimSocket.Client.TcpChannel
{
    public class TcpClientChannelType : IClientChannelType
    {
        public IPEndPoint ConnectEndPoint { get; set; }
        public string ConnectToken { get; set; }

        public string Name => TypeName;
        public const string TypeName = "Tcp";

        public ChannelBase CreateChannel(string address, ILog channelLogger, IPacketSerializer packetSerializer)
        {
            var connectEndPoint = ConnectEndPoint;
            var connectToken = ConnectToken;

            if (string.IsNullOrEmpty(address) == false)
            {
                // type|endpoint|{token}
                var parts = address.Split('|');
                if (parts.Length < 2)
                {
                    return null;
                }
                if (parts[0] != Name)
                {
                    return null;
                }

                connectEndPoint = IPEndPointHelper.Parse(parts[1]);
                connectToken = parts.Length > 2 ? parts[2] : null;
            }

            return new TcpChannel(channelLogger, connectEndPoint, connectToken, packetSerializer);
        }
    }
}
