using System.Collections.Generic;

namespace AuraEngine.Networking
{
    public sealed class AuraSnapshotBaselineNegotiator
    {
        private readonly int _capacity;
        private readonly Queue<AuraSnapshotBaseline> _available = new Queue<AuraSnapshotBaseline>();
        private uint _nextId;

        public AuraSnapshotBaselineNegotiator(int capacity = 32)
        {
            _capacity = capacity < 1 ? 1 : capacity;
        }

        public void Record(uint serverTick)
        {
            _available.Enqueue(new AuraSnapshotBaseline(++_nextId, serverTick));
            while (_available.Count > _capacity)
                _available.Dequeue();
        }

        public bool TrySelect(uint acknowledgedSnapshotId, uint requestedTick, out AuraSnapshotBaseline baseline)
        {
            foreach (var candidate in _available)
            {
                if (candidate.SnapshotId == acknowledgedSnapshotId || candidate.ServerTick == requestedTick)
                {
                    baseline = candidate;
                    return true;
                }
            }

            baseline = default;
            return false;
        }

        public int Count => _available.Count;
    }
}
