using System;

namespace AuraEngine.Core
{
    public static class AuraStateHasher
    {
        private const ulong Offset = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;

        public static ulong Begin() => Offset;

        public static ulong Combine(ulong hash, ulong value)
        {
            unchecked
            {
                hash ^= value;
                hash *= Prime;
                return hash;
            }
        }

        public static ulong Hash(long value) => Combine(Offset, unchecked((ulong)value));

        public static ulong CombineInt(ulong hash, int value) => Combine(hash, unchecked((uint)value));

        public static ulong CombineFloat(ulong hash, float value) => Combine(hash, unchecked((ulong)BitConverter.SingleToInt32Bits(value)));

        public static ulong CombineBool(ulong hash, bool value) => Combine(hash, value ? 1UL : 0UL);

        public static ulong CombineVector3(ulong hash, in AuraVector3 value)
        {
            hash = CombineFloat(hash, value.X);
            hash = CombineFloat(hash, value.Y);
            hash = CombineFloat(hash, value.Z);
            return hash;
        }

        public static ulong CombineQuaternion(ulong hash, in AuraQuaternion value)
        {
            hash = CombineFloat(hash, value.X);
            hash = CombineFloat(hash, value.Y);
            hash = CombineFloat(hash, value.Z);
            hash = CombineFloat(hash, value.W);
            return hash;
        }

        public static ulong CombinePose(ulong hash, in AuraPose value)
        {
            hash = CombineVector3(hash, value.Position);
            hash = CombineQuaternion(hash, value.Rotation);
            return hash;
        }

        public static ulong HashBodyState(ulong hash, in AuraBodyState state)
        {
            hash = CombineInt(hash, state.Entity.Index);
            hash = CombineInt(hash, state.Entity.Generation);
            hash = CombinePose(hash, state.Pose);
            hash = CombineVector3(hash, state.LinearVelocity);
            hash = CombineVector3(hash, state.AngularVelocity);
            hash = CombineBool(hash, state.IsAwake);
            hash = Combine(hash, state.Flags);
            return hash;
        }
    }
}
