using System;

namespace Akka.Interfaced.SlimSocket.Client.SessionChannel
{
    public interface ISessionLine
    {
        int LineIndex { get; }
        void Send(object packet);
        void Close();

        event Action<object, int> Closed;
        event Action<object, object> Received;
    }

    public interface IReliableSessionLine : ISessionLine
    {
        void Create(string token);
        void Rebind(int sessionId, int clientMessageAck);

        event Action<object, int> Created;
        event Action<object, int> Rebound;
    }
}
