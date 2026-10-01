using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace AuraEngine.Networking
{
    /* WebSocket transport built on System.Net.WebSockets. Reference-type, so
       it is registered as an instance; the socket loop runs on the thread pool
       and raises Received on a background thread. */
    public sealed class WebSocketAuraTransport : IAuraTransport, IDisposable
    {
        private readonly WebSocket _socket;
        private readonly ConcurrentQueue<byte[]> _inbox = new ConcurrentQueue<byte[]>();
        private readonly CancellationTokenSource _cancellation = new CancellationTokenSource();
        private int _connected = 1;
        private int _disposed;

        public WebSocketAuraTransport(WebSocket socket)
        {
            _socket = socket ?? throw new ArgumentNullException(nameof(socket));
            _ = Task.Run(ReceiveLoop);
        }

        public bool IsConnected => _connected == 1 && _disposed == 0 && _socket.State == WebSocketState.Open;

        public event Action<byte[]> Received;

        public event Action Disconnected;

        public async Task SendAsync(byte[] payload)
        {
            if (!IsConnected)
                throw new InvalidOperationException("The WebSocket transport is not connected.");

            await _socket.SendAsync(new ArraySegment<byte>(payload), WebSocketMessageType.Binary, true, _cancellation.Token).ConfigureAwait(false);
        }

        public void Send(ReadOnlySpan<byte> payload)
        {
            var copy = payload.ToArray();
            _ = SendAsync(copy);
        }

        public bool TryReceive(out byte[] payload) => _inbox.TryDequeue(out payload);

        public void Disconnect() => MarkDisconnected();

        public void Dispose()
        {
            if (_disposed == 1)
                return;

            _disposed = 1;
            _cancellation.Cancel();
            MarkDisconnected();
            _socket.Dispose();
            _cancellation.Dispose();
        }

        private async Task ReceiveLoop()
        {
            var buffer = new byte[64 * 1024];
            try
            {
                while (!_cancellation.IsCancellationRequested && _socket.State == WebSocketState.Open)
                {
                    var result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), _cancellation.Token).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close)
                        break;

                    var payload = new byte[result.Count];
                    Array.Copy(buffer, payload, result.Count);
                    _inbox.Enqueue(payload);
                    Received?.Invoke(payload);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (WebSocketException)
            {
            }
            finally
            {
                MarkDisconnected();
            }
        }

        private void MarkDisconnected()
        {
            if (Interlocked.Exchange(ref _connected, 0) == 1)
                Disconnected?.Invoke();
        }
    }
}
