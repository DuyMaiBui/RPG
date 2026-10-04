using System;

namespace AuraEngine.Networking
{
    public interface IAuraTransport
    {
        bool IsConnected { get; }

        void Send(ReadOnlySpan<byte> payload);

        bool TryReceive(out byte[] payload);

        event Action<byte[]> Received;

        event Action Disconnected;
    }
}
