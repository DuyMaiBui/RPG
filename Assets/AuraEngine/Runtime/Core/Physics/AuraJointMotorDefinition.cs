namespace AuraEngine.Core
{
    /* Runtime motor command for hinge (rad, rad/s, N*m) and slider (m, m/s, N) joints.
       Position mode is only available on backends that report it (Jolt 3D). */
    public readonly struct AuraJointMotorDefinition
    {
        public AuraJointMotorDefinition(AuraJointMotorMode mode, float target, float maxForce, float springFrequency = 0f, float springDamping = 0f)
        {
            Mode = mode;
            Target = target;
            MaxForce = maxForce;
            SpringFrequency = springFrequency;
            SpringDamping = springDamping;
        }

        public AuraJointMotorMode Mode { get; }

        /* Target velocity (Velocity mode) or target angle/translation relative to the creation pose (Position mode). */
        public float Target { get; }

        /* Maximum motor force (slider) or torque (hinge). Must be positive unless the mode is Off. */
        public float MaxForce { get; }

        /* Position mode spring tuning; 0 keeps the backend default. */
        public float SpringFrequency { get; }
        public float SpringDamping { get; }

        public static AuraJointMotorDefinition Off => new AuraJointMotorDefinition(AuraJointMotorMode.Off, 0f, 0f);

        public static AuraJointMotorDefinition Velocity(float targetVelocity, float maxForce) =>
            new AuraJointMotorDefinition(AuraJointMotorMode.Velocity, targetVelocity, maxForce);

        public static AuraJointMotorDefinition Position(float targetPosition, float maxForce, float springFrequency = 0f, float springDamping = 0f) =>
            new AuraJointMotorDefinition(AuraJointMotorMode.Position, targetPosition, maxForce, springFrequency, springDamping);
    }
}
