using System;
using AuraEngine.Physics.Native;

namespace AuraEngine.KernelTests
{
    /* Invariants the soak enforces. Error codes are never failures by themselves. */
    internal static class SoakChecks
    {
        public static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        public static void Finite(SoakEpisode e, SoakWorld world, string what, NativeVector3 v)
        {
            if (world.Tainted)
                return;
            if (!IsFinite(v.X) || !IsFinite(v.Y) || !IsFinite(v.Z))
                e.Fail("non-finite " + what + " (" + v.X + ", " + v.Y + ", " + v.Z + ") in " + world);
        }

        public static void Finite(SoakEpisode e, SoakWorld world, string what, float value)
        {
            if (!world.Tainted && !IsFinite(value))
                e.Fail("non-finite " + what + " (" + value + ") in " + world);
        }

        public static void Finite(SoakEpisode e, SoakWorld world, string what, NativePose pose)
        {
            Finite(e, world, what + ".position", pose.Position);
            Finite(e, world, what + ".rotation.x", pose.Rotation.X);
            Finite(e, world, what + ".rotation.y", pose.Rotation.Y);
            Finite(e, world, what + ".rotation.z", pose.Rotation.Z);
            Finite(e, world, what + ".rotation.w", pose.Rotation.W);
        }

        /* A live world must never be rejected as invalid. */
        public static int World(SoakEpisode e, SoakWorld world, string op, int code)
        {
            if (code == SoakCodes.InvalidWorld)
                e.Fail(op + " returned INVALID_WORLD for live " + world);
            return code;
        }

        /* Handle confusion: a stale or never issued handle must not succeed, a live handle must not be rejected. */
        public static int Handle(SoakEpisode e, SoakWorld world, SoakHandleState state, string op, int code)
        {
            World(e, world, op, code);
            if (state == SoakHandleState.Dead && code == SoakCodes.Success)
                e.Fail("HANDLE CONFUSION: " + op + " succeeded on a stale or never issued handle in " + world);
            if (state == SoakHandleState.Live && code == SoakCodes.InvalidHandle)
                e.Fail("HANDLE LOST: " + op + " rejected a live handle with INVALID_HANDLE in " + world);
            return code;
        }
    }
}
