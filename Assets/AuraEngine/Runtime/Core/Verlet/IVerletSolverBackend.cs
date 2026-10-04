using System.Collections.Generic;

namespace AuraEngine.Core
{
    /// <summary>
    /// Consumer-owned backend contract for stepping shared Verlet particle buffers.
    /// The CPU solver (<see cref="AuraVerletParticles"/>) is currently the single
    /// implementation. A future GPU compute backend plugs in here by implementing
    /// this same interface over the shared buffers (upload positions/previousPositions/
    /// inverseMass, dispatch integration plus constraint relaxation, read back),
    /// with no changes required in the hair or cloth domain solvers.
    /// </summary>
    public interface IVerletSolverBackend
    {
        void Step(
            float deltaTime,
            AuraVector3 gravity,
            AuraVector3 wind,
            float damping,
            int constraintIterations,
            IReadOnlyList<AuraVerletConstraint> constraints,
            float groundPlaneY);
    }
}
