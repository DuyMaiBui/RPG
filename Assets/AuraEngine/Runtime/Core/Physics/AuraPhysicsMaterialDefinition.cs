using System;

namespace AuraEngine.Core
{
    public readonly struct AuraPhysicsMaterialDefinition : IEquatable<AuraPhysicsMaterialDefinition>
    {
        public AuraPhysicsMaterialDefinition(
            float friction,
            float restitution,
            float density,
            AuraFrictionCombine frictionCombine = AuraFrictionCombine.Average,
            AuraRestitutionCombine restitutionCombine = AuraRestitutionCombine.Average)
        {
            Friction = friction;
            Restitution = restitution;
            Density = density;
            FrictionCombine = frictionCombine;
            RestitutionCombine = restitutionCombine;
        }

        public static AuraPhysicsMaterialDefinition Default => new AuraPhysicsMaterialDefinition(0.5f, 0f, 1000f);

        public float Friction { get; }
        public float Restitution { get; }
        public float Density { get; }
        public AuraFrictionCombine FrictionCombine { get; }
        public AuraRestitutionCombine RestitutionCombine { get; }

        public AuraResult Validate()
        {
            if (Friction < 0f)
                return AuraResult.InvalidDefinition;
            if (Restitution < 0f || Restitution > 1f)
                return AuraResult.InvalidDefinition;
            if (Density <= 0f)
                return AuraResult.InvalidDefinition;

            return AuraResult.Success;
        }

        public static float CombineFriction(float a, float b, AuraFrictionCombine mode)
        {
            switch (mode)
            {
                case AuraFrictionCombine.Minimum: return MathF.Min(a, b);
                case AuraFrictionCombine.Maximum: return MathF.Max(a, b);
                case AuraFrictionCombine.Multiply: return a * b;
                default: return (a + b) * 0.5f;
            }
        }

        public static float CombineRestitution(float a, float b, AuraRestitutionCombine mode)
        {
            switch (mode)
            {
                case AuraRestitutionCombine.Minimum: return MathF.Min(a, b);
                case AuraRestitutionCombine.Maximum: return MathF.Max(a, b);
                case AuraRestitutionCombine.Multiply: return a * b;
                default: return (a + b) * 0.5f;
            }
        }

        bool IEquatable<AuraPhysicsMaterialDefinition>.Equals(AuraPhysicsMaterialDefinition other) =>
            Friction.Equals(other.Friction) &&
            Restitution.Equals(other.Restitution) &&
            Density.Equals(other.Density) &&
            FrictionCombine == other.FrictionCombine &&
            RestitutionCombine == other.RestitutionCombine;

        public override bool Equals(object obj) => obj is AuraPhysicsMaterialDefinition other && ((IEquatable<AuraPhysicsMaterialDefinition>)this).Equals(other);

        public override int GetHashCode() => HashCode.Combine(Friction, Restitution, Density, FrictionCombine, RestitutionCombine);
        public override string ToString() => $"friction={Friction} restitution={Restitution} density={Density}";

        public static bool operator ==(AuraPhysicsMaterialDefinition left, AuraPhysicsMaterialDefinition right) =>
            ((IEquatable<AuraPhysicsMaterialDefinition>)left).Equals(right);

        public static bool operator !=(AuraPhysicsMaterialDefinition left, AuraPhysicsMaterialDefinition right) =>
            !((IEquatable<AuraPhysicsMaterialDefinition>)left).Equals(right);
    }
}
