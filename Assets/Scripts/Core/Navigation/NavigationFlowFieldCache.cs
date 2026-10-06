using System;
using RPG.Simulation.Contracts;

namespace RPG.Core.Navigation
{
    public sealed class NavigationFlowFieldCache
    {
        private const int Capacity = 8;
        private readonly NavigationGrid _grid;
        private readonly NavigationFlowField[] _fields = new NavigationFlowField[Capacity];
        private readonly GridCoordinate[] _targets = new GridCoordinate[Capacity];
        private readonly float[] _radii = new float[Capacity];
        private readonly int[] _revisions = new int[Capacity];
        private int _nextReplacement;

        public NavigationFlowFieldCache(NavigationGrid grid)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            for (var index = 0; index < Capacity; index++)
                _revisions[index] = -1;
        }

        public bool TryGetDirection(
            SimulationVector2 position,
            SimulationVector2 destination,
            float radius,
            out SimulationVector2 direction)
        {
            direction = SimulationVector2.Zero;
            if (!_grid.TryGetCoordinate(destination, out var target)) return false;

            var field = GetOrBuild(target, radius);
            return field.TryGetDirection(position, out direction);
        }

        private NavigationFlowField GetOrBuild(GridCoordinate target, float radius)
        {
            for (var index = 0; index < Capacity; index++)
            {
                if (_fields[index] != null && _targets[index].Equals(target) &&
                    _revisions[index] == _grid.Revision && SimulationMath.Abs(_radii[index] - radius) <= 0.001f)
                    return _fields[index];
            }

            var slot = _nextReplacement++ % Capacity;
            _fields[slot] ??= new NavigationFlowField(_grid);
            _targets[slot] = target;
            _radii[slot] = radius;
            _revisions[slot] = _grid.Revision;
            _fields[slot].Build(target, radius);
            return _fields[slot];
        }
    }
}
