using System;
using AuraEngine.Physics.Native;

namespace AuraEngine.KernelTests
{
    internal static class SoakCharacterOps
    {
        private static SoakHandleState Pick(SoakEpisode e, SoakWorld world, out ulong handle)
        {
            var rng = e.Rng;
            if (rng.Chance(0.8f) && world.Characters.Count > 0)
            {
                var c = world.Characters[rng.Next(world.Characters.Count)];
                handle = c.Handle;
                return c.State;
            }

            handle = rng.Chance(0.5f) ? ulong.MaxValue : ((ulong)rng.Range(0, 3) << 32) | (uint)rng.Range(0, 100);
            return world.CharacterByHandle.TryGetValue(handle, out var known) ? known.State : SoakHandleState.Dead;
        }

        public static void Run(SoakEpisode e, SoakWorld world)
        {
            var rng = e.Rng;
            var op = rng.Next(10);
            if (op < 2 && world.Characters.Count < 12)
            {
                var desc = new NativeCharacterDesc
                {
                    Pose = SoakValues.Pose(rng, 10f, world.Is2D),
                    Radius = SoakValues.Float(rng, 0.2f, 1f),
                    Height = SoakValues.Float(rng, 0.5f, 2.5f),
                    Mass = SoakValues.Float(rng, 1f, 100f),
                    MaxSlopeAngle = SoakValues.Float(rng, 0f, 1.4f),
                    Layer = (uint)rng.Range(0, 7),
                    CollisionMask = rng.Chance(0.7f) ? ulong.MaxValue : rng.NextU64(),
                    StepHeight = SoakValues.Float(rng, 0f, 0.6f),
                };
                desc.Pose.Position.Y = Math.Abs(desc.Pose.Position.Y) + 1f;
                e.Begin("CreateCharacter " + world);
                var code = e.End(NativeMethods.Aura_CreateCharacter(world.Handle, ref desc, out var handle));
                SoakChecks.World(e, world, "CreateCharacter", code);
                if (code != SoakCodes.Success)
                    return;
                e.Mix(handle);
                e.Log.Append("handle=" + handle);
                if (world.CharacterByHandle.TryGetValue(handle, out var existing))
                    e.Fail(existing.State == SoakHandleState.Dead
                        ? "HANDLE CONFUSION: CreateCharacter reissued destroyed handle " + handle + " in " + world
                        : "HANDLE CONFUSION: CreateCharacter returned in-use handle " + handle + " in " + world);
                var character = new SoakCharacter { Handle = handle };
                world.Characters.Add(character);
                world.CharacterByHandle[handle] = character;
                return;
            }

            var state = Pick(e, world, out var h);
            if (op < 4)
            {
                e.Begin("DestroyCharacter " + world + " " + h);
                var code = e.End(NativeMethods.Aura_DestroyCharacter(world.Handle, h));
                SoakChecks.Handle(e, world, state, "DestroyCharacter", code);
                if (code == SoakCodes.Success && world.CharacterByHandle.TryGetValue(h, out var gone))
                    gone.State = SoakHandleState.Dead;
            }
            else if (op < 9)
            {
                var move = SoakValues.Vec(rng, 6f);
                var dt = SoakValues.Float(rng, 0f, 0.1f);
                e.Begin("MoveCharacter " + world + " " + h + " dt=" + dt);
                var code = e.End(NativeMethods.Aura_MoveCharacter(world.Handle, h, move, dt));
                SoakChecks.Handle(e, world, state, "MoveCharacter", code);
            }
            else
            {
                e.Begin("GetCharacterState " + world + " " + h);
                var code = e.End(NativeMethods.Aura_GetCharacterState(world.Handle, h, out var s));
                SoakChecks.Handle(e, world, state, "GetCharacterState", code);
                if (code == SoakCodes.Success)
                {
                    SoakChecks.Finite(e, world, "character position", s.Position);
                    SoakChecks.Finite(e, world, "character velocity", s.Velocity);
                }
            }
        }

        /* Fingerprints and validates every live character. Not an operation. */
        public static void Audit(SoakEpisode e, SoakWorld world)
        {
            foreach (var c in world.Characters)
            {
                if (c.State != SoakHandleState.Live)
                    continue;
                var code = NativeMethods.Aura_GetCharacterState(world.Handle, c.Handle, out var s);
                SoakChecks.Handle(e, world, SoakHandleState.Live, "GetCharacterState(audit)", code);
                if (code != SoakCodes.Success)
                    continue;
                SoakChecks.Finite(e, world, "character position", s.Position);
                SoakChecks.Finite(e, world, "character velocity", s.Velocity);
                e.Mix(SoakDigest.MixFloat(SoakDigest.MixFloat(SoakDigest.MixFloat(c.Handle, s.Position.X), s.Position.Y), s.Position.Z));
                e.Mix(s.IsGrounded);
            }
        }
    }
}
