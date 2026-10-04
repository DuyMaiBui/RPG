namespace AuraEngine.Core
{
    public readonly struct AuraJointFeedback
    {
        public AuraJointFeedback(float force, float torque, float motorLoad, float position, AuraJointMotorMode motorMode, bool isBroken)
        {
            Force = force;
            Torque = torque;
            MotorLoad = motorLoad;
            Position = position;
            MotorMode = motorMode;
            IsBroken = isBroken;
        }

        /* Reaction force magnitude (N) of the last step, excluding motor drive. */
        public float Force { get; }

        /* Reaction torque magnitude (N*m) outside the free axis, including limit torque, excluding motor drive. */
        public float Torque { get; }

        /* Force (slider) or torque (hinge) currently spent by the motor. */
        public float MotorLoad { get; }

        /* Hinge angle (rad) or slider translation (m) relative to the creation pose. */
        public float Position { get; }

        public AuraJointMotorMode MotorMode { get; }

        /* True once a break threshold removed the joint; Force/Torque then hold the loads that broke it. */
        public bool IsBroken { get; }
    }
}
