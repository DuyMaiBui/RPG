using System;
using AuraEngine.Physics.Native;

namespace AuraEngine.KernelTests
{
    /* Random argument generators. Floats occasionally come out non-finite or enormous (SoakSettings.EvilRate):
       the kernel is expected to reject those with an error code instead of corrupting state. */
    internal static class SoakValues
    {
        private static readonly float[] EvilFloats =
        {
            float.NaN, float.PositiveInfinity, float.NegativeInfinity, 1e30f, -1e30f
        };

        public static float Float(SoakRng rng, float lo, float hi)
        {
            if (SoakSettings.EvilRate > 0f && rng.Chance(SoakSettings.EvilRate))
                return rng.Pick(EvilFloats);
            return rng.Range(lo, hi);
        }

        public static NativeVector3 Vec(SoakRng rng, float range) =>
            new NativeVector3 { X = Float(rng, -range, range), Y = Float(rng, -range, range), Z = Float(rng, -range, range) };

        public static NativeVector3 PlainVec(SoakRng rng, float range) =>
            new NativeVector3 { X = rng.Range(-range, range), Y = rng.Range(-range, range), Z = rng.Range(-range, range) };

        public static NativeVector3 PositiveVec(SoakRng rng, float lo, float hi) =>
            new NativeVector3 { X = Float(rng, lo, hi), Y = Float(rng, lo, hi), Z = Float(rng, lo, hi) };

        public static NativeVector3 UnitVec(SoakRng rng)
        {
            if (SoakSettings.Bad(rng, 0.02f))
                return default;
            var v = PlainVec(rng, 1f);
            var length = (float)Math.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);
            if (length < 1e-3f)
                return new NativeVector3 { X = 0f, Y = 1f, Z = 0f };
            return new NativeVector3 { X = v.X / length, Y = v.Y / length, Z = v.Z / length };
        }

        public static NativeQuaternion Quat(SoakRng rng)
        {
            if (SoakSettings.Bad(rng, 0.03f))
                return rng.Chance(0.5f) ? default : new NativeQuaternion { X = rng.Range(-2f, 2f), Y = rng.Range(-2f, 2f), Z = rng.Range(-2f, 2f), W = rng.Range(-2f, 2f) };
            var v = UnitVec(rng);
            var half = rng.Range(0f, 3.1f);
            var s = (float)Math.Sin(half);
            return new NativeQuaternion { X = v.X * s, Y = v.Y * s, Z = v.Z * s, W = (float)Math.Cos(half) };
        }

        /* Rotation about z only, valid for Plane2D worlds. */
        public static NativeQuaternion QuatZ(SoakRng rng)
        {
            var half = rng.Range(-3f, 3f) * 0.5f;
            return new NativeQuaternion { X = 0f, Y = 0f, Z = (float)Math.Sin(half), W = (float)Math.Cos(half) };
        }

        public static NativePose Pose(SoakRng rng, float range, bool is2D)
        {
            var position = Vec(rng, range);
            if (is2D)
                position.Z = 0f;
            return new NativePose { Position = position, Rotation = is2D && !rng.Chance(0.1f) ? QuatZ(rng) : Quat(rng) };
        }

        public static NativeQueryFilter Filter(SoakRng rng, SoakWorld world)
        {
            return new NativeQueryFilter
            {
                LayerMask = rng.Chance(0.6f) ? ulong.MaxValue : rng.NextU64(),
                TriggerInteraction = rng.Chance(0.9f) ? rng.Range(0, 2) : rng.Range(-1, 5),
                Flags = rng.Chance(0.8f) ? 0 : rng.Range(0, 15),
                IgnoredBody = rng.Chance(0.2f) && world.Bodies.Count > 0
                    ? world.Bodies[rng.Next(world.Bodies.Count)].Handle
                    : new NativeBodyHandle { Index = uint.MaxValue, Generation = uint.MaxValue },
                ShapeFilterGroup = 0,
                ShapeFilterMask = uint.MaxValue,
                ActiveEdgeMode = rng.Range(0, 1),
                ActiveEdgeMovementDirection = PlainVec(rng, 1f),
            };
        }
    }
}
