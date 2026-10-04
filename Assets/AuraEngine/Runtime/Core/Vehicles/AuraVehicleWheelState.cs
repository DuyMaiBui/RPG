namespace AuraEngine.Core
{
    public readonly struct AuraVehicleWheelState
    {
        public AuraVehicleWheelState(bool hasContact, float suspensionLength, float steerAngle, float angularVelocity, AuraVector3 contactNormal)
        { HasContact = hasContact; SuspensionLength = suspensionLength; SteerAngle = steerAngle; AngularVelocity = angularVelocity; ContactNormal = contactNormal; }
        public bool HasContact { get; }
        public float SuspensionLength { get; }
        public float SteerAngle { get; }
        public float AngularVelocity { get; }
        public AuraVector3 ContactNormal { get; }
    }
}
