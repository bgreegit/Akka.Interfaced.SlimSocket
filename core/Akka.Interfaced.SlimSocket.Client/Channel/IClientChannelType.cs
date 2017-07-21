using Common.Logging;

namespace Akka.Interfaced.SlimSocket.Client
{
    public interface IClientChannelType : IChannelType
    {
        ChannelBase CreateChannel(string address, ILog channelLogger, IPacketSerializer packetSerializer);
    }
}
