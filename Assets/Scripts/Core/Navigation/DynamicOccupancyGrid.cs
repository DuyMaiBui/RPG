using System;
using RPG.Core.Actors;
using RPG.Simulation.Contracts;

namespace RPG.Core.Navigation
{
    public sealed class DynamicOccupancyGrid
    {
        private readonly int[] _counts;
        private readonly int[] _stationaryCounts;

        public DynamicOccupancyGrid(NavigationGrid grid)
        {
            Grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _counts = new int[grid.CellCount];
            _stationaryCounts = new int[grid.CellCount];
        }

        public NavigationGrid Grid { get; }

        public void Rebuild(ActorRegistry actors)
        {
            Array.Clear(_counts, 0, _counts.Length);
            Array.Clear(_stationaryCounts, 0, _stationaryCounts.Length);
            for (var index = 0; index < actors.SlotCount; index++)
            {
                if (!actors.TryGetAt(index, out var actor) || actor.Components.Get<HealthComponent>().IsDead)
                    continue;
                if (!Grid.TryGetCoordinate(actor.Components.Get<PositionComponent>().Position, out var coordinate))
                    continue;
                var cellIndex = coordinate.Y * Grid.Width + coordinate.X;
                _counts[cellIndex]++;
                if (actor.Components.Get<MovementComponent>().Speed <= 0f)
                    _stationaryCounts[cellIndex]++;
            }
        }

        public int GetCount(SimulationVector2 position)
        {
            return Grid.TryGetCoordinate(position, out var coordinate)
                ? _counts[coordinate.Y * Grid.Width + coordinate.X]
                : int.MaxValue;
        }

        public float GetEstimatedWait(SimulationVector2 position, float speed)
        {
            var count = GetCount(position);
            if (count <= 1) return 0f;
            return (count - 1) * Grid.CellSize / MathF.Max(0.01f, speed);
        }

        public int GetStationaryCount(GridCoordinate coordinate)
        {
            if (coordinate.X < 0 || coordinate.X >= Grid.Width || coordinate.Y < 0 || coordinate.Y >= Grid.Height)
                return int.MaxValue;
            return _stationaryCounts[coordinate.Y * Grid.Width + coordinate.X];
        }

        public bool IsStationaryPathBlocked(SimulationVector2 start, SimulationVector2 destination)
        {
            var delta = destination - start;
            var distance = MathF.Sqrt(delta.LengthSquared);
            var steps = Math.Max(1, (int)MathF.Ceiling(distance / MathF.Max(Grid.CellSize * 0.5f, 0.01f)));
            for (var step = 0; step <= steps; step++)
            {
                var position = start + delta * (step / (float)steps);
                if (Grid.TryGetCoordinate(position, out var coordinate) && GetStationaryCount(coordinate) > 0)
                    return true;
            }

            return false;
        }
    }
}
