using System;
using System.Collections.Generic;

namespace AuraEngine.Core
{
    /// <summary>
    /// Deterministic fixed-step CPU Verlet particle solver over shared buffers.
    /// Buffers are laid out as parallel arrays (structure-of-arrays friendly) so a
    /// future GPU compute backend can upload them directly as structured buffers.
    /// MathF only, no RNG, fixed iteration counts, explicit delta time.
    /// Holds no static state.
    /// </summary>
    public sealed class AuraVerletParticles : IVerletSolverBackend, IVerletColliderConsumer
    {
        /// <summary>Upper bound on colliders consulted per step (bounds the collision cost).</summary>
        public const int MaxColliders = 32;

        private const float ContactMargin = 1e-5f;

        private readonly float[] _contactDepth;
        private IReadOnlyList<AuraVerletCollider> _colliders;
        private bool _lockPlane;
        private float _planeZ;

        public AuraVerletParticles(int count)
        {
            if (count <= 0)
                throw new ArgumentOutOfRangeException(nameof(count), count, "Particle count must be positive.");

            Count = count;
            Positions = new AuraVector3[count];
            PreviousPositions = new AuraVector3[count];
            InverseMass = new float[count];
            _contactDepth = new float[count];
            for (var index = 0; index < count; index++)
                InverseMass[index] = 1f;
        }

        public int Count { get; }
        public AuraVector3[] Positions { get; }
        public AuraVector3[] PreviousPositions { get; }
        public float[] InverseMass { get; }

        public void Pin(int index)
        {
            if (index < 0 || index >= Count)
                throw new ArgumentOutOfRangeException(nameof(index), index, "Particle index out of range.");

            InverseMass[index] = 0f;
            PreviousPositions[index] = Positions[index];
        }

        /// <summary>
        /// Teleports a pinned particle (kinematic anchor) so it follows a moving owner.
        /// Previous position is reset so the anchor carries no stored velocity.
        /// </summary>
        public void MovePinned(int index, AuraVector3 position)
        {
            if (index < 0 || index >= Count)
                throw new ArgumentOutOfRangeException(nameof(index), index, "Particle index out of range.");
            if (InverseMass[index] > 0f)
                throw new InvalidOperationException("Only pinned particles can be moved kinematically.");

            Positions[index] = position;
            PreviousPositions[index] = position;
        }

        public void Step(
            float deltaTime,
            AuraVector3 gravity,
            AuraVector3 wind,
            float damping,
            int constraintIterations,
            IReadOnlyList<AuraVerletConstraint> constraints,
            float groundPlaneY)
        {
            var acceleration = gravity + wind;
            var scaledAcceleration = acceleration * (deltaTime * deltaTime);

            for (var index = 0; index < Count; index++)
            {
                if (InverseMass[index] <= 0f)
                {
                    PreviousPositions[index] = Positions[index];
                    continue;
                }

                var current = Positions[index];
                var velocity = (current - PreviousPositions[index]) * damping;
                PreviousPositions[index] = current;
                Positions[index] = current + velocity + scaledAcceleration;
            }

            LockToPlane();

            var hasColliders = _colliders != null && _colliders.Count > 0;
            if (hasColliders)
                Array.Clear(_contactDepth, 0, _contactDepth.Length);
            if (constraints != null || hasColliders)
            {
                for (var iteration = 0; iteration < constraintIterations; iteration++)
                {
                    if (constraints != null)
                    {
                        for (var index = 0; index < constraints.Count; index++)
                            Relax(constraints[index]);
                    }

                    LockToPlane();
                    if (hasColliders)
                        Collide(deltaTime, iteration == constraintIterations - 1);
                }
            }

            for (var index = 0; index < Count; index++)
            {
                var position = Positions[index];
                if (position.Y < groundPlaneY)
                    Positions[index] = new AuraVector3(position.X, groundPlaneY, position.Z);
            }
        }

