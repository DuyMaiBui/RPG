using System;

namespace AuraEngine.Core
{
    public readonly struct AuraPhysicsBodyDefinition
    {
        public AuraPhysicsBodyDefinition(
            AuraBodyType type,
            AuraPose initialPose,
            AuraPhysicsLayer layer,
            AuraPhysicsLayerMask collisionMask,
            AuraPhysicsShapeDefinition[] shapes,
            float mass = 1f,
            float gravityScale = 1f,
            int groupIndex = 0,
            AuraPhysicsMaterialDefinition material = default,
            AuraVector3 initialLinearVelocity = default,
            AuraVector3 initialAngularVelocity = default,
            float linearDamping = 0f,
            float angularDamping = 0f,
            AuraBodyFreezeFlags freeze = AuraBodyFreezeFlags.None,
            AuraVector3 centerOfMass = default,
            float inertiaMultiplier = 1f,
            AuraBodyCollisionDetection collisionDetection = AuraBodyCollisionDetection.Discrete,
            bool allowSleeping = true,
            float maxLinearVelocity = 0f,
            float maxAngularVelocity = 0f)
        {
            Type = type;
            InitialPose = initialPose;
            Layer = layer;
            CollisionMask = collisionMask;
            Shapes = shapes;
            Mass = mass;
            GravityScale = gravityScale;
            GroupIndex = groupIndex;
            Material = material;
            InitialLinearVelocity = initialLinearVelocity;
            InitialAngularVelocity = initialAngularVelocity;
            LinearDamping = linearDamping;
            AngularDamping = angularDamping;
            Freeze = freeze;
            CenterOfMass = centerOfMass;
            InertiaMultiplier = inertiaMultiplier;
            CollisionDetection = collisionDetection;
            AllowSleeping = allowSleeping;
            MaxLinearVelocity = maxLinearVelocity;
            MaxAngularVelocity = maxAngularVelocity;
        }

        public AuraBodyType Type { get; }
        public AuraPose InitialPose { get; }
        public AuraPhysicsLayer Layer { get; }
        public AuraPhysicsLayerMask CollisionMask { get; }
        public AuraPhysicsShapeDefinition[] Shapes { get; }
        public float Mass { get; }
        public float GravityScale { get; }
        public int GroupIndex { get; }
        public AuraPhysicsMaterialDefinition Material { get; }
        public AuraVector3 InitialLinearVelocity { get; }
        public AuraVector3 InitialAngularVelocity { get; }
        public float LinearDamping { get; }
        public float AngularDamping { get; }
        public AuraBodyFreezeFlags Freeze { get; }
        public AuraVector3 CenterOfMass { get; }
        public float InertiaMultiplier { get; }
        public AuraBodyCollisionDetection CollisionDetection { get; }
        public bool AllowSleeping { get; }
        public float MaxLinearVelocity { get; }
        public float MaxAngularVelocity { get; }

        public static AuraPhysicsBodyDefinition CreateStatic(
            AuraPose pose,
            AuraPhysicsLayer layer,
            AuraPhysicsLayerMask collisionMask,
            params AuraPhysicsShapeDefinition[] shapes) =>
            new AuraPhysicsBodyDefinition(AuraBodyType.Static, pose, layer, collisionMask, shapes);

        public static AuraPhysicsBodyDefinition CreateDynamic(
            AuraPose pose,
            AuraPhysicsLayer layer,
            AuraPhysicsLayerMask collisionMask,
            params AuraPhysicsShapeDefinition[] shapes) =>
            new AuraPhysicsBodyDefinition(AuraBodyType.Dynamic, pose, layer, collisionMask, shapes);

        public static AuraPhysicsBodyDefinition CreateKinematic(
            AuraPose pose,
            AuraPhysicsLayer layer,
            AuraPhysicsLayerMask collisionMask,
            params AuraPhysicsShapeDefinition[] shapes) =>
            new AuraPhysicsBodyDefinition(AuraBodyType.Kinematic, pose, layer, collisionMask, shapes);

        public bool IsValid => Shapes != null;

        public AuraResult Validate()
        {
            if (Shapes == null || Shapes.Length == 0)
                return AuraResult.InvalidDefinition;

            if (Type == AuraBodyType.Dynamic && Mass <= 0f)
                return AuraResult.InvalidDefinition;

            if (Layer.Value < 0 || Layer.Value >= AuraPhysicsLayer.MaxLayers)
                return AuraResult.InvalidDefinition;

            if (InertiaMultiplier < 0f)
                return AuraResult.InvalidDefinition;

            for (var index = 0; index < Shapes.Length; index++)
            {
                var result = Shapes[index].Validate();
                if (result != AuraResult.Success)
                    return result;
            }

            return AuraResult.Success;
        }
    }
}
