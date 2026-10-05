using System;
using AuraEngine.Physics.Native;

namespace AuraEngine.KernelTests
{
    internal static class SoakSnapshotOps
    {
        public static void Run(SoakEpisode e, SoakWorld world)
        {
            var rng = e.Rng;
            var op = rng.Next(10);
            if (op < 4 || world.Snapshot == null)
            {
                Save(e, world, false);
                return;
            }

            if (op < 6)
            {
                RoundTrip(e, world);
                return;
            }

            var bytes = (byte[])world.Snapshot.Clone();
            var taint = false;
            var label = "plain";
            if (op == 6 && bytes.Length > 16)
            {
                label = "truncated";
                Array.Resize(ref bytes, rng.Range(0, bytes.Length - 1));
            }
            else if (op == 7)
            {
                label = "header";
                if (bytes.Length >= 16)
                    bytes[rng.Next(16)] ^= (byte)rng.Range(1, 255);
            }
            else if (op == 8 && bytes.Length > 16 && SoakSettings.Bad(rng, 1f))
            {
                label = "payload-flip";
                taint = true;
                for (var i = 0; i < 4; i++)
                    bytes[rng.Range(16, bytes.Length - 1)] ^= (byte)rng.Range(1, 255);
            }
            else if (op == 9 && bytes.Length >= 16)
            {
                label = "count";
                bytes[8] = (byte)rng.Range(0, 255);
                bytes[9] = (byte)rng.Range(0, 255);
            }

            Restore(e, world, bytes, label, taint);
        }

        private static void Save(SoakEpisode e, SoakWorld world, bool exact)
        {
            e.Begin("SerializeState probe " + world);
            var probe = e.End(NativeMethods.Aura_SerializeState(world.Handle, IntPtr.Zero, 0, out var size));
            SoakChecks.World(e, world, "SerializeState", probe);
            if (probe != SoakCodes.CapacityExceeded && !(probe == SoakCodes.Success && size == 0))
                e.Fail("SerializeState with a null buffer returned " + probe + " instead of CAPACITY_EXCEEDED");
            var capacity = (int)size - (!exact && e.Rng.Chance(0.2f) && size > 0 ? e.Rng.Range(1, (int)Math.Min(size, 40u)) : 0);
            using (var buffer = new SoakGuardBuffer<byte>(Math.Max(capacity, 0)))
            {
                e.Begin("SerializeState " + world + " size=" + size + " cap=" + capacity);
                var code = e.End(NativeMethods.Aura_SerializeState(world.Handle, buffer.Pointer, (uint)capacity, out var written));
                SoakChecks.World(e, world, "SerializeState", code);
                buffer.Verify("SerializeState");
                if (code == SoakCodes.Success)
                {
                    if (written != size && capacity == (int)size)
                        e.Fail("SerializeState size changed between the probe and the write (" + size + " vs " + written + ")");
                    var copy = new byte[written];
                    for (var i = 0; i < copy.Length; i++)
                        copy[i] = buffer[i];
                    world.Snapshot = copy;
                    e.Mix(written);
                }
                else if (capacity < (int)size && code != SoakCodes.CapacityExceeded)
                    e.Fail("SerializeState into a too small buffer returned " + code + " instead of CAPACITY_EXCEEDED");
            }
        }

        /* Saving is read-only and restoring your own just-saved state must not change the state hash. */
        private static void RoundTrip(SoakEpisode e, SoakWorld world)
        {
            e.Begin("SnapshotRoundTrip " + world);
            NativeMethods.Aura_ComputeStateHash(world.Handle, out var before);
            Save(e, world, true);
            NativeMethods.Aura_ComputeStateHash(world.Handle, out var afterSave);
            if (before != afterSave)
                e.Fail("SerializeState changed the state hash of " + world + " (" + before + " -> " + afterSave + ")");
            Restore(e, world, world.Snapshot, "own", false);
            NativeMethods.Aura_ComputeStateHash(world.Handle, out var afterRestore);
            if (afterRestore != before)
                e.Fail("restoring the just saved snapshot changed the state hash of " + world + " (" + before + " -> " + afterRestore + ")");
        }

        /* A restore applies the snapshot's disabled flags, so the enabled model is re-read from the kernel. */
        private static void RefreshEnabled(SoakWorld world)
        {
            foreach (var body in world.Bodies)
            {
                if (body.State == SoakHandleState.Live
                    && NativeMethods.Aura_IsBodyEnabled(world.Handle, body.Handle, out var enabled) == SoakCodes.Success)
                    body.Disabled = enabled == 0;
            }
        }

        private static void Restore(SoakEpisode e, SoakWorld world, byte[] bytes, string label, bool taint)
        {
            using (var scope = new SoakNativeScope())
            {
                var block = scope.Copy(bytes);
                e.Begin("DeserializeState " + world + " " + label + " size=" + bytes.Length);
                var code = e.End(NativeMethods.Aura_DeserializeState(world.Handle, block, (uint)bytes.Length));
                SoakChecks.World(e, world, "DeserializeState", code);
                if (taint)
                    world.Tainted = true;
                RefreshEnabled(world);
            }
        }
    }
}
