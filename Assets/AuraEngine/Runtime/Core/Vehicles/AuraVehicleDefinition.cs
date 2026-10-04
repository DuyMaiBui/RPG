using AuraEngine.Core;

namespace AuraEngine.Core
{
    public readonly struct AuraVehicleDefinition
    {
        public AuraVehicleDefinition(PhysicsBodyId chassis, AuraVector3 up, AuraVector3 forward, AuraVector3[] wheelPositions,
            float wheelRadius, float wheelWidth, float suspensionMinLength, float suspensionMaxLength,
            float suspensionFrequency, float suspensionDamping, float maxSteerAngle, float maxPitchRollAngle,
            float maxEngineTorque, AuraPhysicsLayer wheelObjectLayer = default)
        {
            Chassis = chassis; Up = up; Forward = forward; WheelPositions = wheelPositions;
            WheelRadius = wheelRadius; WheelWidth = wheelWidth; SuspensionMinLength = suspensionMinLength;
            SuspensionMaxLength = suspensionMaxLength; SuspensionFrequency = suspensionFrequency;
            SuspensionDamping = suspensionDamping; MaxSteerAngle = maxSteerAngle;
            MaxPitchRollAngle = maxPitchRollAngle; MaxEngineTorque = maxEngineTorque; WheelObjectLayer = wheelObjectLayer;
        }

        public PhysicsBodyId Chassis { get; }
        public AuraVector3 Up { get; }
        public AuraVector3 Forward { get; }
        public AuraVector3[] WheelPositions { get; }
        public float WheelRadius { get; }
        public float WheelWidth { get; }
        public float SuspensionMinLength { get; }
        public float SuspensionMaxLength { get; }
        public float SuspensionFrequency { get; }
        public float SuspensionDamping { get; }
        public float MaxSteerAngle { get; }
        public float MaxPitchRollAngle { get; }
        public float MaxEngineTorque { get; }
        public AuraPhysicsLayer WheelObjectLayer { get; }
    }
}
