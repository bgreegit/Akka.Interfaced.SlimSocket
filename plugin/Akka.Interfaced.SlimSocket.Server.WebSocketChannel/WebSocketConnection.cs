using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging;

namespace Akka.Interfaced.SlimSocket.Server.WebSocketChannel
{
    public class WebSocketConnection : IDisposable
    {
        private ILog _logger;
        private WebSocket _socket;
        private IPEndPoint _localEndPoint;
        private IPEndPoint _remoteEndPoint;
        private int _state;

        private byte[] _receiveBuffer;
        private List<ArraySegment<byte>> _receiveLargeBuffers;
        private DateTime _lastReceiveTime;

        private byte[] _sendBuffer;
        private int _sendCount;
        private ConcurrentQueue<object> _sendQueue;

        public bool IsOpen
        {
            get { return _state == 1; }
        }

        public bool Active
        {
            get { return _state != 2; }
        }

        public IPEndPoint LocalEndPoint
        {
            get { return _localEndPoint; }
        }

        public IPEndPoint RemoteEndPoint
        {
            get { return _remoteEndPoint; }
        }

        public WebSocket WebSocket
        {
            get { return _socket; }
        }

        public WebSocketConnectionSettings Settings { get; set; }
        public int Id { get; set; }

        public DateTime LastReceiveTime
        {
            get { return _lastReceiveTime; }
        }

        public enum SerializeError
        {
            None,
            SerializeSizeExceeded,
            SerializeExceptionRaised,
            DeserializeSizeExceeded,
            DeserializeExceptionRaised,
            DeserializeNoPacket,
        }

        public event Action<WebSocketConnection> Opened;
        public event Action<WebSocketConnection, int> Closed;
        public event Action<WebSocketConnection, object> Received;
        public event Action<WebSocketConnection, SerializeError> SerializeErrored;

        public WebSocketConnection(ILog logger, WebSocket socket, IPEndPoint localEndPoint, IPEndPoint remoteEndPoint)
        {
            _logger = logger;
            _socket = socket;
            _localEndPoint = localEndPoint;
            _remoteEndPoint = remoteEndPoint;
        }

        ~WebSocketConnection()
        {
            Dispose(false);
        }

        public void Open()
        {
            if (Settings == null)
            {
                throw new InvalidOperationException("Settings");
            }

            if (Interlocked.CompareExchange(ref _state, 1, 0) != 0)
            {
                throw new InvalidOperationException("Already Opened");
            }

            ProcessOpen();
            StartReceive();
        }

        public async void Close()
        {
            await ProcessClose(WebSocketCloseStatus.NormalClosure);
        }

        private async Task ProcessClose(WebSocketCloseStatus closeStatus)
        {
            if (Interlocked.CompareExchange(ref _state, 2, 1) != 1)
            {
                return;
            }

            try
            {
                // Closed Event

                if (Closed != null)
                {
                    Closed(this, (int)closeStatus);
                }

                // Dispose Resources

                if (_socket != null)
                {
                    if (_socket.State == WebSocketState.Open)
                    {
                        await _socket.CloseAsync(closeStatus, string.Empty, CancellationToken.None);
                    }
                    else
                    {
                        await _socket.CloseOutputAsync(closeStatus, string.Empty, CancellationToken.None);
                    }
                }
            }
            catch (Exception e)
            {
                _logger.ErrorFormat("Exception raised in ProcessClose", e);
            }
        }

        private void HandleSerializeError(SerializeError error)
        {
            if (_logger != null)
            {
                _logger.WarnFormat("HandleSerializeError: {0}", error);
            }

            if (SerializeErrored != null)
            {
                SerializeErrored(this, error);
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                Close();
                GC.SuppressFinalize(this);
            }
        }

        private void ProcessOpen()
        {
            _receiveBuffer = new byte[Settings.ReceiveBufferSize];
            _receiveLargeBuffers = new List<ArraySegment<byte>>();

            _sendBuffer = new byte[Settings.SendBufferSize];
            _sendCount = 0;
            _sendQueue = new ConcurrentQueue<object>();

            // Opened Event

            if (Opened != null)
            {
                Opened(this);
            }
        }

