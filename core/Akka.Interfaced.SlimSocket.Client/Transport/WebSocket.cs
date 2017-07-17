using System;
using System.IO;
using Common.Logging;

namespace Akka.Interfaced.SlimSocket.Client
{
    // Default IWebSocket implementation using WebSocketSharp
    public class WebSocket : IWebSocket
    {
        private WebSocketSharp.WebSocket _webSocket;
        private ILog _logger;

        public event Action<object> Connected;
        public event Action<object, int> Closed;
        public event Action<object, byte[]> Received;

        public WebSocket(ILog logger)
        {
            _logger = logger;
        }

        void IWebSocket.Connect(string uri)
        {
            if (_webSocket != null)
            {
                throw new Exception("Already try connected");
            }

            _webSocket = new WebSocketSharp.WebSocket(uri);
            _webSocket.OnMessage += (sender, e) =>
            {
                if (Received != null)
                {
                    Received(this, e.RawData);
                }
            };
            _webSocket.OnOpen += (sender, e) =>
            {
                if (Connected != null)
                {
                    Connected(this);
                }
            };
            _webSocket.OnError += (sender, e) =>
            {
                _logger?.ErrorFormat("OnError {0} {1}", e.Message, e.Exception != null ? e.Exception.ToString() : string.Empty);
                _webSocket.Close();
            };
            _webSocket.OnClose += (sender, e) =>
            {
                if (Closed != null)
                {
                    Closed(this, (int)e.Code);
                }
            };
            _webSocket.ConnectAsync();
        }

        void IWebSocket.Close()
        {
            if (_webSocket != null)
            {
                _webSocket.Close();
            }
        }

        void IWebSocket.Send(byte[] buffer, int length, int offset)
        {
            if (_webSocket == null || _webSocket.ReadyState != WebSocketSharp.WebSocketState.Open)
            {
                return;
            }

            var ms = new MemoryStream(buffer, offset, length, false);
            _webSocket.SendAsync(ms, length, (isSuccess) =>
            {
                if (isSuccess == false)
                {
                    _logger?.ErrorFormat("Send Failed");
                    _webSocket.Close();
                }

                ms.Dispose();
            });
        }
    }
}
