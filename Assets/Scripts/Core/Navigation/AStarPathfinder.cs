using System;
using System.Collections.Generic;
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

        public AStarPathfinder(NavigationGrid grid)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
        }

        public bool TryFindPath(SimulationVector2 start, SimulationVector2 destination, float radius, out SimulationVector2[] path)
        {
            path = null;
            if (!_grid.TryGetCoordinate(start, out var startCell) ||
                !_grid.TryGetCoordinate(destination, out var goalCell) ||
                !_grid.IsWalkableForRadius(startCell, radius) ||
                !_grid.IsWalkableForRadius(goalCell, radius))
                return false;

            var count = _grid.Width * _grid.Height;
            var costs = new float[count];
            var parents = new int[count];
            var open = new List<int>(count);
            for (var index = 0; index < count; index++)
            {
                costs[index] = float.MaxValue;
                parents[index] = -1;
            }

            var startIndex = ToIndex(startCell);
            var goalIndex = ToIndex(goalCell);
            costs[startIndex] = 0f;
            open.Add(startIndex);

            while (open.Count > 0)
            {
                var currentIndex = TakeBest(open, costs, goalCell);
                if (currentIndex == goalIndex)
                {
                    path = BuildPath(parents, startIndex, goalIndex);
                    return true;
                }

                var current = FromIndex(currentIndex);
                for (var neighborIndex = 0; neighborIndex < Neighbors.Length; neighborIndex++)
                {
                    var neighbor = new GridCoordinate(
                        current.X + Neighbors[neighborIndex].X,
                        current.Y + Neighbors[neighborIndex].Y);
                    if (!_grid.IsWalkableForRadius(neighbor, radius)) continue;

                    var index = ToIndex(neighbor);
                    var cost = costs[currentIndex] + 1f;
                    if (cost >= costs[index]) continue;
                    costs[index] = cost;
                    parents[index] = currentIndex;
                    if (!open.Contains(index)) open.Add(index);
                }
            }

            return false;
        }

        private int TakeBest(List<int> open, float[] costs, GridCoordinate goal)
        {
            var bestListIndex = 0;
            var bestIndex = open[0];
            var bestScore = costs[bestIndex] + Heuristic(FromIndex(bestIndex), goal);
            for (var index = 1; index < open.Count; index++)
            {
                var candidate = open[index];
                var score = costs[candidate] + Heuristic(FromIndex(candidate), goal);
                if (score >= bestScore && (score != bestScore || candidate >= bestIndex)) continue;
                bestListIndex = index;
                bestIndex = candidate;
                bestScore = score;
            }

            open.RemoveAt(bestListIndex);
            return bestIndex;
        }

        private SimulationVector2[] BuildPath(int[] parents, int startIndex, int goalIndex)
        {
            var reversed = new List<SimulationVector2>();
            for (var index = goalIndex; index >= 0; index = parents[index])
            {
                reversed.Add(_grid.GetCenter(FromIndex(index)));
                if (index == startIndex) break;
            }

            reversed.Reverse();
            return reversed.ToArray();
        }

        private int ToIndex(GridCoordinate coordinate) => coordinate.Y * _grid.Width + coordinate.X;
        private GridCoordinate FromIndex(int index) => new(index % _grid.Width, index / _grid.Width);
        private static float Heuristic(GridCoordinate from, GridCoordinate to) => Math.Abs(from.X - to.X) + Math.Abs(from.Y - to.Y);
    }
}
