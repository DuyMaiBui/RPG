using System.Collections.Generic;
using AuraEngine.Core;

namespace AuraEngine.Networking
{
    /* Keeps a bounded history of authoritative body states so a server can
       rewind and evaluate a client's hit/query against the world as the client
       saw it (server-side lag compensation). Snapshots are stamped with the
       server tick they represent. */
    public sealed class AuraLagCompensator
    {
        private readonly int _historyLength;
        private readonly List<Snapshot> _history = new List<Snapshot>();

        public AuraLagCompensator(int historyLength = 128)
        {
            _historyLength = historyLength < 1 ? 1 : historyLength;
        }

        public int Count => _history.Count;

        public void Record(uint tick, in AuraBodyState[] states)
        {
            var copy = states == null ? System.Array.Empty<AuraBodyState>() : (AuraBodyState[])states.Clone();
            _history.Add(new Snapshot(tick, copy));
            if (_history.Count > _historyLength)
                _history.RemoveAt(0);
        }

        public bool TryRewind(uint tick, uint maxLagTicks, out AuraBodyState[] states)
        {
            states = null;
            for (var index = _history.Count - 1; index >= 0; index--)
            {
                var snapshot = _history[index];
                if (snapshot.Tick > tick)
                    continue;

                if (tick - snapshot.Tick > maxLagTicks)
                    return false;

                states = snapshot.States;
                return true;
            }

            return false;
        }

        public void Clear() => _history.Clear();

        private readonly struct Snapshot
        {
            public Snapshot(uint tick, AuraBodyState[] states)
            {
                Tick = tick;
                States = states;
            }

            public uint Tick { get; }
            public AuraBodyState[] States { get; }
        }
    }
}
