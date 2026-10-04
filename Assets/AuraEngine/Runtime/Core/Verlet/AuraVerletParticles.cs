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
    public sealed class AuraVerletParticles : IVerletSolverBackend
    {
        public AuraVerletParticles(int count)
        {
            if (count <= 0)
                throw new ArgumentOutOfRangeException(nameof(count), count, "Particle count must be positive.");

            Count = count;
            Positions = new AuraVector3[count];
            PreviousPositions = new AuraVector3[count];
            InverseMass = new float[count];
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

            if (constraints != null)
            {
                for (var iteration = 0; iteration < constraintIterations; iteration++)
                {
                    for (var index = 0; index < constraints.Count; index++)
                        Relax(constraints[index]);
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
