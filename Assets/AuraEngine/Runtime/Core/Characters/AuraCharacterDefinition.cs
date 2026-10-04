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
            float maxSlopeAngle = 0f,
            float stepHeight = 0f)
        {
            Pose = pose;
            Radius = radius;
            Height = height;
            Layer = layer;
            CollisionMask = collisionMask;
            Mass = mass;
            MaxSlopeAngle = maxSlopeAngle;
            StepHeight = stepHeight;
        }

        public AuraPose Pose { get; }
        public float Radius { get; }
        public float Height { get; }
        public AuraPhysicsLayer Layer { get; }
        public AuraPhysicsLayerMask CollisionMask { get; }
        public float Mass { get; }
        public float MaxSlopeAngle { get; }

        /* Largest ledge the Plane2D mover steps up while walking; 0 disables stepping. The 3D backend ignores it. */
        public float StepHeight { get; }
    }
}
