namespace Akka.Interfaced.SlimSocket.Server.TcpChannel
{
    public class TcpChannelType : IChannelType
    {
        public string Name => TypeName;
        public const string TypeName = "Tcp";
    }
}