        void IVerletSolverBackend.Step(
            float deltaTime,
            AuraVector3 gravity,
            AuraVector3 wind,
            float damping,
            int constraintIterations,
            IReadOnlyList<AuraVerletConstraint> constraints,
            float groundPlaneY) =>
            Step(deltaTime, gravity, wind, damping, constraintIterations, constraints, groundPlaneY);

        void IVerletColliderConsumer.SetColliders(IReadOnlyList<AuraVerletCollider> colliders) => _colliders = colliders;

        void IVerletColliderConsumer.SetPlaneLock(bool enabled, float z)
        {
            _lockPlane = enabled;
            _planeZ = z;
        }

        private void LockToPlane()
        {
            if (!_lockPlane)
                return;

            for (var index = 0; index < Count; index++)
            {
                if (InverseMass[index] <= 0f)
                    continue;

                var position = Positions[index];
                var previous = PreviousPositions[index];
                Positions[index] = new AuraVector3(position.X, position.Y, _planeZ);
                PreviousPositions[index] = new AuraVector3(previous.X, previous.Y, _planeZ);
            }
        }

        /// <summary>
        /// Projects free particles out of every collider's skin shell. Pinned particles never move.
        /// On the final pass the contact velocity is also resolved: velocity into the surface is
        /// removed (no bounce) and the tangential velocity relative to the collider is limited by its
        /// Coulomb friction, so moving colliders carry the particles.
        /// </summary>
        private void Collide(float deltaTime, bool resolveVelocity)
        {
            var colliders = _colliders;
            var count = colliders.Count < MaxColliders ? colliders.Count : MaxColliders;
            for (var index = 0; index < Count; index++)
            {
                if (InverseMass[index] <= 0f)
                    continue;

                for (var c = 0; c < count; c++)
                {
                    var collider = colliders[c];
                    AuraVerletCollisions.Query(collider, Positions[index], out var normal, out var depth);
                    if (depth <= -ContactMargin)
                        continue;

                    var position = Positions[index];
                    if (depth > 0f)
                    {
                        position += normal * depth;
                        Positions[index] = position;
                        _contactDepth[index] += depth;
                    }

                    if (!resolveVelocity)
                        continue;

                    var previous = PreviousPositions[index];
                    var relative = (position - previous) - collider.Velocity * deltaTime;
                    var normalPart = AuraVector3.Dot(relative, normal);
                    if (normalPart < 0f)
                    {
                        previous += normal * normalPart;
                        relative -= normal * normalPart;
                        normalPart = 0f;
                    }

                    // Coulomb friction: the stick/slide limit is friction times this step's push-out
                    // (the normal impulse), so resting particles hold on slopes up to atan(friction)
                    // and sliding particles lose at most that much tangential displacement per step.
                    var tangent = relative - normal * normalPart;
                    var tangentLength = tangent.Length;
                    var limit = collider.Friction * _contactDepth[index];
                    if (tangentLength > 1e-9f)
                        PreviousPositions[index] = previous + tangent * (tangentLength <= limit ? 1f : limit / tangentLength);
                    else
                        PreviousPositions[index] = previous;
                }
            }
        }

        private void Relax(AuraVerletConstraint constraint)
        {
            var weightA = InverseMass[constraint.ParticleA];
            var weightB = InverseMass[constraint.ParticleB];
            var weightSum = weightA + weightB;
            if (weightSum <= 0f)
                return;

            var delta = Positions[constraint.ParticleB] - Positions[constraint.ParticleA];
            var distanceSquared = delta.LengthSquared;
            if (distanceSquared <= 1e-12f)
                return;

            var distance = MathF.Sqrt(distanceSquared);
            var correction = (distance - constraint.RestLength) / distance;
            Positions[constraint.ParticleA] += delta * (correction * (weightA / weightSum));
            Positions[constraint.ParticleB] -= delta * (correction * (weightB / weightSum));
        }
    }
}
