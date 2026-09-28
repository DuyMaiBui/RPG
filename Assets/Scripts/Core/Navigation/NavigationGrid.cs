using System;
using System.Collections.Generic;
using RPG.Core.Physics;
using RPG.Simulation.Contracts;

namespace RPG.Core.Navigation
{
    public sealed class NavigationGrid
    {
        private readonly bool[] _blocked;
        private readonly bool[] _baseBlocked;
        private readonly List<NavigationObstacle> _obstacles = new();

        public NavigationGrid(int width, int height, float cellSize, SimulationVector2 origin, bool[] blocked = null)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            if (cellSize <= 0f) throw new ArgumentOutOfRangeException(nameof(cellSize));
            if (blocked != null && blocked.Length != width * height)
                throw new ArgumentException("Blocked cell data has the wrong size.", nameof(blocked));

            Width = width;
            Height = height;
            CellSize = cellSize;
            Origin = origin;
            _blocked = blocked == null ? new bool[width * height] : (bool[])blocked.Clone();
            _baseBlocked = (bool[])_blocked.Clone();
            Revision = 0;
        }

        public int Width { get; }
        public int Height { get; }
        public int CellCount => Width * Height;
        public float CellSize { get; }
        public SimulationVector2 Origin { get; }
        public int Revision { get; private set; }

        public bool IsWalkable(GridCoordinate coordinate) =>
            coordinate.X >= 0 && coordinate.X < Width && coordinate.Y >= 0 && coordinate.Y < Height &&
            !_blocked[coordinate.Y * Width + coordinate.X];

        public bool TryGetCoordinate(SimulationVector2 position, out GridCoordinate coordinate)
        {
            var x = (int)MathF.Floor((position.X - Origin.X) / CellSize);
            var y = (int)MathF.Floor((position.Y - Origin.Y) / CellSize);
            coordinate = new GridCoordinate(x, y);
            return x >= 0 && x < Width && y >= 0 && y < Height;
        }

        public SimulationVector2 GetCenter(GridCoordinate coordinate) =>
            new(Origin.X + (coordinate.X + 0.5f) * CellSize, Origin.Y + (coordinate.Y + 0.5f) * CellSize);

        public bool IsDirectPathWalkable(SimulationVector2 start, SimulationVector2 destination, float radius)
        {
            var delta = destination - start;
            var distance = MathF.Sqrt(delta.LengthSquared);
            var steps = Math.Max(1, (int)MathF.Ceiling(distance / MathF.Max(CellSize * 0.5f, 0.01f)));
            for (var step = 0; step <= steps; step++)
            {
                var position = start + delta * (step / (float)steps);
                if (!TryGetCoordinate(position, out var coordinate) || !IsWalkableForRadius(coordinate, radius))
                    return false;
            }

            return true;
        }

        public void ResetObstacles()
        {
            Array.Copy(_baseBlocked, _blocked, _blocked.Length);
            _obstacles.Clear();
            Revision++;
        }

        public void ApplyObstacle(NavigationObstacle obstacle)
        {
            _obstacles.Add(obstacle);
            var bounds = obstacle.Shape.GetBoundsHalfExtents();
            var minX = (int)MathF.Floor((obstacle.Center.X - bounds.X - Origin.X) / CellSize);
            var maxX = (int)MathF.Floor((obstacle.Center.X + bounds.X - Origin.X) / CellSize);
            var minY = (int)MathF.Floor((obstacle.Center.Y - bounds.Y - Origin.Y) / CellSize);
            var maxY = (int)MathF.Floor((obstacle.Center.Y + bounds.Y - Origin.Y) / CellSize);
            for (var y = minY; y <= maxY; y++)
            {
                for (var x = minX; x <= maxX; x++)
                {
                    if (x >= 0 && x < Width && y >= 0 && y < Height &&
                        obstacle.Shape.IntersectsAabb(
                            obstacle.Center,
                            GetCenter(new GridCoordinate(x, y)),
                            new SimulationVector2(CellSize * 0.5f, CellSize * 0.5f)))
                        _blocked[y * Width + x] = true;
                }
            }

            Revision++;
        }

        public bool IsPositionWalkable(SimulationVector2 position, float radius)
        {
            if (!TryGetCoordinate(position, out var coordinate) || !IsWalkableForRadius(coordinate, radius))
                return false;

            var body = CollisionShape.Circle(MathF.Max(0f, radius));
            for (var index = 0; index < _obstacles.Count; index++)
            {
                var obstacle = _obstacles[index];
                if (CollisionShapeQueries.Overlaps(body, position, obstacle.Shape, obstacle.Center))
                    return false;
            }

            return true;
        }

        public SimulationVector2 ResolveMovement(
            SimulationVector2 start,
            SimulationVector2 destination,
            float radius)
        {
            if (!IsPositionWalkable(start, radius)) return start;
            IsDirectMovementWalkable(start, destination, radius, out var lastSafe);
            return lastSafe;
        }

        private bool IsDirectMovementWalkable(
            SimulationVector2 start,
            SimulationVector2 destination,
            float radius,
            out SimulationVector2 lastSafe)
        {
            var delta = destination - start;
            var distance = MathF.Sqrt(delta.LengthSquared);
            var steps = Math.Max(1, (int)MathF.Ceiling(distance / MathF.Max(MathF.Max(radius, 0.05f) * 0.5f, 0.05f)));
            lastSafe = start;
            for (var step = 1; step <= steps; step++)
            {
                var position = start + delta * (step / (float)steps);
                if (!IsPositionWalkable(position, radius))
                    return false;

                lastSafe = position;
            }

            return true;
        }

        public SimulationVector2 ClampInside(SimulationVector2 position, float radius)
        {
            var padding = MathF.Max(0f, radius);
            return new SimulationVector2(
                MathF.Max(Origin.X + padding, MathF.Min(Origin.X + Width * CellSize - padding, position.X)),
                MathF.Max(Origin.Y + padding, MathF.Min(Origin.Y + Height * CellSize - padding, position.Y)));
        }

        public bool IsWalkableForRadius(GridCoordinate coordinate, float radius)
        {
            if (!IsWalkable(coordinate)) return false;
            var clearance = MathF.Max(0f, CellSize * 0.5f);
            return radius <= clearance;
        }
    }
}
