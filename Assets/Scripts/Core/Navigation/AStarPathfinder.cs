using System;
using RPG.Simulation.Contracts;

namespace RPG.Core.Navigation
{
    public sealed class AStarPathfinder
    {
        private static readonly GridCoordinate[] Neighbors =
        {
            new(0, 1), new(1, 0), new(0, -1), new(-1, 0),
        };

        private readonly NavigationGrid _grid;
        private readonly float[] _costs;
        private readonly int[] _parents;
        private readonly int[] _open;
        private readonly bool[] _openFlags;
        private readonly SimulationVector2[] _pathScratch;
        private int _openCount;

        public AStarPathfinder(NavigationGrid grid)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _costs = new float[grid.CellCount];
            _parents = new int[grid.CellCount];
            _open = new int[grid.CellCount];
            _openFlags = new bool[grid.CellCount];
            _pathScratch = new SimulationVector2[grid.CellCount];
        }

        public bool TryFindPath(
            SimulationVector2 start,
            SimulationVector2 destination,
            float radius,
            out SimulationVector2[] path)
        {
            path = null;
            if (!TryFindPath(start, destination, radius, null, _pathScratch, out var length))
                return false;

            path = new SimulationVector2[length];
            Array.Copy(_pathScratch, path, length);
            return true;
        }

        public bool TryFindPath(
            SimulationVector2 start,
            SimulationVector2 destination,
            float radius,
            SimulationVector2[] output,
            out int pathLength)
        {
            return TryFindPath(start, destination, radius, null, output, out pathLength);
        }

        public bool TryFindPath(
            SimulationVector2 start,
            SimulationVector2 destination,
            float radius,
            DynamicOccupancyGrid occupancy,
            SimulationVector2[] output,
            out int pathLength)
        {
            pathLength = 0;
            if (!_grid.TryGetCoordinate(start, out var startCell) ||
                !_grid.TryGetCoordinate(destination, out var goalCell) ||
                !_grid.IsWalkableForRadius(startCell, radius) ||
                !_grid.IsWalkableForRadius(goalCell, radius))
                return false;

            for (var index = 0; index < _costs.Length; index++)
            {
                _costs[index] = float.MaxValue;
                _parents[index] = -1;
                _openFlags[index] = false;
            }

            _openCount = 0;
            var startIndex = ToIndex(startCell);
            var goalIndex = ToIndex(goalCell);
            _costs[startIndex] = 0f;
            AddOpen(startIndex);

            while (_openCount > 0)
            {
                var currentIndex = TakeBest(goalCell);
                if (currentIndex == goalIndex)
                {
                    pathLength = BuildPath(_parents, startIndex, goalIndex, output);
                    return pathLength > 0;
                }

                var current = FromIndex(currentIndex);
                for (var neighborIndex = 0; neighborIndex < Neighbors.Length; neighborIndex++)
                {
                    var neighbor = new GridCoordinate(
                        current.X + Neighbors[neighborIndex].X,
                        current.Y + Neighbors[neighborIndex].Y);
                    if (!_grid.IsWalkableForRadius(neighbor, radius)) continue;

                    var index = ToIndex(neighbor);
                    var occupancyCost = occupancy == null
                        ? 0f
                        : occupancy.GetStationaryCount(neighbor) * 8f;
                    var cost = _costs[currentIndex] + 1f + occupancyCost;
                    if (cost >= _costs[index]) continue;
                    _costs[index] = cost;
                    _parents[index] = currentIndex;
                    if (!_openFlags[index]) AddOpen(index);
                }
            }

            return false;
        }

        private int TakeBest(GridCoordinate goal)
        {
            var bestOpenIndex = 0;
            var bestIndex = _open[0];
            var bestScore = _costs[bestIndex] + Heuristic(FromIndex(bestIndex), goal);
            for (var index = 1; index < _openCount; index++)
            {
                var candidate = _open[index];
                var score = _costs[candidate] + Heuristic(FromIndex(candidate), goal);
                if (score >= bestScore && (score != bestScore || candidate >= bestIndex)) continue;
                bestOpenIndex = index;
                bestIndex = candidate;
                bestScore = score;
            }

            _openCount--;
            _open[bestOpenIndex] = _open[_openCount];
            _openFlags[bestIndex] = false;
            return bestIndex;
        }

        private int BuildPath(int[] parents, int startIndex, int goalIndex, SimulationVector2[] output)
        {
            var count = 0;
            for (var index = goalIndex; index >= 0 && count < output.Length; index = parents[index])
            {
                output[count++] = _grid.GetCenter(FromIndex(index));
                if (index == startIndex) break;
            }

            var left = 0;
            var right = count - 1;
            for (; left < right; left++, right--)
            {
                var value = output[left];
                output[left] = output[right];
                output[right] = value;
            }

            return count;
        }

        private void AddOpen(int index)
        {
            _open[_openCount++] = index;
            _openFlags[index] = true;
        }

        private int ToIndex(GridCoordinate coordinate) => coordinate.Y * _grid.Width + coordinate.X;
        private GridCoordinate FromIndex(int index) => new(index % _grid.Width, index / _grid.Width);
        private static float Heuristic(GridCoordinate from, GridCoordinate to) => Math.Abs(from.X - to.X) + Math.Abs(from.Y - to.Y);
    }
}
