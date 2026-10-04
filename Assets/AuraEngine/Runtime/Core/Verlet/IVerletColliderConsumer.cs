using System.Collections.Generic;

namespace AuraEngine.Core
{
    /// <summary>
    /// Consumer-owned contract for Verlet solvers that collide against world colliders
    /// described by <see cref="AuraVerletCollider"/>. The provider (a view/bridge) refreshes
    /// its list each fixed step and hands the same list instance in, so no per-step allocation occurs.
    /// </summary>
    public interface IVerletColliderConsumer
    {
        /// <summary>
        /// Sets the colliders consumed by every subsequent step. The list is read, never stored by value,
        /// so the provider may mutate and re-pass it. At most <see cref="AuraVerletParticles.MaxColliders"/> are used.
        /// Pass null to disable collision.
        /// </summary>
        void SetColliders(IReadOnlyList<AuraVerletCollider> colliders);

        /// <summary>
        /// 2D mode: when enabled every free particle is held on the plane Z = <paramref name="z"/>
        /// (planar colliders only act in XY).
        /// </summary>
        void SetPlaneLock(bool enabled, float z);
    }
}
