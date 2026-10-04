namespace AuraEngine.Core
{
    /* A force field zone. Bodies are affected while their centre of mass is inside the volume and their
       layer is in LayerMask. Directional: Vector is the acceleration/force. Radial: Strength pulls toward the
       centre (negative pushes away), MinRadius softens the core, MaxRadius cuts the reach (0 = zone extent).
       Drag: Vector is the wind velocity and Strength the drag rate. */
    public readonly struct AuraForceFieldDefinition
    {
        public AuraForceFieldDefinition(
            AuraForceFieldShape shape,
            AuraForceFieldKind kind,
            AuraPose pose,
            float radius,
            AuraVector3 halfExtents,
            AuraVector3 vector,
            float strength,
            AuraForceFieldMode mode = AuraForceFieldMode.Acceleration,
            AuraForceFieldFalloff falloff = AuraForceFieldFalloff.None,
            float minRadius = 0f,
            float maxRadius = 0f,
            AuraPhysicsLayerMask? layerMask = null,
            bool enabled = true)
        {
            Shape = shape;
            Kind = kind;
            Pose = pose;
            Radius = radius;
            HalfExtents = halfExtents;
            Vector = vector;
            Strength = strength;
            Mode = mode;
            Falloff = falloff;
            MinRadius = minRadius;
            MaxRadius = maxRadius;
            LayerMask = layerMask ?? AuraPhysicsLayerMask.All;
            Enabled = enabled;
        }

        public AuraForceFieldShape Shape { get; }
        public AuraForceFieldKind Kind { get; }
        public AuraPose Pose { get; }
        public float Radius { get; }
        public AuraVector3 HalfExtents { get; }
        public AuraVector3 Vector { get; }
        public float Strength { get; }
        public AuraForceFieldMode Mode { get; }
        public AuraForceFieldFalloff Falloff { get; }
        public float MinRadius { get; }
        public float MaxRadius { get; }
        public AuraPhysicsLayerMask LayerMask { get; }
        public bool Enabled { get; }

        public AuraForceFieldDefinition WithPose(AuraPose pose) => new AuraForceFieldDefinition(
            Shape, Kind, pose, Radius, HalfExtents, Vector, Strength, Mode, Falloff, MinRadius, MaxRadius, LayerMask, Enabled);

        public AuraForceFieldDefinition WithEnabled(bool enabled) => new AuraForceFieldDefinition(
            Shape, Kind, Pose, Radius, HalfExtents, Vector, Strength, Mode, Falloff, MinRadius, MaxRadius, LayerMask, enabled);
    }
}
