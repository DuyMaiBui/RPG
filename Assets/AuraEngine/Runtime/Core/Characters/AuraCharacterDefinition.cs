namespace AuraEngine.Core
{
    public readonly struct AuraCharacterDefinition
    {
        public AuraCharacterDefinition(
            AuraPose pose,
            float radius,
            float height,
            AuraPhysicsLayer layer = default,
            AuraPhysicsLayerMask collisionMask = default,
            float mass = 0f,
            float maxSlopeAngle = 0f)
        {
            Pose = pose;
            Radius = radius;
            Height = height;
            Layer = layer;
            CollisionMask = collisionMask;
            Mass = mass;
            MaxSlopeAngle = maxSlopeAngle;
        }

        public AuraPose Pose { get; }
        public float Radius { get; }
        public float Height { get; }
        public AuraPhysicsLayer Layer { get; }
        public AuraPhysicsLayerMask CollisionMask { get; }
        public float Mass { get; }
        public float MaxSlopeAngle { get; }
    }
}
