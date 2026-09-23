using System;
using RPG.Simulation.Contracts;

namespace RPG.Core.Navigation
{
    public sealed class NavigationGrid
    {
        private readonly bool[] _blocked;
        private readonly bool[] _baseBlocked;

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

        public void ResetObstacles()
        {
            Array.Copy(_baseBlocked, _blocked, _blocked.Length);
            Revision++;
        }

        public void ApplyObstacle(NavigationObstacle obstacle)
        {
            var minX = (int)MathF.Floor((obstacle.Center.X - obstacle.HalfExtents.X - Origin.X) / CellSize);
            var maxX = (int)MathF.Floor((obstacle.Center.X + obstacle.HalfExtents.X - Origin.X) / CellSize);
            var minY = (int)MathF.Floor((obstacle.Center.Y - obstacle.HalfExtents.Y - Origin.Y) / CellSize);
            var maxY = (int)MathF.Floor((obstacle.Center.Y + obstacle.HalfExtents.Y - Origin.Y) / CellSize);
            for (var y = minY; y <= maxY; y++)
            {
                for (var x = minX; x <= maxX; x++)
                {
                    if (x >= 0 && x < Width && y >= 0 && y < Height)
                        _blocked[y * Width + x] = true;
                }
            }

            Revision++;
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
