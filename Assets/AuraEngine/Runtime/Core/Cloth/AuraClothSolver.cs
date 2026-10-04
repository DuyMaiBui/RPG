using System;
using System.Collections.Generic;

namespace AuraEngine.Core
{
    public sealed class AuraClothSolver
    {
        public const int DefaultConstraintIterations = 3;
        public const float DefaultGroundPlaneY = float.NegativeInfinity;

        private readonly AuraVerletParticles _particles;
        private readonly List<AuraVerletConstraint> _constraints;
        private readonly AuraClothDefinition _definition;
        private readonly int _constraintIterations;
        private readonly float _groundPlaneY;

        public AuraClothSolver(
            AuraClothDefinition definition,
            int constraintIterations = DefaultConstraintIterations,
            float groundPlaneY = DefaultGroundPlaneY)
        {
            AuraException.ThrowIfFailed(definition.Validate(), "Cloth definition is invalid.");
            if (constraintIterations < 1)
                throw AuraException.FromResult(AuraResult.InvalidDefinition, "Cloth solver requires at least one constraint iteration.");

            _definition = definition;
            _constraintIterations = constraintIterations;
            _groundPlaneY = groundPlaneY;
            Width = definition.Width;
            Height = definition.Height;

            _particles = new AuraVerletParticles(Width * Height);
            _constraints = new List<AuraVerletConstraint>((Width - 1) * Height + Width * (Height - 1));

            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    var index = y * Width + x;
                    var local = new AuraVector3(x * definition.Spacing, -y * definition.Spacing, 0f);
                    var position = definition.Origin.TransformPoint(local);
                    _particles.Positions[index] = position;
                    _particles.PreviousPositions[index] = position;
                }
            }

            for (var x = 0; x < Width; x++)
                _particles.Pin(x);

            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    var index = y * Width + x;
                    if (x + 1 < Width)
                        _constraints.Add(new AuraVerletConstraint(index, index + 1, definition.Spacing));
                    if (y + 1 < Height)
                        _constraints.Add(new AuraVerletConstraint(index, index + Width, definition.Spacing));
                }
            }
        }

        public int Width { get; }
        public int Height { get; }
        public int ParticleCount => Width * Height;

        /// <summary>
        /// Moves the pinned top row to a new origin pose so hanging cloth follows its owner.
        /// Free particles are left to the solver, so the cloth trails the motion.
        /// </summary>
        public void SetOrigin(AuraPose origin)
        {
            for (var x = 0; x < Width; x++)
                _particles.MovePinned(x, origin.TransformPoint(new AuraVector3(x * _definition.Spacing, 0f, 0f)));
        }

        public void Step(float deltaTime, AuraVector3 gravity, AuraVector3 wind) =>
            _particles.Step(
                deltaTime,
                gravity * _definition.GravityScale,
                wind,
                _definition.Damping,
                _constraintIterations,
                _constraints,
                _groundPlaneY);

        public AuraVector3 GetPosition(int x, int y)
        {
            if (x < 0 || x >= Width)
                throw new ArgumentOutOfRangeException(nameof(x), x, "Cloth column out of range.");
            if (y < 0 || y >= Height)
                throw new ArgumentOutOfRangeException(nameof(y), y, "Cloth row out of range.");

            return _particles.Positions[y * Width + x];
        }

        public AuraVector3[] GetPositions()
        {
            var copy = new AuraVector3[ParticleCount];
            Array.Copy(_particles.Positions, copy, ParticleCount);
            return copy;
        }
    }
}
