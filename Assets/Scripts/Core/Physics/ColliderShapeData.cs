using System;
using RPG.Simulation.Contracts;

namespace RPG.Core.Physics
{
    public readonly struct ColliderShapeData
    {
        public ColliderShapeData(
            CollisionShape shape,
            SimulationVector2 localOffset,
            float localRotationRadians,
            ColliderMode mode,
            ColliderFilter filter)
        {
            Shape = shape;
            LocalOffset = localOffset;
            LocalRotationRadians = localRotationRadians;
            Mode = mode;
            Filter = filter;
        }

        public CollisionShape Shape { get; }
        public SimulationVector2 LocalOffset { get; }
        public float LocalRotationRadians { get; }
        public ColliderMode Mode { get; }
        public ColliderFilter Filter { get; }

        public bool IsQueryable(ColliderMode mode, ColliderFilter filter) =>
            Mode == mode && Filter.CanInteractWith(filter);
    }
}