        private async void StartReceive()
        {
            Task<WebSocketCloseStatus> t;

            var oldContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
            try
            {
                t = ReceiveLoop();
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(oldContext);
            }

            WebSocketCloseStatus closeStatus = WebSocketCloseStatus.NormalClosure;
            try
            {
                closeStatus = await t;
            }
            catch (Exception)
            {
                closeStatus = WebSocketCloseStatus.InternalServerError;
            }
            finally
            {
                await ProcessClose(closeStatus);
            }
        }

        private async Task<WebSocketCloseStatus> ReceiveLoop()
        {
            while (true)
            {
                if (_socket.State != WebSocketState.Open)
                {
                    return WebSocketCloseStatus.EndpointUnavailable;
                }

                ArraySegment<byte> buffer;
                if (_receiveLargeBuffers.Count == 0)
                {
                    buffer = new ArraySegment<byte>(_receiveBuffer, 0, _receiveBuffer.Length);
                }
                else
                {
                    var lastBuffer = _receiveLargeBuffers.Last();
                    var lastReceivePos = lastBuffer.Offset + lastBuffer.Count;
                    var remainLength = lastBuffer.Array.Length - lastReceivePos;
                    if (remainLength > 0)
                    {
                        buffer = new ArraySegment<byte>(lastBuffer.Array, lastReceivePos, remainLength);
                    }
                    else
                    {
                        var newBufferLength = lastBuffer.Array.Length * 2;
                        buffer = new ArraySegment<byte>(new byte[newBufferLength], 0, newBufferLength);
                    }
                }

                var result = await _socket.ReceiveAsync(buffer, CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return WebSocketCloseStatus.NormalClosure;
                }
                else if (result.MessageType == WebSocketMessageType.Text)
                {
                    return WebSocketCloseStatus.InvalidMessageType;
                }
                else if (result.MessageType == WebSocketMessageType.Binary)
                {
                    _receiveLargeBuffers.Add(new ArraySegment<byte>(buffer.Array, buffer.Offset, result.Count));

                    if (result.EndOfMessage)
                    {
                        var closeStatus = HandleReceivedMessage();
                        if (closeStatus.HasValue)
                        {
                            return closeStatus.Value;
                        }
                    }
                    else
                    {
                        // receiving data not done yet.
                    }
                }
            }
        }

        private byte[] ConvertToByteArray(IList<ArraySegment<byte>> list)
        {
            var buffer = new byte[list.Sum(a => a.Count)];
            int pos = 0;

            foreach (var a in list)
            {
                Buffer.BlockCopy(a.Array, a.Offset, buffer, pos, a.Count);
                pos += a.Count;
            }
            return buffer;
        }

        private WebSocketCloseStatus? HandleReceivedMessage()
        {
            // Message receive done, so create packet by serializer
            // Use large buffer (naive implementation)

            var messageBuffer = ConvertToByteArray(_receiveLargeBuffers);
            _receiveLargeBuffers.Clear();

            if (messageBuffer.Length > Settings.ReceiveBufferMaxSize)
            {
                HandleSerializeError(SerializeError.DeserializeSizeExceeded);
                return WebSocketCloseStatus.MessageTooBig;
            }

            using (var stream = new MemoryStream(messageBuffer, 0, messageBuffer.Length, false, true))
            {
                object packet;
                try
                {
                    packet = Settings.PacketSerializer.Deserialize(stream);
                }
                catch (Exception e)
                {
                    if (_logger != null)
                    {
                        var bytes = Convert.ToBase64String(_receiveBuffer, 0, Math.Min(messageBuffer.Length, 2048));
                        _logger.WarnFormat("Exception raised in deserializing: " + bytes, e);
                    }

                    HandleSerializeError(SerializeError.DeserializeExceptionRaised);
                    return WebSocketCloseStatus.InternalServerError;
                }

                if (packet == null)
                {
                    HandleSerializeError(SerializeError.DeserializeNoPacket);
                    return WebSocketCloseStatus.ProtocolError;
                }

                _lastReceiveTime = DateTime.UtcNow;
                if (Received != null)
                {
                    Received(this, packet);
                }
            }

            return null;
        }

