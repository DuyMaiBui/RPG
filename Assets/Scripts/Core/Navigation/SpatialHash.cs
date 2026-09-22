using System;
using System.Collections.Generic;
using RPG.Core.Actors;
using RPG.Simulation.Contracts;

namespace RPG.Core.Navigation
{
    public sealed class SpatialHash
    {
        private readonly float _cellSize;
        private readonly Dictionary<int, List<EntityId>> _buckets = new();
        public SpatialHash(float cellSize)
        {
            if (cellSize <= 0f) throw new ArgumentOutOfRangeException(nameof(cellSize));
            _cellSize = cellSize;
        }

        public void Rebuild(ActorRegistry actors)
        {
            foreach (var bucket in _buckets.Values)
                bucket.Clear();

            for (var index = 0; index < actors.SlotCount; index++)
            {
                if (!actors.TryGetAt(index, out var actor) ||
                    actor.Components.Get<HealthComponent>().IsDead)
                    continue;

                var position = actor.Components.Get<PositionComponent>().Position;
                var key = Key(Cell(position.X), Cell(position.Y));
                if (!_buckets.TryGetValue(key, out var bucket))
                {
                    bucket = new List<EntityId>(4);
                    _buckets.Add(key, bucket);
                }

                bucket.Add(actor.Id);
            }
        }

        public void Collect(SimulationVector2 position, float radius, List<EntityId> results)
        {
            results.Clear();
            var minX = Cell(position.X - radius);
            var maxX = Cell(position.X + radius);
            var minY = Cell(position.Y - radius);
            var maxY = Cell(position.Y + radius);
            for (var y = minY; y <= maxY; y++)
            {
                for (var x = minX; x <= maxX; x++)
                {
                    if (_buckets.TryGetValue(Key(x, y), out var bucket))
                        results.AddRange(bucket);
                }
            }
        }

        private int Cell(float value) => (int)MathF.Floor(value / _cellSize);
        private static int Key(int x, int y) => HashCode.Combine(x, y);
    }
}
