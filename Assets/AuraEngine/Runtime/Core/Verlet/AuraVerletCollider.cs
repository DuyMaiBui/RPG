using System;

namespace AuraEngine.Core
{
    /// <summary>
    /// Engine-free world-space collider description consumed by Verlet cloth and hair.
    /// Capsules and boxes are oriented by <see cref="Rotation"/>; a capsule's axis is its local Y.
    /// A particle is kept at least <see cref="Skin"/> outside the surface. In planar (2D) mode the
    /// shape is an infinite extrusion along Z: only X/Y of particle and shape matter and the
    /// contact normal has no Z component (rotation is assumed to be about Z).
    /// </summary>
    public readonly struct AuraVerletCollider
    {
        private AuraVerletCollider(
            AuraVerletColliderKind kind,
            AuraVector3 position,
            AuraQuaternion rotation,
            float radius,
            float halfSegment,
            AuraVector3 halfExtents,
            AuraVector3 normal,
            float friction,
            float skin,
            AuraVector3 velocity,
            bool planar)
        {
            Kind = kind;
            Position = position;
            Rotation = rotation;
            Radius = radius;
            HalfSegment = halfSegment;
            HalfExtents = halfExtents;
            Normal = normal;
            Friction = friction < 0f ? 0f : (friction > 1f ? 1f : friction);
            Skin = skin < 0f ? 0f : skin;
            Velocity = velocity;
            Planar = planar;
        }

        public AuraVerletColliderKind Kind { get; }
        public AuraVector3 Position { get; }
        public AuraQuaternion Rotation { get; }
        public float Radius { get; }
        /// <summary>Half length of the capsule core segment (total height / 2 - radius).</summary>
        public float HalfSegment { get; }
        public AuraVector3 HalfExtents { get; }
        /// <summary>World-space unit plane normal.</summary>
        public AuraVector3 Normal { get; }
        /// <summary>Tangential damping per step in [0, 1] (1 sticks to the collider surface).</summary>
        public float Friction { get; }
        public float Skin { get; }
        /// <summary>World linear velocity (units/second) used so moving colliders carry particles by friction.</summary>
        public AuraVector3 Velocity { get; }
        public bool Planar { get; }

        public static AuraVerletCollider Sphere(
            AuraVector3 center, float radius, float friction = 0.5f, float skin = 0.01f,
            AuraVector3 velocity = default, bool planar = false) =>
            new AuraVerletCollider(AuraVerletColliderKind.Sphere, center, AuraQuaternion.Identity, radius, 0f,
                AuraVector3.Zero, AuraVector3.UnitY, friction, skin, velocity, planar);

        public static AuraVerletCollider Capsule(
            AuraVector3 center, AuraQuaternion rotation, float radius, float totalHeight, float friction = 0.5f,
            float skin = 0.01f, AuraVector3 velocity = default, bool planar = false) =>
            new AuraVerletCollider(AuraVerletColliderKind.Capsule, center, rotation, radius,
                Math.Max(0f, totalHeight * 0.5f - radius), AuraVector3.Zero, AuraVector3.UnitY, friction, skin, velocity, planar);

        public static AuraVerletCollider Box(
            AuraVector3 center, AuraQuaternion rotation, AuraVector3 halfExtents, float friction = 0.5f,
            float skin = 0.01f, AuraVector3 velocity = default, bool planar = false) =>
            new AuraVerletCollider(AuraVerletColliderKind.Box, center, rotation, 0f, 0f, halfExtents,
                AuraVector3.UnitY, friction, skin, velocity, planar);

        public static AuraVerletCollider Plane(
            AuraVector3 point, AuraVector3 normal, float friction = 0.5f, float skin = 0.01f,
            AuraVector3 velocity = default, bool planar = false)
        {
            var unit = normal.Normalized();
            if (unit.LengthSquared <= 0f)
                unit = AuraVector3.UnitY;
            return new AuraVerletCollider(AuraVerletColliderKind.Plane, point, AuraQuaternion.Identity, 0f, 0f,
                AuraVector3.Zero, unit, friction, skin, velocity, planar);
        }
    }
}
