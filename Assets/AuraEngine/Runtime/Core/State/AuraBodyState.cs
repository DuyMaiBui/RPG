using System;

namespace AuraEngine.Core
{
    public readonly struct AuraBodyState : IEquatable<AuraBodyState>
    {
        public AuraBodyState(
            PhysicsBodyId body,
            SimulationEntityId entity,
            AuraPose pose,
            AuraVector3 linearVelocity,
            AuraVector3 angularVelocity,
            bool isAwake,
            uint flags)
        {
            Body = body;
            Entity = entity;
            Pose = pose;
            LinearVelocity = linearVelocity;
            AngularVelocity = angularVelocity;
            IsAwake = isAwake;
            Flags = flags;
        }

        public PhysicsBodyId Body { get; }
        public SimulationEntityId Entity { get; }
        public AuraPose Pose { get; }
        public AuraVector3 LinearVelocity { get; }
        public AuraVector3 AngularVelocity { get; }
        public bool IsAwake { get; }
        public uint Flags { get; }

        bool IEquatable<AuraBodyState>.Equals(AuraBodyState other) =>
            Body == other.Body &&
            Entity == other.Entity &&
            Pose.Equals(other.Pose) &&
            LinearVelocity.Equals(other.LinearVelocity) &&
            AngularVelocity.Equals(other.AngularVelocity) &&
            IsAwake == other.IsAwake &&
            Flags == other.Flags;

        public override bool Equals(object obj) => obj is AuraBodyState other && ((IEquatable<AuraBodyState>)this).Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(Body, Entity, Pose, LinearVelocity, AngularVelocity, IsAwake, Flags);

        public override string ToString() => $"{Body} {Entity} pose={Pose}";
    }
}
