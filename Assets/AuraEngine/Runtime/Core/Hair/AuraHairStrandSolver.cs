using System.Collections.Generic;

namespace AuraEngine.Core
{
    public sealed class AuraHairStrandSolver
    {
        public const float DefaultDamping = 0.98f;
        public const int DefaultConstraintIterations = 3;
        public const float DefaultGroundPlaneY = float.NegativeInfinity;

        private readonly AuraVerletParticles _particles;
        private readonly List<AuraVerletConstraint> _constraints;
        private readonly AuraVector3[] _strandRoots;
        private readonly float _damping;
        private readonly int _constraintIterations;
        private readonly float _groundPlaneY;

        public AuraHairStrandSolver(
            AuraHairDefinition definition,
            float damping = DefaultDamping,
            int constraintIterations = DefaultConstraintIterations,
            float groundPlaneY = DefaultGroundPlaneY)
        {
            if (definition.StrandRoots == null || definition.StrandRoots.Length == 0)
                throw AuraException.FromResult(AuraResult.InvalidDefinition, "Hair definition requires at least one strand root.");
            if (definition.PointsPerStrand < 2)
                throw AuraException.FromResult(AuraResult.InvalidDefinition, "Hair definition requires at least two points per strand.");
            if (!(definition.SegmentLength > 0f) || !float.IsFinite(definition.SegmentLength))
                throw AuraException.FromResult(AuraResult.InvalidDefinition, "Hair definition requires a positive finite segment length.");
            if (float.IsNaN(damping) || damping < 0f || damping > 1f)
                throw AuraException.FromResult(AuraResult.InvalidDefinition, "Hair solver damping must be in [0, 1].");
            if (constraintIterations < 1)
                throw AuraException.FromResult(AuraResult.InvalidDefinition, "Hair solver requires at least one constraint iteration.");

            StrandCount = definition.StrandRoots.Length;
            PointsPerStrand = definition.PointsPerStrand;
            _strandRoots = (AuraVector3[])definition.StrandRoots.Clone();
            _damping = damping;
            _constraintIterations = constraintIterations;
            _groundPlaneY = groundPlaneY;

            _particles = new AuraVerletParticles(StrandCount * PointsPerStrand);
            _constraints = new List<AuraVerletConstraint>(StrandCount * (PointsPerStrand - 1));

            for (var strand = 0; strand < StrandCount; strand++)
            {
                var root = definition.InitialPose.TransformPoint(definition.StrandRoots[strand]);
                for (var point = 0; point < PointsPerStrand; point++)
                {
                    var index = strand * PointsPerStrand + point;
                    var position = root + new AuraVector3(0f, -definition.SegmentLength * point, 0f);
                    _particles.Positions[index] = position;
                    _particles.PreviousPositions[index] = position;
                }

                _particles.Pin(strand * PointsPerStrand);

                for (var point = 0; point < PointsPerStrand - 1; point++)
                {
                    var index = strand * PointsPerStrand + point;
                    _constraints.Add(new AuraVerletConstraint(index, index + 1, definition.SegmentLength));
                }
            }
        }

        public int StrandCount { get; }
        public int PointsPerStrand { get; }

        /// <summary>
        /// Moves every pinned strand root to the given pose so hair follows its owner.
        /// </summary>
        public void SetRootPose(AuraPose pose)
        {
            for (var strand = 0; strand < StrandCount; strand++)
                _particles.MovePinned(strand * PointsPerStrand, pose.TransformPoint(_strandRoots[strand]));
        }

        public void Step(float deltaTime, AuraVector3 gravity, AuraVector3 wind) =>
            _particles.Step(deltaTime, gravity, wind, _damping, _constraintIterations, _constraints, _groundPlaneY);

        public AuraVector3 GetPoint(int strandIndex, int pointIndex)
        {
            if (strandIndex < 0 || strandIndex >= StrandCount)
                throw new System.ArgumentOutOfRangeException(nameof(strandIndex), strandIndex, "Strand index out of range.");
            if (pointIndex < 0 || pointIndex >= PointsPerStrand)
                throw new System.ArgumentOutOfRangeException(nameof(pointIndex), pointIndex, "Point index out of range.");

            return _particles.Positions[strandIndex * PointsPerStrand + pointIndex];
        }
    }
}
