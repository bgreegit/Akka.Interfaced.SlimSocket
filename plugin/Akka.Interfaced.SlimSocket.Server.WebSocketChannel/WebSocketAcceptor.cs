using System;
using System.Net;
using System.Net.WebSockets;
using System.Threading;

namespace Akka.Interfaced.SlimSocket.Server.WebSocketChannel
{
    public class WebSocketAcceptor
    {
        private HttpListener _listener;
        private bool _isStop;

        public bool Active
        {
            get { return _listener != null; }
        }

        public HttpListener Listener
        {
            get { return _listener; }
        }

        public enum AcceptResult
        {
            Close,
            Accept
        }

        public event Func<WebSocketAcceptor, AcceptedWebSocket, AcceptResult> Accepted;

        // if Access-denied exception raise, you need grant permissions to the particular URL
        // ex) netsh http add urlacl url="http://+:80/" user=everyone
        // (http://stackoverflow.com/questions/4019466/httplistener-access-denied)
        public void Listen(string uriPrefix, int listenCount = 1)
        {
            if (listenCount < 1)
            {
                throw new ArgumentException(nameof(listenCount));
            }

            if (_listener != null)
            {
                throw new InvalidOperationException("Already Listening");
            }

            // uriPrefix ex) "http://+:80/ws/"
            _listener = new HttpListener();
            _listener.Prefixes.Add(uriPrefix);
            _listener.Start();

             IssueAccept();
        }

        private void IssueAccept()
        {
            var oldContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
            try
            {
                _listener.BeginGetContext(new AsyncCallback(OnGetContext), _listener);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(oldContext);
            }
        }

        public void OnGetContext(IAsyncResult result)
        {
            HttpListenerContext context = null;
            try
            {
                HttpListener listener = (HttpListener)result.AsyncState;
                context = listener.EndGetContext(result);
            }
            catch (Exception)
            {
                Close();
                return;
            }

            if (_listener != null && _isStop == false)
            {
                IssueAccept();
            }

            if (context != null)
            {
                ProcessContext(context);
            }
        }

        private async void ProcessContext(HttpListenerContext context)
        {
            if (_isStop)
            {
                // if WebSocketAcceptor._listener is closed, ongoing websocket connection is also closed.
                // So, just handle future websocket request as internal error

                context.Response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
                context.Response.Close();
                return;
            }

            if (context.Request.IsWebSocketRequest == false)
            {
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                context.Response.Close();
                return;
            }

            WebSocketContext webSocketContext = null;

            try
            {
                webSocketContext = await context.AcceptWebSocketAsync(subProtocol: null);
            }
            catch (Exception)
            {
                // The upgrade process failed somehow.
                // Regard as a failure if internal server error
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                context.Response.Close();
                return;
            }

            WebSocket webSocket = webSocketContext.WebSocket;
            if (webSocket != null)
            {
                var acceptedWebSocket = new AcceptedWebSocket()
                {
                    Url = context.Request.Url,
                    LocalEndpoint = context.Request.LocalEndPoint,
                    RemoteEndPoint = context.Request.RemoteEndPoint,
                    WebSocket = webSocket
                };

                if (Accepted == null || Accepted(this, acceptedWebSocket) == AcceptResult.Close)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Accept denied", CancellationToken.None);
                    webSocket.Dispose();
                }
            }
            else
            {
                throw new InvalidOperationException("webSocketContext.WebSocket == null on AcceptWebSocketAsync");
            }
        }

        public void Stop()
        {
            _isStop = true;
        }

        public void Close()
        {
            var listener = _listener;
            if (listener == null)
            {
                return;
            }

            _listener = null;
            listener.Close();
        }
    }

    public class AcceptedWebSocket
    {
        public Uri Url;
        public IPEndPoint LocalEndpoint;
        public IPEndPoint RemoteEndPoint;
        public WebSocket WebSocket;
    }
}
