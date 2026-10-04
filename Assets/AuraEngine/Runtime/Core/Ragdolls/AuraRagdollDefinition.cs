namespace AuraEngine.Core
{
    public readonly struct AuraRagdollDefinition
    {
        public AuraRagdollDefinition(AuraRigDefinition rig, AuraPhysicsBodyDefinition[] bodies, AuraJointDefinition[] jointsToParent, uint collisionGroup = 0)
        { Rig = rig; Bodies = bodies; JointsToParent = jointsToParent; CollisionGroup = collisionGroup; }
        public AuraRigDefinition Rig { get; }
        public AuraPhysicsBodyDefinition[] Bodies { get; }
        public AuraJointDefinition[] JointsToParent { get; }
        public uint CollisionGroup { get; }
    }
}
