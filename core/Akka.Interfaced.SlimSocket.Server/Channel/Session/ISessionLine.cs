using System;
using System.Net;

namespace Akka.Interfaced.SlimSocket.Server
{
    public interface ISessionLine
    {
        int SessionId { get; set; }
        int LineIndex { get; set; }
        EndPoint RemoteEndPoint { get; }

        void Open();
        void Send(object packet);
        void Close(bool isGracefulPreferred = false);

        event Action<ISessionLine> Closed;
        event Action<ISessionLine, SessionPacket> Received;
    }
}
