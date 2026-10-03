using System;
using System.Collections.Generic;

namespace AuraEngine.Networking
{
    public sealed class AuraReliableChannel
    {
        private readonly int _maximumPayload;
        private readonly Dictionary<uint, byte[]> _pending = new Dictionary<uint, byte[]>();
        private uint _nextSend;
        private uint _nextReceive;

        public AuraReliableChannel(int maximumPayload = 1200)
        {
            if (maximumPayload < 64)
                throw new ArgumentOutOfRangeException(nameof(maximumPayload));
            _maximumPayload = maximumPayload;
        }

        public uint LastSentSequence => _nextSend;
        public uint LastReceivedSequence => _nextReceive == 0 ? 0 : _nextReceive - 1;
        public int PendingCount => _pending.Count;

        public IReadOnlyList<AuraReliablePacket> Enqueue(byte[] payload)
        {
            payload ??= Array.Empty<byte>();
            var count = Math.Max(1, (payload.Length + _maximumPayload - 1) / _maximumPayload);
            var packets = new List<AuraReliablePacket>(count);
            var sequence = ++_nextSend;
            for (var index = 0; index < count; index++)
            {
                var offset = index * _maximumPayload;
                var length = Math.Min(_maximumPayload, payload.Length - offset);
                var fragment = new byte[length];
                Array.Copy(payload, offset, fragment, 0, length);
                packets.Add(new AuraReliablePacket(sequence, LastReceivedSequence, (ushort)index, (ushort)count, fragment));
            }

            _pending[sequence] = payload;
            return packets;
        }

        public void Acknowledge(uint sequence)
        {
            _pending.Remove(sequence);
        }

        public bool TryAccept(uint sequence)
        {
            if (sequence < _nextReceive)
                return false;
            if (sequence > _nextReceive)
                return false;
            _nextReceive++;
            return true;
        }
    }
}
