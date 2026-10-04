using AuraEngine.Core;
using AuraEngine.Physics;

namespace AuraEngine.Simulation
{
    /* State saved while a body is frozen by AuraHitStop. */
    internal struct AuraHitStopEntry
    {
        public PhysicsBodyId Body;
        public int RemainingTicks;
        public AuraVector3 LinearVelocity;
        public AuraVector3 AngularVelocity;
        public float GravityScale;
    }
}
