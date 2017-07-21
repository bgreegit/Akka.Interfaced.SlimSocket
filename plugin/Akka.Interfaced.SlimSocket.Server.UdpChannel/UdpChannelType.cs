namespace Akka.Interfaced.SlimSocket.Server.UdpChannel
{
    public class UdpChannelType : IChannelType
    {
        public string Name => TypeName;
        public const string TypeName = "Udp";
    }
}
