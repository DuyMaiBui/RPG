using System;
using RPG.Simulation.Contracts;

namespace RPG.Core.Navigation
{
    public sealed class NavigationFlowField
    {
        private static readonly GridCoordinate[] Neighbors =
        {
            new(0, 1), new(1, 0), new(0, -1), new(-1, 0),
            new(1, 1), new(1, -1), new(-1, -1), new(-1, 1),
        };

        private readonly NavigationGrid _grid;
        private readonly float[] _costs;
        private readonly int[] _heapIndices;
        private readonly float[] _heapCosts;
        private readonly SimulationVector2[] _directions;
        private int _heapCount;

        public NavigationFlowField(NavigationGrid grid)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _costs = new float[grid.CellCount];
            _heapIndices = new int[grid.CellCount * Neighbors.Length];
            _heapCosts = new float[grid.CellCount * Neighbors.Length];
            _directions = new SimulationVector2[grid.CellCount];
            Target = new GridCoordinate(-1, -1);
            Radius = -1f;
            Revision = -1;
        }

        public GridCoordinate Target { get; private set; }
        public float Radius { get; private set; }
        public int Revision { get; private set; }

        public void Build(GridCoordinate target, float radius)
        {
            Target = target;
            Radius = radius;
            Revision = _grid.Revision;
            Array.Fill(_costs, float.MaxValue);
            Array.Fill(_directions, SimulationVector2.Zero);
            _heapCount = 0;

            if (!_grid.IsWalkableForRadius(target, radius))
                return;

            var targetIndex = ToIndex(target);
            _costs[targetIndex] = 0f;
            Push(targetIndex, 0f);
            while (_heapCount > 0)
            {
                Pop(out var currentIndex, out var currentCost);
                if (currentCost > _costs[currentIndex]) continue;

                var current = FromIndex(currentIndex);
                for (var neighborIndex = 0; neighborIndex < Neighbors.Length; neighborIndex++)
                {
                    var offset = Neighbors[neighborIndex];
                    var neighbor = new GridCoordinate(current.X + offset.X, current.Y + offset.Y);
                    if (!IsTraversable(current, neighbor, radius))
                        continue;

                    var neighborCell = ToIndex(neighbor);
                    var stepCost = offset.X != 0 && offset.Y != 0 ? 1.4142135f : 1f;
                    var cost = currentCost + stepCost;
                    if (cost >= _costs[neighborCell]) continue;
                    _costs[neighborCell] = cost;
                    Push(neighborCell, cost);
                }
            }

            for (var index = 0; index < _costs.Length; index++)
            {
                if (_costs[index] == float.MaxValue || index == targetIndex) continue;
                var cell = FromIndex(index);
                var bestCost = _costs[index];
                var bestDirection = SimulationVector2.Zero;
                for (var neighborIndex = 0; neighborIndex < Neighbors.Length; neighborIndex++)
                {
                    var neighbor = new GridCoordinate(cell.X + Neighbors[neighborIndex].X, cell.Y + Neighbors[neighborIndex].Y);
                    if (!IsTraversable(cell, neighbor, radius)) continue;
                    var neighborCost = _costs[ToIndex(neighbor)];
                    if (neighborCost >= bestCost) continue;
                    bestCost = neighborCost;
                    bestDirection = new SimulationVector2(neighbor.X - cell.X, neighbor.Y - cell.Y).Normalized();
                }

                _directions[index] = bestDirection;
            }
        }

        public bool TryGetDirection(SimulationVector2 position, out SimulationVector2 direction)
        {
            direction = SimulationVector2.Zero;
            if (Revision != _grid.Revision || !_grid.TryGetCoordinate(position, out var coordinate))
                return false;

            direction = _directions[ToIndex(coordinate)];
            return direction.LengthSquared > 0.000001f;
        }

        public bool TryGetCost(SimulationVector2 position, out float cost)
        {
            cost = float.MaxValue;
            return _grid.TryGetCoordinate(position, out var coordinate) &&
                   (cost = _costs[ToIndex(coordinate)]) < float.MaxValue;
        }

        private void Push(int index, float cost)
        {
            if (_heapCount == _heapIndices.Length)
                throw new InvalidOperationException("Navigation flow field heap capacity was exceeded.");

            var child = _heapCount++;
            while (child > 0)
            {
                var parent = (child - 1) / 2;
                if (_heapCosts[parent] <= cost) break;
                _heapIndices[child] = _heapIndices[parent];
                _heapCosts[child] = _heapCosts[parent];
                child = parent;
            }

            _heapIndices[child] = index;
            _heapCosts[child] = cost;
        }

        private void Pop(out int index, out float cost)
        {
            index = _heapIndices[0];
            cost = _heapCosts[0];
            _heapCount--;
            if (_heapCount == 0) return;

            var lastIndex = _heapIndices[_heapCount];
            var lastCost = _heapCosts[_heapCount];
            var parent = 0;
            while (true)
            {
                var left = parent * 2 + 1;
                if (left >= _heapCount) break;
                var right = left + 1;
                var child = right < _heapCount && _heapCosts[right] < _heapCosts[left] ? right : left;
                if (_heapCosts[child] >= lastCost) break;
                _heapIndices[parent] = _heapIndices[child];
                _heapCosts[parent] = _heapCosts[child];
                parent = child;
            }

            _heapIndices[parent] = lastIndex;
            _heapCosts[parent] = lastCost;
        }

        private int ToIndex(GridCoordinate coordinate) => coordinate.Y * _grid.Width + coordinate.X;
        private GridCoordinate FromIndex(int index) => new(index % _grid.Width, index / _grid.Width);

        private bool IsTraversable(GridCoordinate from, GridCoordinate to, float radius)
        {
            if (!_grid.IsWalkableForRadius(to, radius)) return false;
            var deltaX = to.X - from.X;
            var deltaY = to.Y - from.Y;
            return deltaX == 0 || deltaY == 0 ||
                   (_grid.IsWalkableForRadius(new GridCoordinate(from.X + deltaX, from.Y), radius) &&
                    _grid.IsWalkableForRadius(new GridCoordinate(from.X, from.Y + deltaY), radius));
        }
    }
}
