using System;

namespace AuraEngine.Core
{
    public readonly struct AuraCharacterState : IEquatable<AuraCharacterState>
    {
        public AuraCharacterState(AuraCharacterId id, AuraVector3 position, AuraVector3 velocity, bool isGrounded)
        {
            Id = id;
            Position = position;
            Velocity = velocity;
            IsGrounded = isGrounded;
        }

        public AuraCharacterId Id { get; }
        public AuraVector3 Position { get; }
        public AuraVector3 Velocity { get; }
        public bool IsGrounded { get; }

        bool IEquatable<AuraCharacterState>.Equals(AuraCharacterState other) =>
            Id == other.Id && Position.Equals(other.Position) && Velocity.Equals(other.Velocity) && IsGrounded == other.IsGrounded;

        public override bool Equals(object obj) => obj is AuraCharacterState other && ((IEquatable<AuraCharacterState>)this).Equals(other);
        public override int GetHashCode() => HashCode.Combine(Id, Position, Velocity, IsGrounded);
        public override string ToString() => $"{Id} pos={Position} grounded={IsGrounded}";
    }
}
