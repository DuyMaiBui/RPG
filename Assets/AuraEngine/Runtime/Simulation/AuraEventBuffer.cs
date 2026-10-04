using System;
using AuraEngine.Core;

namespace AuraEngine.Simulation
{
    internal sealed class AuraEventBuffer
    {
        private AuraPhysicsEvent[] _events;
        private int _count;

        public AuraEventBuffer(int initialCapacity = 64)
        {
            _events = new AuraPhysicsEvent[Math.Max(1, initialCapacity)];
        }

        public int Count => _count;

        public void Clear() => _count = 0;

        public void Append(in AuraPhysicsEvent value)
        {
            if (_count == _events.Length)
                Array.Resize(ref _events, _events.Length * 2);

            _events[_count++] = value;
        }

        public int CopyTo(Span<AuraPhysicsEvent> destination)
        {
            var written = Math.Min(_count, destination.Length);
            for (var index = 0; index < written; index++)
                destination[index] = _events[index];

            return written;
        }
    }
}
