using AuraEngine.Physics.Native;

namespace AuraEngine.KernelTests
{
    internal static class SoakFieldOps
    {
        private static NativeForceFieldDesc Desc(SoakEpisode e, SoakWorld world)
        {
            var rng = e.Rng;
            var bad = SoakSettings.Bad(rng, 0.04f);
            return new NativeForceFieldDesc
            {
                Shape = bad ? rng.Range(-1, 4) : rng.Range(0, 1),
                Kind = bad ? rng.Range(-1, 5) : rng.Range(0, 2),
                Mode = bad ? rng.Range(-1, 4) : rng.Range(0, 1),
                Falloff = bad ? rng.Range(-1, 5) : rng.Range(0, 2),
                Pose = SoakValues.Pose(rng, 12f, world.Is2D),
                HalfExtents = SoakValues.PositiveVec(rng, 0.5f, 8f),
                Radius = SoakValues.Float(rng, 0.5f, 10f),
                Vector = SoakValues.Vec(rng, 15f),
                Strength = SoakValues.Float(rng, -20f, 20f),
                MinRadius = SoakValues.Float(rng, 0f, 1f),
                MaxRadius = rng.Chance(0.5f) ? 0f : SoakValues.Float(rng, 0f, 12f),
                LayerMask = rng.Chance(0.7f) ? ulong.MaxValue : rng.NextU64(),
                Enabled = (byte)(rng.Chance(0.9f) ? 1 : 0),
            };
        }

        public static void Run(SoakEpisode e, SoakWorld world)
        {
            var rng = e.Rng;
            var op = rng.Next(10);
            if (op < 4 && world.Fields.Count < 12)
            {
                var desc = Desc(e, world);
                e.Begin("CreateForceField " + world + " shape=" + desc.Shape + " kind=" + desc.Kind);
                var code = e.End(NativeMethods.Aura_CreateForceField(world.Handle, ref desc, out var handle));
                SoakChecks.World(e, world, "CreateForceField", code);
                if (code != SoakCodes.Success)
                    return;
                e.Mix(handle.Opaque);
                if (world.FieldByHandle.TryGetValue(handle.Opaque, out var existing))
                    e.Fail(existing.State == SoakHandleState.Dead
                        ? "HANDLE CONFUSION: CreateForceField reissued destroyed handle " + handle.Opaque + " in " + world
                        : "HANDLE CONFUSION: CreateForceField returned in-use handle " + handle.Opaque + " in " + world);
                var field = new SoakField { Handle = handle.Opaque };
                world.Fields.Add(field);
                world.FieldByHandle[handle.Opaque] = field;
                return;
            }

            ulong h;
            SoakHandleState state;
            if (rng.Chance(0.8f) && world.Fields.Count > 0)
            {
                var f = world.Fields[rng.Next(world.Fields.Count)];
                h = f.Handle;
                state = f.State;
            }
            else
            {
                h = rng.Chance(0.5f) ? ulong.MaxValue : ((ulong)rng.Range(0, 3) << 32) | (uint)rng.Range(0, 100);
                state = world.FieldByHandle.TryGetValue(h, out var known) ? known.State : SoakHandleState.Dead;
            }

            var handle2 = new NativeForceFieldHandle { Opaque = h };
            if (op < 7)
            {
                var desc = Desc(e, world);
                e.Begin("UpdateForceField " + world + " " + h);
                var code = e.End(NativeMethods.Aura_UpdateForceField(world.Handle, handle2, ref desc));
                SoakChecks.Handle(e, world, state, "UpdateForceField", code);
            }
            else
            {
                e.Begin("DestroyForceField " + world + " " + h);
                var code = e.End(NativeMethods.Aura_DestroyForceField(world.Handle, handle2));
                SoakChecks.Handle(e, world, state, "DestroyForceField", code);
                if (code == SoakCodes.Success && world.FieldByHandle.TryGetValue(h, out var gone))
                    gone.State = SoakHandleState.Dead;
            }
        }
    }
}
