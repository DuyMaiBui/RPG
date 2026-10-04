using System;
using System.Collections.Generic;

namespace AuraEngine.Networking
{
    public sealed class InMemoryAuraTransport : IAuraTransport, IDisposable
    {
        private readonly Queue<byte[]> _inbox = new Queue<byte[]>();
        private bool _connected = true;
        private bool _disposed;

        public InMemoryAuraTransport Peer { get; private set; }

        public bool IsConnected => _connected && !_disposed;

        public event Action<byte[]> Received;

        public event Action Disconnected;

        public static (InMemoryAuraTransport A, InMemoryAuraTransport B) CreatePair()
        {
            var a = new InMemoryAuraTransport();
            var b = new InMemoryAuraTransport();
            a.Peer = b;
            b.Peer = a;
            return (a, b);
        }

        public void Send(ReadOnlySpan<byte> payload)
        {
            if (!IsConnected)
                throw new InvalidOperationException("The transport is not connected.");

            if (Peer == null)
                return;

            Peer.Deliver(payload.ToArray());
        }

        public bool TryReceive(out byte[] payload)
        {
            if (_inbox.Count > 0)
            {
                payload = _inbox.Dequeue();
                return true;
            }

            payload = null;
            return false;
        }

        public void Disconnect()
        {
            if (!_connected)
                return;

            _connected = false;
            Disconnected?.Invoke();

            var peer = Peer;
            if (peer != null && peer._connected)
            {
                peer._connected = false;
                peer.Disconnected?.Invoke();
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            Disconnect();
            _inbox.Clear();
        }

        private void Deliver(byte[] payload)
        {
            if (!_connected || _disposed)
                return;

            _inbox.Enqueue(payload);
            Received?.Invoke(payload);
        }
    }
}
