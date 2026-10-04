using System;

namespace AuraEngine.Core
{
    public readonly struct AuraJointDefinition
    {
        public AuraJointDefinition(
            AuraJointType type,
            PhysicsBodyId bodyA,
            PhysicsBodyId bodyB,
            AuraVector3 anchorA,
            AuraVector3 anchorB,
            float distance = 0f,
            AuraVector3 axisA = default,
            AuraVector3 axisB = default,
            bool enableLimit = false,
            float minLimit = 0f,
            float maxLimit = 0f,
            AuraVector3 normalAxisA = default,
            AuraVector3 normalAxisB = default,
            float swingLimit = 0f,
            bool motorEnabled = false,
            float motorTargetVelocity = 0f,
            float maxMotorForce = 1f,
            float springFrequency = 0f,
            float springDamping = 0f)
        {
            Type = type;
            BodyA = bodyA;
            BodyB = bodyB;
            AnchorA = anchorA;
            AnchorB = anchorB;
            Distance = distance;
            AxisA = axisA == default ? AuraVector3.UnitY : axisA;
            AxisB = axisB == default ? AuraVector3.UnitY : axisB;
            EnableLimit = enableLimit;
            MinLimit = minLimit;
            MaxLimit = maxLimit;
            NormalAxisA = normalAxisA == default ? AuraVector3.UnitZ : normalAxisA;
            NormalAxisB = normalAxisB == default ? AuraVector3.UnitZ : normalAxisB;
            SwingLimit = swingLimit;
            MotorEnabled = motorEnabled;
            MotorTargetVelocity = motorTargetVelocity;
            MaxMotorForce = maxMotorForce;
            SpringFrequency = springFrequency;
            SpringDamping = springDamping;
        }

        public AuraJointType Type { get; }
        public PhysicsBodyId BodyA { get; }
        public PhysicsBodyId BodyB { get; }
        public AuraVector3 AnchorA { get; }
        public AuraVector3 AnchorB { get; }
        public float Distance { get; }
        public AuraVector3 AxisA { get; }
        public AuraVector3 AxisB { get; }
        public bool EnableLimit { get; }
        public float MinLimit { get; }
        public float MaxLimit { get; }
        public AuraVector3 NormalAxisA { get; }
        public AuraVector3 NormalAxisB { get; }
        public float SwingLimit { get; }
        public bool MotorEnabled { get; }
        public float MotorTargetVelocity { get; }
        public float MaxMotorForce { get; }
        public float SpringFrequency { get; }
        public float SpringDamping { get; }

        public static AuraJointDefinition CreateDistance(PhysicsBodyId a, PhysicsBodyId b, AuraVector3 anchorA, AuraVector3 anchorB, float distance) =>
            new AuraJointDefinition(AuraJointType.Distance, a, b, anchorA, anchorB, distance);

        public static AuraJointDefinition CreateFixed(PhysicsBodyId a, PhysicsBodyId b, AuraVector3 anchorA, AuraVector3 anchorB) =>
            new AuraJointDefinition(AuraJointType.Fixed, a, b, anchorA, anchorB);

        public static AuraJointDefinition CreatePoint(PhysicsBodyId a, PhysicsBodyId b, AuraVector3 anchorA, AuraVector3 anchorB) =>
            new AuraJointDefinition(AuraJointType.Point, a, b, anchorA, anchorB);

        public static AuraJointDefinition CreateHinge(PhysicsBodyId a, PhysicsBodyId b, AuraVector3 anchorA, AuraVector3 anchorB, AuraVector3 axisA, AuraVector3 axisB) =>
            new AuraJointDefinition(AuraJointType.Hinge, a, b, anchorA, anchorB, 0f, axisA, axisB);

        public static AuraJointDefinition CreateSlider(PhysicsBodyId a, PhysicsBodyId b, AuraVector3 anchorA, AuraVector3 anchorB, AuraVector3 axisA, AuraVector3 axisB, bool enableLimit = false, float minLimit = 0f, float maxLimit = 0f) =>
            new AuraJointDefinition(AuraJointType.Slider, a, b, anchorA, anchorB, 0f, axisA, axisB, enableLimit, minLimit, maxLimit);

        public static AuraJointDefinition CreateCone(PhysicsBodyId a, PhysicsBodyId b, AuraVector3 anchorA, AuraVector3 anchorB, AuraVector3 axisA, AuraVector3 axisB, float halfConeAngle) =>
            new AuraJointDefinition(AuraJointType.Cone, a, b, anchorA, anchorB, 0f, axisA, axisB, true, 0f, halfConeAngle);

        public static AuraJointDefinition CreateSwingTwist(PhysicsBodyId a, PhysicsBodyId b, AuraVector3 anchorA, AuraVector3 anchorB, AuraVector3 axisA, AuraVector3 axisB, AuraVector3 normalA, AuraVector3 normalB, float swingLimit = 0f, float twistMin = 0f, float twistMax = 0f) =>
            new AuraJointDefinition(AuraJointType.SwingTwist, a, b, anchorA, anchorB, 0f, axisA, axisB, true, twistMin, twistMax, normalA, normalB, swingLimit);

        public static AuraJointDefinition CreatePulley(PhysicsBodyId a, PhysicsBodyId b, AuraVector3 anchorA, AuraVector3 anchorB, AuraVector3 fixedPoint, float length) =>
            new AuraJointDefinition(AuraJointType.Pulley, a, b, anchorA, anchorB, length, fixedPoint, fixedPoint);

        public static AuraJointDefinition CreateSpring(PhysicsBodyId a, PhysicsBodyId b, AuraVector3 anchorA, AuraVector3 anchorB, float distance, float frequency, float damping) =>
            new AuraJointDefinition(AuraJointType.Spring, a, b, anchorA, anchorB, distance, default, default, false, 0f, 0f, default, default, 0f, false, 0f, 0f, frequency, damping);
    }
}
