namespace Akka.Interfaced.SlimSocket.Server.SessionChannel
{
    public class SessionChannelType : IChannelType
    {
        public string Name => TypeName;
        public const string TypeName = "Session";
    }
}