        public async void Send(object packet)
        {
            if (packet == null)
            {
                throw new ArgumentNullException("packet");
            }

            if (_state != 1)
            {
                return;
            }

            if (Interlocked.Increment(ref _sendCount) == 1)
            {
                var oldContext = SynchronizationContext.Current;
                SynchronizationContext.SetSynchronizationContext(null);

                Task<WebSocketCloseStatus?> t;
                try
                {
                    t = StartSend(packet);
                }
                finally
                {
                    SynchronizationContext.SetSynchronizationContext(oldContext);
                }

                WebSocketCloseStatus? closeStatus = null;
                try
                {
                    closeStatus = await t;
                }
                catch (Exception)
                {
                    closeStatus = WebSocketCloseStatus.InternalServerError;
                }
                finally
                {
                    if (closeStatus.HasValue)
                    {
                        await ProcessClose(closeStatus.Value);
                    }
                }
            }
            else
            {
                _sendQueue.Enqueue(packet);
            }
        }

        private async Task<WebSocketCloseStatus?> StartSend(object firstPacket)
        {
            // handle first packet in argument
            {
                var closeStatus = await SendEachPacket(firstPacket);
                if (closeStatus.HasValue)
                {
                    return closeStatus;
                }
            }

            // try queued packets

            while (true)
            {
                if (Interlocked.Decrement(ref _sendCount) > 0)
                {
                    object packet;
                    while (_sendQueue.TryDequeue(out packet) == false)
                    {
                    }
                    if (packet != null)
                    {
                        var closeStatus = await SendEachPacket(packet);
                        if (closeStatus.HasValue)
                        {
                            return closeStatus;
                        }
                    }
                    else
                    {
                        return WebSocketCloseStatus.InternalServerError;
                    }
                }
                else
                {
                    return null;
                }
            }
        }

        private async Task<WebSocketCloseStatus?> SendEachPacket(object packet)
        {
            var packetLen = Settings.PacketSerializer.EstimateLength(packet);
            var tailSize = 0;
            if (packetLen > _sendBuffer.Length)
            {
                tailSize = packetLen - _sendBuffer.Length;
            }

            using (var stream = new HeadTailWriteStream(new ArraySegment<byte>(_sendBuffer), tailSize))
            {
                try
                {
                    Settings.PacketSerializer.Serialize(stream, packet);
                }
                catch (Exception e)
                {
                    if (_logger != null)
                    {
                        _logger.WarnFormat("Exception raised in serializing", e);
                    }

                    HandleSerializeError(SerializeError.SerializeExceptionRaised);
                    return WebSocketCloseStatus.InternalServerError;
                }

                try
                {
                    var streamLength = (int)stream.Length;
                    if (streamLength <= _sendBuffer.Length)
                    {
                        await _socket.SendAsync(new ArraySegment<byte>(_sendBuffer, 0, streamLength),
                            WebSocketMessageType.Binary, true, CancellationToken.None);
                    }
                    else
                    {
                        if (streamLength > Settings.SendBufferMaxSize)
                        {
                            HandleSerializeError(SerializeError.SerializeSizeExceeded);
                            return WebSocketCloseStatus.MessageTooBig;
                        }

                        await _socket.SendAsync(new ArraySegment<byte>(_sendBuffer, 0, _sendBuffer.Length),
                            WebSocketMessageType.Binary, false, CancellationToken.None);

                        var largeBuffer = stream.Tail.Value;
                        var largeBufferLen = streamLength - _sendBuffer.Length;
                        await _socket.SendAsync(new ArraySegment<byte>(largeBuffer.Array, 0, largeBufferLen),
                            WebSocketMessageType.Binary, true, CancellationToken.None);
                    }
                }
                catch (Exception)
                {
                    return WebSocketCloseStatus.NormalClosure;
                }
            }

            return null;
        }
    }
}
