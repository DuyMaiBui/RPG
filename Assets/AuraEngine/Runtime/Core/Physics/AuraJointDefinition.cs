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
            : this(type, bodyA, bodyB, anchorA, anchorB, distance, axisA, axisB, enableLimit, minLimit, maxLimit, normalAxisA, normalAxisB,
                swingLimit, motorEnabled, motorTargetVelocity, maxMotorForce, springFrequency, springDamping,
                default, 1f, 0f, 0f, AuraJointId.Invalid, AuraJointId.Invalid, default, false, default)
        {
        }

        /* Full constructor: the Jolt constraint appendix (second pulley point, ratio, plane swing, friction, referenced
           joints, SixDof axes, pyramid swing). `fixedPointA` is the pulley's first world rope anchor. */
        private AuraJointDefinition(
            AuraJointType type,
            PhysicsBodyId bodyA,
            PhysicsBodyId bodyB,
            AuraVector3 anchorA,
            AuraVector3 anchorB,
            float distance,
            AuraVector3 axisA,
            AuraVector3 axisB,
            bool enableLimit,
            float minLimit,
            float maxLimit,
            AuraVector3 normalAxisA,
            AuraVector3 normalAxisB,
            float swingLimit,
            bool motorEnabled,
            float motorTargetVelocity,
            float maxMotorForce,
            float springFrequency,
            float springDamping,
            AuraVector3 fixedPointA,
            float ratio,
            float planeSwingLimit,
            float maxFriction,
            AuraJointId jointA,
            AuraJointId jointB,
            AuraSixDofLimits sixDof,
            bool pyramidSwing,
            AuraVector3 fixedPointB)
        {
            FixedPointA = fixedPointA;
            FixedPointB = fixedPointB;
            Ratio = ratio;
            PlaneSwingLimit = planeSwingLimit;
            MaxFriction = maxFriction;
            JointA = jointA;
            JointB = jointB;
            SixDof = sixDof;
            PyramidSwing = pyramidSwing;
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

        /* Pulley rope anchors in world space (Full3D). */
        public AuraVector3 FixedPointA { get; }
        public AuraVector3 FixedPointB { get; }

        /* Pulley (segment B scale), Gear (A rotation = -Ratio * B rotation) or RackAndPinion (pinion radians per rack metre). */
        public float Ratio { get; }

        /* SwingTwist plane half cone angle; 0 reuses SwingLimit. */
        public float PlaneSwingLimit { get; }

        /* SwingTwist friction torque while no motor drives it. */
        public float MaxFriction { get; }

        /* Gear: the hinge joints of body A and body B. RackAndPinion: the pinion hinge (A) and the rack slider (B). */
        public AuraJointId JointA { get; }
        public AuraJointId JointB { get; }

        /* SixDof axis settings; ignored by other types. */
        public AuraSixDofLimits SixDof { get; }

        /* SwingTwist and SixDof: pyramid instead of cone swing limits. */
        public bool PyramidSwing { get; }

        /* The same joint between other bodies (the simulation resolves entities to bodies late). */
        public AuraJointDefinition WithBodies(PhysicsBodyId bodyA, PhysicsBodyId bodyB) =>
            new AuraJointDefinition(Type, bodyA, bodyB, AnchorA, AnchorB, Distance, AxisA, AxisB, EnableLimit, MinLimit, MaxLimit,
                NormalAxisA, NormalAxisB, SwingLimit, MotorEnabled, MotorTargetVelocity, MaxMotorForce, SpringFrequency, SpringDamping,
                FixedPointA, Ratio, PlaneSwingLimit, MaxFriction, JointA, JointB, SixDof, PyramidSwing, FixedPointB);

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

        public static AuraJointDefinition CreateCone(PhysicsBodyId a, PhysicsBodyId b, AuraVector3 anchorA, AuraVector3 anchorB, AuraVector3 axisA, AuraVector3 axisB, float halfConeAngle)
        {
            RequireAngle(halfConeAngle, nameof(halfConeAngle));
            return new AuraJointDefinition(AuraJointType.Cone, a, b, anchorA, anchorB, 0f, axisA, axisB, true, 0f, halfConeAngle);
        }

        /* Swing cone half angles (normal, plane; plane 0 reuses swingLimit), twist range min..max within -pi..pi,
           friction torque while undriven. Drive it with IPhysicsJointAxisControl (axis 0 twist, 1/2 swing). */
        public static AuraJointDefinition CreateSwingTwist(PhysicsBodyId a, PhysicsBodyId b, AuraVector3 anchorA, AuraVector3 anchorB, AuraVector3 axisA, AuraVector3 axisB, AuraVector3 normalA, AuraVector3 normalB, float swingLimit = 0f, float twistMin = 0f, float twistMax = 0f, float planeSwingLimit = 0f, float maxFriction = 0f, bool pyramidSwing = false)
        {
            RequireAngle(swingLimit, nameof(swingLimit));
            RequireAngle(planeSwingLimit, nameof(planeSwingLimit));
            if (!IsFinite(twistMin) || !IsFinite(twistMax) || twistMin > twistMax || twistMin < -Pi || twistMax > Pi)
                throw new ArgumentOutOfRangeException(nameof(twistMin), "Twist limits must satisfy -pi <= min <= max <= pi.");
            RequireNonNegative(maxFriction, nameof(maxFriction));
            return new AuraJointDefinition(AuraJointType.SwingTwist, a, b, anchorA, anchorB, 0f, axisA, axisB, true, twistMin, twistMax, normalA, normalB,
                swingLimit, false, 0f, 1f, 0f, 0f, default, 1f, planeSwingLimit, maxFriction, AuraJointId.Invalid, AuraJointId.Invalid, default, pyramidSwing, default);
        }

        /* Fixed total rope length (both fixed points equal, ratio 1). */
        public static AuraJointDefinition CreatePulley(PhysicsBodyId a, PhysicsBodyId b, AuraVector3 anchorA, AuraVector3 anchorB, AuraVector3 fixedPoint, float length) =>
            CreatePulley(a, b, anchorA, anchorB, fixedPoint, fixedPoint, 1f, length, length);

        /* Rope over two world points: |A - fixedA| + ratio * |B - fixedB| stays within minLength..maxLength.
           min == max is a rigid rope; min 0 is a slack-able rope. Limits can be changed with IPhysicsJointControl.SetLimits. */
        public static AuraJointDefinition CreatePulley(PhysicsBodyId a, PhysicsBodyId b, AuraVector3 anchorA, AuraVector3 anchorB, AuraVector3 fixedPointA, AuraVector3 fixedPointB, float ratio, float minLength, float maxLength)
        {
            if (!IsFinite(ratio) || ratio < 1.0e-4f || ratio > 1.0e4f)
                throw new ArgumentOutOfRangeException(nameof(ratio), "Pulley ratio must be positive and finite.");
            if (!IsFinite(minLength) || !IsFinite(maxLength) || minLength < 0f || maxLength < minLength || maxLength <= 0f)
                throw new ArgumentOutOfRangeException(nameof(maxLength), "Pulley lengths must satisfy 0 <= min <= max, max > 0.");
            return new AuraJointDefinition(AuraJointType.Pulley, a, b, anchorA, anchorB, 0f, default, default, true, minLength, maxLength, default, default,
                0f, false, 0f, 1f, 0f, 0f, fixedPointA, ratio, 0f, 0f, AuraJointId.Invalid, AuraJointId.Invalid, default, false, fixedPointB);
        }

        /* Axes are the joint frame X (axis) and Y (normal) on each body, in world space at creation. Every axis of a
           default AuraSixDofLimits is locked; free, limit or drive axes through the limits and IPhysicsJointAxisControl. */
        public static AuraJointDefinition CreateSixDof(PhysicsBodyId a, PhysicsBodyId b, AuraVector3 anchorA, AuraVector3 anchorB, in AuraSixDofLimits limits, AuraVector3 axisA = default, AuraVector3 normalA = default, AuraVector3 axisB = default, AuraVector3 normalB = default, bool pyramidSwing = false)
        {
            if (!limits.IsValid(pyramidSwing))
                throw new ArgumentException("A SixDof axis limit is out of range (rotation X -pi..pi, swing Y/Z half angle 0..pi, min <= max, friction >= 0).", nameof(limits));
            var ax = axisA == default ? AuraVector3.UnitX : axisA;
            var bx = axisB == default ? AuraVector3.UnitX : axisB;
            var ay = normalA == default ? AuraVector3.UnitY : normalA;
            var by = normalB == default ? AuraVector3.UnitY : normalB;
            return new AuraJointDefinition(AuraJointType.SixDof, a, b, anchorA, anchorB, 0f, ax, bx, false, 0f, 0f, ay, by,
                0f, false, 0f, 1f, 0f, 0f, default, 1f, 0f, 0f, AuraJointId.Invalid, AuraJointId.Invalid, limits, pyramidSwing, default);
        }

        /* Couples the rotation of body A to body B: angular position A = -ratio * angular position B about the given
           world-space hinge axes. hingeA / hingeB are the live hinge joints of A and B (typically to a static body) and
           must exist first. Destroying or breaking either hinge removes the gear (it then reports broken). */
        public static AuraJointDefinition CreateGear(PhysicsBodyId a, PhysicsBodyId b, AuraVector3 axisA, AuraVector3 axisB, float ratio, AuraJointId hingeA, AuraJointId hingeB)
        {
            RequireCoupling(axisA, axisB, ratio, hingeA, hingeB);
            return new AuraJointDefinition(AuraJointType.Gear, a, b, default, default, 0f, axisA, axisB, false, 0f, 0f, default, default,
                0f, false, 0f, 1f, 0f, 0f, default, ratio, 0f, 0f, hingeA, hingeB, default, false, default);
        }

        /* Couples a pinion (body A, hinge joint pinionHinge about hingeAxis) to a rack (body B, slider joint rackSlider
           along sliderAxis, both world space): rack translation = pinion rotation / ratio. ratio is radians per metre. */
        public static AuraJointDefinition CreateRackAndPinion(PhysicsBodyId pinion, PhysicsBodyId rack, AuraVector3 hingeAxis, AuraVector3 sliderAxis, float ratio, AuraJointId pinionHinge, AuraJointId rackSlider)
        {
            RequireCoupling(hingeAxis, sliderAxis, ratio, pinionHinge, rackSlider);
            return new AuraJointDefinition(AuraJointType.RackAndPinion, pinion, rack, default, default, 0f, hingeAxis, sliderAxis, false, 0f, 0f, default, default,
                0f, false, 0f, 1f, 0f, 0f, default, ratio, 0f, 0f, pinionHinge, rackSlider, default, false, default);
        }

        /* Plane2D only. A is the chassis, B the wheel; axis is the suspension direction in world space. A spring
           frequency of 0 means a rigid axis. Travel limits are relative to the creation pose (min <= 0 <= max);
           the motor spins the wheel in rad/s limited by maxMotorTorque. */
        public static AuraJointDefinition CreateWheel(PhysicsBodyId chassis, PhysicsBodyId wheel, AuraVector3 anchorA, AuraVector3 anchorB, AuraVector3 axis, float springFrequency = 0f, float springDamping = 0f, bool enableLimit = false, float minLimit = 0f, float maxLimit = 0f, bool motorEnabled = false, float motorSpeed = 0f, float maxMotorTorque = 1f) =>
            new AuraJointDefinition(AuraJointType.Wheel, chassis, wheel, anchorA, anchorB, 0f, axis, axis, enableLimit, minLimit, maxLimit, default, default, 0f, motorEnabled, motorSpeed, maxMotorTorque, springFrequency, springDamping);

        /* Plane2D only. Drags body toward target (world space) with a soft spring; move the target at runtime with
           IPhysicsJointTarget. anchor is the grabbed point on body (world space at creation); fixedBody may be
           PhysicsBodyId.Invalid, in which case the backend uses an internal static anchor. */
        public static AuraJointDefinition CreateMouse(PhysicsBodyId fixedBody, PhysicsBodyId body, AuraVector3 target, float frequency, float damping, float maxForce) =>
            new AuraJointDefinition(AuraJointType.Mouse, fixedBody, body, target, target, 0f, default, default, false, 0f, 0f, default, default, 0f, false, 0f, maxForce, frequency, damping);

        /* Plane2D only. Like a distance joint that never pushes: free below maxLength. */
        public static AuraJointDefinition CreateRope(PhysicsBodyId a, PhysicsBodyId b, AuraVector3 anchorA, AuraVector3 anchorB, float maxLength) =>
            new AuraJointDefinition(AuraJointType.Rope, a, b, anchorA, anchorB, maxLength);

        public static AuraJointDefinition CreateSpring(PhysicsBodyId a, PhysicsBodyId b, AuraVector3 anchorA, AuraVector3 anchorB, float distance, float frequency, float damping) =>
            new AuraJointDefinition(AuraJointType.Spring, a, b, anchorA, anchorB, distance, default, default, false, 0f, 0f, default, default, 0f, false, 0f, 0f, frequency, damping);

        private const float Pi = (float)Math.PI;

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static void RequireAngle(float value, string name)
        {
            if (!IsFinite(value) || value < 0f || value > Pi)
                throw new ArgumentOutOfRangeException(name, "Angle must be within 0..pi.");
        }

        private static void RequireNonNegative(float value, string name)
        {
            if (!IsFinite(value) || value < 0f)
                throw new ArgumentOutOfRangeException(name, "Value must be finite and non-negative.");
        }

        private static void RequireCoupling(AuraVector3 axisA, AuraVector3 axisB, float ratio, AuraJointId first, AuraJointId second)
        {
            if (!first.IsValid || !second.IsValid || first == second)
                throw new ArgumentException("A coupling joint needs two distinct, valid referenced joints.");
            if (!IsFinite(ratio) || ratio < 1.0e-4f || ratio > 1.0e4f)
                throw new ArgumentOutOfRangeException(nameof(ratio), "Ratio must be positive and finite.");
            if (!IsFinite(axisA.LengthSquared) || !IsFinite(axisB.LengthSquared) || axisA.LengthSquared < 1.0e-8f || axisB.LengthSquared < 1.0e-8f)
                throw new ArgumentException("Coupling axes must be finite and non-zero.");
        }
    }
}
