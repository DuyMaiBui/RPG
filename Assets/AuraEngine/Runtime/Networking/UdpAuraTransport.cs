using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace AuraEngine.Networking
{
    /* Unreliable UDP transport for real-time game traffic. Each payload is one
       datagram; the codec layer is responsible for sequencing and reliability
       where needed. */
    public sealed class UdpAuraTransport : IAuraTransport, IDisposable
    {
        private readonly ConcurrentQueue<byte[]> _inbox = new ConcurrentQueue<byte[]>();
        private readonly Socket _socket;
        private readonly EndPoint _remote;
        private readonly byte[] _receiveBuffer = new byte[64 * 1024];
        private int _connected;
        private int _disposed;

        private UdpAuraTransport(Socket socket, EndPoint remote)
        {
            _socket = socket;
            _remote = remote;
            _connected = 1;
        }

        public bool IsConnected => _connected == 1 && _disposed == 0;

        public EndPoint RemoteEndPoint => _remote;

        public event Action<byte[]> Received;

        public event Action Disconnected;

        public static UdpAuraTransport CreateClient(string host, int port)
        {
            var remote = new IPEndPoint(Resolve(host), port);
            var socket = new Socket(remote.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
            socket.Connect(remote);
            return new UdpAuraTransport(socket, remote);
        }

        public static UdpAuraTransport CreateServer(int port, out int boundPort)
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Bind(new IPEndPoint(IPAddress.Loopback, port));
            boundPort = ((IPEndPoint)socket.LocalEndPoint).Port;
            var any = new IPEndPoint(IPAddress.Any, 0);
            return new UdpAuraTransport(socket, any);
        }

        public void Send(ReadOnlySpan<byte> payload)
        {
            if (!IsConnected)
                throw new InvalidOperationException("The UDP transport is not connected.");

            try
            {
                var buffer = payload.ToArray();
                if (_socket.Connected)
                    _socket.Send(buffer, SocketFlags.None);
                else
                    _socket.SendTo(buffer, SocketFlags.None, _remote);
            }
            catch (SocketException)
            {
                MarkDisconnected();
            }
        }

        public bool TryReceive(out byte[] payload)
        {
            payload = null;
            if (!IsConnected)
                return false;

            try
            {
                if (!_socket.Poll(0, SelectMode.SelectRead))
                    return false;

                EndPoint source = new IPEndPoint(IPAddress.Any, 0);
                var count = _socket.ReceiveFrom(_receiveBuffer, ref source);
                if (count <= 0)
                    return false;

                payload = new byte[count];
                Array.Copy(_receiveBuffer, payload, count);
                Received?.Invoke(payload);
                return true;
            }
            catch (SocketException)
            {
                MarkDisconnected();
                return false;
            }
        }

        public void Disconnect() => MarkDisconnected();

        public void Dispose()
        {
            if (_disposed == 1)
                return;

            _disposed = 1;
            MarkDisconnected();
            _socket.Dispose();
        }

        private void MarkDisconnected()
        {
            if (System.Threading.Interlocked.Exchange(ref _connected, 0) == 1)
                Disconnected?.Invoke();
        }

        private static IPAddress Resolve(string host)
        {
            if (string.IsNullOrEmpty(host))
                return IPAddress.Loopback;

            return IPAddress.TryParse(host, out var address) ? address : IPAddress.Loopback;
        }
    }
}
