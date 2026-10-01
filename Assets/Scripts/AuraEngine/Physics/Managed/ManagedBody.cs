using AuraEngine.Core;

namespace AuraEngine.Physics
{
    internal sealed class ManagedBody
    {
        public int Generation;
        public bool Occupied;
        public AuraBodyType BodyType;
        public AuraPhysicsLayer Layer;
        public AuraPhysicsLayerMask CollisionMask;
        public int GroupIndex;
        public float InvMass;
        public AuraVector3 InvInertiaLocal;
        public float GravityScale;
        public float Friction;
        public float Restitution;
        public float LinearDamping;
        public float AngularDamping;
        public uint Freeze;
        public bool AllowSleeping;
        public float MaxLinearVelocity;
        public float MaxAngularVelocity;
        public AuraPose Pose;
        public AuraVector3 LinearVelocity;
        public AuraVector3 AngularVelocity;
        public AuraVector3 SurfaceVelocity;
        public bool Awake;
        public float SleepTimer;
        public ManagedShape[] Shapes;
        public ManagedAabb Aabb;
    }
}
