using System;
using System.Collections.Generic;
using AuraEngine.Simulation;

namespace AuraEngine.Networking
{
    public sealed class AuraRollbackBuffer
    {
        private readonly int _capacity;
        private readonly Queue<Entry> _entries = new Queue<Entry>();

        public AuraRollbackBuffer(int capacity = 128)
        {
            _capacity = Math.Max(1, capacity);
        }

        public int Count => _entries.Count;

        public void Record(uint tick, AuraSimulationWorld world, IReadOnlyList<AuraInputCommand> inputs)
        {
            if (world == null)
                throw new ArgumentNullException(nameof(world));

            _entries.Enqueue(new Entry(tick, world.SaveState(), inputs));
            while (_entries.Count > _capacity)
                _entries.Dequeue();
        }

        public bool TryGet(uint tick, out byte[] state, out IReadOnlyList<AuraInputCommand> inputs)
        {
            foreach (var entry in _entries)
            {
                if (entry.Tick != tick)
                    continue;
                state = (byte[])entry.State.Clone();
                inputs = entry.Inputs;
                return true;
            }

            state = null;
            inputs = null;
            return false;
        }

        public void Clear() => _entries.Clear();

        private readonly struct Entry
        {
            public Entry(uint tick, byte[] state, IReadOnlyList<AuraInputCommand> inputs)
            {
                Tick = tick;
                State = state;
                Inputs = inputs;
            }

            public uint Tick { get; }
            public byte[] State { get; }
            public IReadOnlyList<AuraInputCommand> Inputs { get; }
        }
    }
}
