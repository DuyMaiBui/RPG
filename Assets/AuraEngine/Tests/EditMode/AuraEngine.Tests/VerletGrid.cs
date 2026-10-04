using System.Collections.Generic;
using AuraEngine.Core;

namespace AuraEngine.Tests
{
    /// <summary>Unpinned horizontal cloth grid built on the shared particle solver.</summary>
    public sealed class VerletGrid
    {
        private readonly int _width;
        private readonly List<AuraVerletConstraint> _constraints = new List<AuraVerletConstraint>();

        public VerletGrid(int width, int height, float spacing, float y)
        {
            _width = width;
            Particles = new AuraVerletParticles(width * height);
            for (var row = 0; row < height; row++)
            {
                for (var column = 0; column < width; column++)
                {
                    var index = row * width + column;
                    // Deterministic sub-grid jitter so the sheet is not perfectly symmetric.
                    var position = new AuraVector3(column * spacing, y + 0.01f * ((index * 7) % 5), row * spacing);
                    Particles.Positions[index] = position;
                    Particles.PreviousPositions[index] = position;
                    if (column + 1 < width)
                        _constraints.Add(new AuraVerletConstraint(index, index + 1, spacing));
                    if (row + 1 < height)
                        _constraints.Add(new AuraVerletConstraint(index, index + width, spacing));
                }
            }
        }

        public AuraVerletParticles Particles { get; }

        public int Index(int column, int row) => row * _width + column;

        public void SetColliders(AuraVerletCollider collider) =>
            ((IVerletColliderConsumer)Particles).SetColliders(new List<AuraVerletCollider> { collider });

        public void Step(AuraVector3 acceleration) =>
            Particles.Step(1f / 60f, acceleration, AuraVector3.Zero, 0.98f, 4, _constraints, float.NegativeInfinity);

        public float WorstDepth(AuraVerletCollider collider)
        {
            var worst = float.MinValue;
            for (var i = 0; i < Particles.Count; i++)
            {
                AuraVerletCollisions.Query(collider, Particles.Positions[i], out _, out var depth);
                worst = System.Math.Max(worst, depth);
            }

            return worst;
        }
    }
}
