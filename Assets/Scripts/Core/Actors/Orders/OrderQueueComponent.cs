namespace RPG.Core.Actors
{
    /// <summary>An actor's order queue. The current order runs until it completes, is replaced or is cleared; queued
    /// orders then run in order. The queue is bounded so a spammed command stream cannot grow actor state without
    /// limit — a full queue rejects the command instead of dropping an order silently.</summary>
    public sealed class OrderQueueComponent : IActorComponent
    {
        public const int MaximumQueuedOrders = 8;

        private readonly ActorOrder[] _queued = new ActorOrder[MaximumQueuedOrders];
        private int _count;

        public bool HasOrder { get; private set; }

        public ActorOrder Current { get; private set; }

        public int QueuedCount => _count;

        /// <summary>Adds an order. <paramref name="replace"/> clears whatever is queued first, which is what a plain
        /// (unshifted) player order does.</summary>
        public bool Enqueue(ActorOrder order, bool replace)
        {
            if (replace)
            {
                Clear();
                Current = order;
                HasOrder = true;
                return true;
            }

            if (!HasOrder)
            {
                Current = order;
                HasOrder = true;
                return true;
            }

            if (_count >= MaximumQueuedOrders)
                return false;

            _queued[_count] = order;
            _count++;
            return true;
        }

        /// <summary>Promotes the next queued order. Returns false when nothing is left, which clears the queue.</summary>
        public bool Advance()
        {
            if (_count == 0)
            {
                Clear();
                return false;
            }

            Current = _queued[0];
            HasOrder = true;
            for (var index = 1; index < _count; index++)
                _queued[index - 1] = _queued[index];

            _count--;
            _queued[_count] = default;
            return true;
        }

        public void Clear()
        {
            HasOrder = false;
            Current = default;
            _count = 0;
        }
    }
}
