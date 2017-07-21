using System;

namespace Akka.Interfaced.SlimSocket.Client.WebSocketChannel
{
    public interface IWebSocket
    {
        void Connect(string uri);
        void Close();
        void Send(byte[] buffer, int length, int offset);

        event Action<object> Connected;
        event Action<object, int> Closed;
        event Action<object, byte[]> Received;
    }
}
