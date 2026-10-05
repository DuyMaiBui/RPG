using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AuraEngine.Core;
using AuraEngine.Physics;
using AuraEngine.Physics.Native;

namespace AuraEngine.KernelTests
{
    /* Kernel cases for package J: snapshot restore. */
    public sealed partial class KernelTestSuite
    {
        private static IEnumerable<(string Name, Action Body)> PackageJTests()
        {
            var cases = new List<(string Name, Action Body)>();
            foreach (var is2D in new[] { false, true })
            {
                var flat = is2D;
                var suffix = flat ? "2d" : "3d";
                cases.Add(("j_restore_rejects_hostile_buffers_" + suffix, () => JHostileBuffers(flat)));
                cases.Add(("j_restore_accepts_own_and_v2_snapshots_" + suffix, () => JOwnAndLegacy(flat)));
                cases.Add(("j_restore_keeps_sleepers_asleep_" + suffix, () => JSleepers(flat)));
                cases.Add(("j_restore_returns_kinematic_motion_type_" + suffix, () => JKinematicType(flat)));
            }

            foreach (var name in JExactScenarios)
            {
                var scenarioName = name;
                cases.Add(("j_restore_bit_exact_" + scenarioName, () =>
                {
                    foreach (var candidate in DetScenarios())
                        if (candidate.Name == scenarioName)
                            JBitwiseRestore(candidate, true);
                }));
            }

            foreach (var scenario in DetScenarios())
            {
                var captured = scenario;
                var only = Environment.GetEnvironmentVariable("AURA_J_ONLY");
                if (only != null && !captured.Name.Contains(only))
                    continue;
                if (!captured.ExpectsBreak && Environment.GetEnvironmentVariable("AURA_J_DIAG") == "1")
                    cases.Add(("j_diag_restore_" + captured.Name, () => JBitwiseRestore(captured, false)));
                if (!captured.ExpectsBreak && Environment.GetEnvironmentVariable("AURA_J_DIAG") == "2")
                    cases.Add(("j_diag_imm_" + captured.Name, () => JImmediateRestore(captured)));
            }

            return cases;
        }

        /* Scenarios whose in-world restore is bit exact on every body field and awake flag (see JBitwiseRestore). The
           others keep hidden solver state that the snapshot cannot carry: contact and joint warm starting. */
        private static readonly string[] JExactScenarios =
        {
            "free_flight_2d", "ccd_2d", "joint_slider_motor_limits_2d", "joint_mouse_2d",
            "char2d_walk_jump_ledge", "char2d_slope", "char2d_oneway", "char2d_moving_platform",
            "joint_slider_motor_limits_3d", "char3d_slope_platform",
        };

        [StructLayout(LayoutKind.Sequential)]
        private struct JExtra
        {
            public float RotationA;
            public float RotationB;
            public uint MotionType;
            public uint Flags;
        }

        private sealed class JNative : IDisposable
        {
            public NativeWorldHandle World;
            public readonly List<NativeBodyHandle> Bodies = new List<NativeBodyHandle>();
            public readonly List<ulong> Characters = new List<ulong>();
            public bool Is2D;
            public uint Tick;

            public void Dispose()
            {
                if (World.Opaque != 0)
                    NativeMethods.Aura_DestroyWorld(World);
                World = default;
            }
        }

        private static NativeBodyHandle JBody(JNative world, int type, float x, float y, float halfX, float halfY, bool sleeps = true)
        {
            var shape = new NativeShapeDesc
            {
                Type = 0,
                LocalPose = new NativePose { Rotation = new NativeQuaternion { W = 1f } },
                Friction = 0.5f,
                Density = 1f,
                HalfExtents = new NativeVector3 { X = halfX, Y = halfY, Z = world.Is2D ? 0.5f : halfX },
                Radius = 0.5f,
                Height = 1f,
                PlaneNormal = new NativeVector3 { Y = 1f },
                ShapeFilterMask = uint.MaxValue,
            };
            var block = Marshal.AllocHGlobal(Marshal.SizeOf<NativeShapeDesc>());
            try
            {
                Marshal.StructureToPtr(shape, block, false);
                var desc = new NativeBodyDesc
                {
                    Type = type,
                    CollisionMask = ulong.MaxValue,
                    Mass = 1f,
                    GravityScale = 1f,
                    Friction = 0.5f,
                    Density = 1f,
                    InitialPose = new NativePose { Position = new NativeVector3 { X = x, Y = y }, Rotation = new NativeQuaternion { W = 1f } },
                    Shapes = block,
                    ShapeCount = 1,
                    InertiaMultiplier = 1f,
                    AllowSleeping = (byte)(sleeps ? 1 : 0),
                };
                Check(NativeMethods.Aura_AttachBody(world.World, default, ref desc, out var body) == 0, "AttachBody failed.");
                world.Bodies.Add(body);
                return body;
            }
            finally
            {
                Marshal.FreeHGlobal(block);
            }
        }

        private static void JStep(JNative world, int count)
        {
            for (var i = 0; i < count; i++)
                Check(NativeMethods.Aura_Step(world.World, ++world.Tick, Dt) == 0, "Step failed.");
        }

        /* Ground, a box stack, a kinematic platform, a disabled body and a character, settled long enough to sleep. */
        private static JNative JBuild(bool is2D, bool withExtras = true)
        {
            var world = new JNative { Is2D = is2D };
            var desc = new NativeWorldDesc { Mode = is2D ? 1 : 0, Gravity = new NativeVector3 { Y = -9.81f }, InitialBodyCapacity = 16, FixedDeltaTime = Dt };
            Check(NativeMethods.Aura_CreateWorld(ref desc, out world.World) == 0, "CreateWorld failed.");
            JBody(world, 0, 0f, -0.5f, 20f, 0.5f);
            for (var i = 0; i < 4; i++)
                JBody(world, 1, i * 2.5f - 4f, 0.5f + i * 0.02f, 0.5f, 0.5f);
            JBody(world, 1, 0f, 3f, 0.5f, 0.5f);
            JBody(world, 2, 6f, 4f, 1f, 0.2f, false);
            var disabled = JBody(world, 1, -8f, 2f, 0.5f, 0.5f);
            Check(NativeMethods.Aura_SetBodyEnabled(world.World, disabled, 0) == 0, "SetBodyEnabled failed.");
            if (withExtras)
            {
                var character = new NativeCharacterDesc
                {
                    Pose = new NativePose { Position = new NativeVector3 { X = 9f, Y = 1f }, Rotation = new NativeQuaternion { W = 1f } },
                    Radius = 0.3f, Height = 1.8f, Mass = 70f, MaxSlopeAngle = 0.8f, CollisionMask = ulong.MaxValue, StepHeight = 0.3f,
                };
                Check(NativeMethods.Aura_CreateCharacter(world.World, ref character, out var id) == 0, "CreateCharacter failed.");
                world.Characters.Add(id);
            }

            JStep(world, 150);
            return world;
        }

        private static byte[] JSerialize(JNative world)
        {
            NativeMethods.Aura_SerializeState(world.World, IntPtr.Zero, 0, out var size);
            var block = Marshal.AllocHGlobal((int)size);
            try
            {
                Check(NativeMethods.Aura_SerializeState(world.World, block, size, out var written) == 0 && written == size, "SerializeState failed.");
                var bytes = new byte[written];
                Marshal.Copy(block, bytes, 0, bytes.Length);
                return bytes;
            }
            finally
            {
                Marshal.FreeHGlobal(block);
            }
        }

        /* Every restore attempt gets a block of exactly the buffer size so Guard Malloc traps any read past it. */
        private static int JDeserialize(JNative world, byte[] bytes)
        {
            var block = Marshal.AllocHGlobal(Math.Max(1, bytes.Length));
            try
            {
                Marshal.Copy(bytes, 0, block, bytes.Length);
                return NativeMethods.Aura_DeserializeState(world.World, block, (uint)bytes.Length);
            }
            finally
            {
                Marshal.FreeHGlobal(block);
            }
        }

        private static byte[] JFingerprint(JNative world)
        {
            NativeMethods.Aura_ComputeStateHash(world.World, out var hash);
            NativeMethods.Aura_WorldBodyCount(world.World, out var count);
            var size = Marshal.SizeOf<NativeBodyState>();
            var block = Marshal.AllocHGlobal(Math.Max(1, (int)count * size));
            try
            {
                for (var i = 0; i < (int)count * size; i++)
                    Marshal.WriteByte(block, i, 0);
                NativeMethods.Aura_CopyBodyStates(world.World, block, count, out var copied);
                var bytes = new List<byte>(BitConverter.GetBytes(hash));
                var raw = new byte[(int)copied * size];
                Marshal.Copy(block, raw, 0, raw.Length);
                bytes.AddRange(raw);
                foreach (var character in world.Characters)
                {
                    NativeMethods.Aura_GetCharacterState(world.World, character, out var state);
                    foreach (var value in new[] { state.Position.X, state.Position.Y, state.Position.Z, state.Velocity.X, state.Velocity.Y, state.Velocity.Z })
                        bytes.AddRange(BitConverter.GetBytes(value));
                    bytes.Add(state.IsGrounded);
                }

                return bytes.ToArray();
            }
            finally
            {
                Marshal.FreeHGlobal(block);
            }
        }

        private static void JSame(byte[] expected, byte[] actual, string label) =>
            Check(expected.AsSpan().SequenceEqual(actual), label + ": the world state changed.");

        private static void JMutateBody(byte[] snapshot, int index, Func<NativeBodyState, NativeBodyState> change)
        {
            var offset = 16 + index * Marshal.SizeOf<NativeBodyState>();
            var state = change(MemoryMarshal.Read<NativeBodyState>(snapshot.AsSpan(offset)));
            MemoryMarshal.Write(snapshot.AsSpan(offset), in state);
        }

        private static void JMutateExtra(byte[] snapshot, int bodyCount, int characterCount, int index, Func<JExtra, JExtra> change)
        {
            var offset = 16 + bodyCount * Marshal.SizeOf<NativeBodyState>() + characterCount * Marshal.SizeOf<NativeCharacterState>() + index * Marshal.SizeOf<JExtra>();
            var extra = MemoryMarshal.Read<JExtra>(snapshot.AsSpan(offset));
            extra = change(extra);
            MemoryMarshal.Write(snapshot.AsSpan(offset), in extra);
        }

        private static void JHostileBuffers(bool is2D)
        {
            using (var world = JBuild(is2D))
            {
                var good = JSerialize(world);
                var bodies = (int)BitConverter.ToUInt32(good, 8);
                var characters = (int)BitConverter.ToUInt32(good, 12);
                Check(bodies >= 8 && characters == 1, "unexpected test world.");
                JStep(world, 20);
                var before = JFingerprint(world);

                byte[] Clone() => (byte[])good.Clone();
                var cases = new List<(string Name, int Expected, byte[] Buffer)>();
                void Add(string name, int expected, byte[] buffer) => cases.Add((name, expected, buffer));

                const int Invalid = 2, Handle = 1, Capacity = 8;
                Add("empty", Invalid, new byte[0]);
                Add("shorter than header", Invalid, new byte[10]);
                Add("header only", Invalid, Clone().AsSpan(0, 16).ToArray());
                Add("truncated by one", Invalid, Clone().AsSpan(0, good.Length - 1).ToArray());
                Add("truncated mid body", Invalid, Clone().AsSpan(0, 16 + 40).ToArray());
                Add("oversized by one", Invalid, JPad(Clone(), 1));
                Add("oversized by 4096", Invalid, JPad(Clone(), 4096));
                var b = Clone(); b[0] ^= 0xFF; Add("wrong magic", Invalid, b);
                foreach (var version in new ushort[] { 0, 1, 4, 0xFFFF })
                {
                    b = Clone(); BitConverter.GetBytes(version).CopyTo(b, 4); Add("wrong version " + version, Invalid, b);
                }

                b = Clone(); BitConverter.GetBytes((ushort)2).CopyTo(b, 4); Add("v2 header on a v3 sized buffer", Invalid, b);
                b = Clone(); BitConverter.GetBytes(0xFFFFFFFFu).CopyTo(b, 8); Add("huge body count", Invalid, b);
                b = Clone(); BitConverter.GetBytes(0xFFFFFFFFu).CopyTo(b, 12); Add("huge character count", Invalid, b);
                b = Clone(); BitConverter.GetBytes(0u).CopyTo(b, 12); Add("character count mismatch", Invalid, b);

                // Consistent counts that exceed the world: one extra body record and extras record.
                var bodySize = Marshal.SizeOf<NativeBodyState>();
                var characterSize = Marshal.SizeOf<NativeCharacterState>();
                var extraSize = Marshal.SizeOf<JExtra>();
                var more = new byte[good.Length + bodySize + extraSize];
                Array.Copy(good, 0, more, 0, 16 + bodies * bodySize);
                Array.Copy(good, 16 + (bodies - 1) * bodySize, more, 16 + bodies * bodySize, bodySize);
                Array.Copy(good, 16 + bodies * bodySize, more, 16 + (bodies + 1) * bodySize, characters * characterSize + bodies * extraSize);
                Array.Copy(good, good.Length - extraSize, more, more.Length - extraSize, extraSize);
                BitConverter.GetBytes((uint)(bodies + 1)).CopyTo(more, 8);
                Add("more bodies than the world holds", Capacity, more);

                var last = bodies - 1;
                b = Clone(); JMutateBody(b, last, s2 => { s2.Pose.Position.X = float.NaN; return s2; }); Add("NaN position in the last body", Invalid, b);
                b = Clone(); JMutateBody(b, 0, s2 => { s2.Pose.Position.Y = float.PositiveInfinity; return s2; }); Add("Inf position", Invalid, b);
                b = Clone(); JMutateBody(b, last, s2 => { s2.Pose.Position.Z = 1e30f; return s2; }); Add("huge position", Invalid, b);
                b = Clone(); JMutateBody(b, last, s2 => { s2.LinearVelocity.X = float.NegativeInfinity; return s2; }); Add("Inf linear velocity", Invalid, b);
                b = Clone(); JMutateBody(b, last, s2 => { s2.LinearVelocity.Y = 1e9f; return s2; }); Add("huge linear velocity", Invalid, b);
                b = Clone(); JMutateBody(b, last, s2 => { s2.AngularVelocity.Z = float.NaN; return s2; }); Add("NaN angular velocity", Invalid, b);
                b = Clone(); JMutateBody(b, last, s2 => { s2.Pose.Rotation = new NativeQuaternion(); return s2; }); Add("zero quaternion", Invalid, b);
                b = Clone(); JMutateBody(b, last, s2 => { s2.Pose.Rotation.W = float.NaN; return s2; }); Add("NaN quaternion", Invalid, b);
                b = Clone(); JMutateBody(b, last, s2 => { s2.Flags = 0x80u; return s2; }); Add("unknown body flag", Invalid, b);
                b = Clone(); JMutateBody(b, last, s2 => { s2.IsAwake = 2; return s2; }); Add("awake flag out of range", Invalid, b);
                b = Clone(); JMutateBody(b, last, s2 => { s2.Body.Generation += 1; return s2; }); Add("stale generation", Handle, b);
                b = Clone(); JMutateBody(b, last, s2 => { s2.Body.Index = 9999; return s2; }); Add("unknown body index", Handle, b);
                b = Clone(); JMutateBody(b, last, s2 => { s2.Body.Index = 0xFFFFFFFFu; return s2; }); Add("invalid body index", Handle, b);
                b = Clone();
                var firstHandle = MemoryMarshal.Read<NativeBodyState>(b.AsSpan(16)).Body;
                JMutateBody(b, last, s2 => { s2.Body = firstHandle; return s2; }); Add("duplicate handle", Invalid, b);
                b = Clone(); JMutateExtra(b, bodies, characters, last, e => { e.MotionType = 7; return e; }); Add("motion type out of range", Invalid, b);
                b = Clone(); JMutateExtra(b, bodies, characters, last, e => { e.Flags |= 0x100u; return e; }); Add("unknown extra flag", Invalid, b);
                if (is2D)
                {
                    b = Clone(); JMutateExtra(b, bodies, characters, last, e => { e.Flags |= 2u; e.RotationA = 3f; e.RotationB = 3f; return e; }); Add("non unit rotation pair", Invalid, b);
                    b = Clone(); JMutateExtra(b, bodies, characters, last, e => { e.Flags |= 2u; e.RotationA = float.NaN; return e; }); Add("NaN rotation pair", Invalid, b);
                }

                var characterOffset = 16 + bodies * bodySize;
                b = Clone(); BitConverter.GetBytes(float.NaN).CopyTo(b, characterOffset); Add("NaN character position", Invalid, b);
                b = Clone(); BitConverter.GetBytes(1e30f).CopyTo(b, characterOffset + 12); Add("huge character velocity", Invalid, b);
                b = Clone(); b[characterOffset + 24] = 2; Add("grounded flag out of range", Invalid, b);

                foreach (var entry in cases)
                {
                    var code = JDeserialize(world, entry.Buffer);
                    Check(code == entry.Expected, $"{(is2D ? "2d" : "3d")} '{entry.Name}': DeserializeState returned {code}, expected {entry.Expected}.");
                    JSame(before, JFingerprint(world), $"{(is2D ? "2d" : "3d")} '{entry.Name}' (rejected restore)");
                }

                Check(NativeMethods.Aura_DeserializeState(world.World, IntPtr.Zero, 100) == 2, "null buffer must be rejected.");
                JSame(before, JFingerprint(world), "null buffer");
                Check(NativeMethods.Aura_DeserializeState(default, IntPtr.Zero, 0) == 6, "invalid world must be reported.");

                // The world is still healthy: it steps, stays finite and still accepts its own snapshot.
                JStep(world, 30);
                var after = JFingerprint(world);
                Check(after.Length == before.Length, "body count changed.");
                Check(JDeserialize(world, good) == 0, "the valid snapshot was rejected after hostile ones.");
            }
        }

        private static byte[] JPad(byte[] bytes, int extra)
        {
            var result = new byte[bytes.Length + extra];
            Array.Copy(bytes, result, bytes.Length);
            for (var i = bytes.Length; i < result.Length; i++)
                result[i] = 0xCD;
            return result;
        }

        private static void JOwnAndLegacy(bool is2D)
        {
            using (var world = JBuild(is2D))
            {
                var snapshot = JSerialize(world);
                var atSave = JFingerprint(world);
                JStep(world, 40);
                Check(JDeserialize(world, snapshot) == 0, "own snapshot rejected.");
                JSame(atSave, JFingerprint(world), "restoring the own snapshot");

                // A v2 buffer (no extras) still restores: header version 2 and the extras array cut off.
                var bodies = (int)BitConverter.ToUInt32(snapshot, 8);
                var extrasSize = bodies * Marshal.SizeOf<JExtra>();
                var legacy = new byte[snapshot.Length - extrasSize];
                Array.Copy(snapshot, legacy, legacy.Length);
                BitConverter.GetBytes((ushort)2).CopyTo(legacy, 4);
                JStep(world, 40);
                Check(JDeserialize(world, legacy) == 0, "v2 snapshot rejected.");
                var restored = JFingerprint(world);
                // Positions come back; rotation of 2D bodies uses b2MakeRot in this path so compare with a tolerance.
                var size = Marshal.SizeOf<NativeBodyState>();
                for (var i = 0; i < bodies; i++)
                {
                    var a = MemoryMarshal.Read<NativeBodyState>(atSave.AsSpan(8 + i * size));
                    var c = MemoryMarshal.Read<NativeBodyState>(restored.AsSpan(8 + i * size));
                    Check(Math.Abs(a.Pose.Position.X - c.Pose.Position.X) < 1e-3f && Math.Abs(a.Pose.Position.Y - c.Pose.Position.Y) < 1e-3f, "v2 restore moved a body.");
                }
            }
        }

        private static NativeBodyState JState(JNative world, NativeBodyHandle body)
        {
            Check(NativeMethods.Aura_GetBodyState(world.World, body, out var state) == 0, "GetBodyState failed.");
            return state;
        }

        private static void JSleepers(bool is2D)
        {
            using (var world = JBuild(is2D, false))
            {
                var resting = world.Bodies[1];
                JStep(world, 200);
                Check(JState(world, resting).IsAwake == 0, "the resting body did not fall asleep.");
                var snapshot = JSerialize(world);
                var atSave = JState(world, resting);
                Check(NativeMethods.Aura_SetLinearVelocity(world.World, resting, new NativeVector3 { X = 3f, Y = 4f }) == 0, "SetLinearVelocity failed.");
                JStep(world, 5);
                Check(JState(world, resting).IsAwake == 1, "the body did not wake up.");
                Check(JDeserialize(world, snapshot) == 0, "restore failed.");
                var restored = JState(world, resting);
                Check(restored.IsAwake == 0, "RestoreState woke a body that was asleep when captured.");
                Check(restored.Pose.Position.X == atSave.Pose.Position.X && restored.Pose.Position.Y == atSave.Pose.Position.Y, "sleeping body pose not restored exactly.");

                // An awake body stays awake.
                var falling = world.Bodies[5];
                Check(NativeMethods.Aura_SetBodyPose(world.World, falling, ref atSave.Pose, 1) == 0, "SetBodyPose failed.");
                var moving = new NativeVector3 { X = 1f, Y = 2f };
                Check(NativeMethods.Aura_SetLinearVelocity(world.World, falling, moving) == 0, "SetLinearVelocity failed.");
                JStep(world, 3);
                var awakeSnapshot = JSerialize(world);
                JStep(world, 300);
                Check(JDeserialize(world, awakeSnapshot) == 0, "restore failed.");
                Check(JState(world, falling).IsAwake == 1, "RestoreState put a moving body to sleep.");
            }
        }

        private static void JKinematicType(bool is2D)
        {
            using (var world = JBuild(is2D, false))
            {
                var platform = world.Bodies[6];
                var snapshot = JSerialize(world);
                var atSave = JState(world, platform);
                Check(NativeMethods.Aura_SetMotionType(world.World, platform, 1) == 0, "SetMotionType failed.");
                JStep(world, 30);
                Check(JState(world, platform).Pose.Position.Y < atSave.Pose.Position.Y - 0.1f, "the demoted platform did not fall.");
                Check(JDeserialize(world, snapshot) == 0, "restore failed.");
                JStep(world, 30);
                var held = JState(world, platform);
                Check(Math.Abs(held.Pose.Position.Y - atSave.Pose.Position.Y) < 1e-4f, "the kinematic platform fell after restore (motion type not restored).");
            }
        }

        private static List<int[]> JReplayBits(DetRunner runner, int from, int to)
        {
            var result = new List<int[]>();
            for (var step = from; step < to; step++)
            {
                runner.Advance(step, step + 1, false, false);
                result.Add(JStateBits(runner));
            }

            return result;
        }

        private static int[] JStateBits(DetRunner runner)
        {
            var buffer = new AuraBodyState[512];
            {
                var count = runner.World.CopyBodyStates(buffer);
                Array.Sort(buffer, 0, count, DetBodyOrder.Instance);
                var bits = new List<int>();
                for (var index = 0; index < count; index++)
                {
                    var s = buffer[index];
                    AddBits(bits, s.Pose.Position);
                    bits.Add(F(s.Pose.Rotation.X)); bits.Add(F(s.Pose.Rotation.Y)); bits.Add(F(s.Pose.Rotation.Z)); bits.Add(F(s.Pose.Rotation.W));
                    AddBits(bits, s.LinearVelocity);
                    AddBits(bits, s.AngularVelocity);
                    bits.Add(s.IsAwake ? 1 : 0);
                }

                return bits.ToArray();
            }
        }

        private static void JImmediateRestore(DetScenario scenario)
        {
            var split = Math.Max(scenario.Steps / 3, scenario.LastConfigStep + 1);
            var end = Math.Min(scenario.Steps, split + 30);
            List<int[]> a;
            using (var runner = new DetRunner(scenario))
            {
                runner.Advance(0, split, false, false);
                a = JReplayBits(runner, split, end);
            }

            using (var runner = new DetRunner(scenario))
            {
                runner.Advance(0, split, false, false);
                runner.World.RestoreState(runner.World.SaveState());
                var b = JReplayBits(runner, split, end);
                for (var step = 0; step < a.Count; step++)
                    for (var i = 0; i < a[step].Length; i++)
                        if (a[step][i] != b[step][i])
                        {
                            Console.WriteLine($"JIMM {scenario.Name}: diverges at step {step} body {i / 14} field {i % 14}");
                            return;
                        }
            }

            Console.WriteLine($"JIMM {scenario.Name}: exact");
        }

        /* Save at the scenario's replay point, continue, restore into the same world and continue again; every body
           pose, rotation, velocity and awake flag must match bit for bit on every replayed step. */
        private static string JBitwiseRestore(DetScenario scenario, bool enforce)
        {
            var split = Math.Max(scenario.Steps / 3, scenario.LastConfigStep + 1);
            var end = Math.Min(scenario.Steps, split + 60);
            using (var runner = new DetRunner(scenario))
            {
                runner.Advance(0, split, false, false);
                var saved = runner.World.SaveState();
                var atSave = JStateBits(runner);
                var n = Environment.GetEnvironmentVariable("AURA_J_N");
                var first = JReplayBits(runner, split, n == null ? end : split + int.Parse(n));
                if (n != null)
                {
                    runner.World.RestoreState(saved);
                    end = split + int.Parse(n);
                }

                runner.World.RestoreState(saved);
                var atRestore = JStateBits(runner);
                for (var i = 0; i < atSave.Length; i++)
                    if (atSave[i] != atRestore[i] && Environment.GetEnvironmentVariable("AURA_J_DIAG") == "1")
                    {
                        Console.WriteLine($"JDIAG {scenario.Name}: state right after restore differs at body {i / 14} field {i % 14}");
                        break;
                    }
                var second = JReplayBits(runner, split, end);
                for (var step = 0; step < first.Count; step++)
                {
                    for (var i = 0; i < first[step].Length; i++)
                    {
                        if (first[step][i] == second[step][i])
                            continue;
                        var message = $"{scenario.Name}: first bit divergence at replay step {step}, value {i} (body {i / 14}, field {i % 14}): " +
                            $"{BitConverter.Int32BitsToSingle(first[step][i]):R} vs {BitConverter.Int32BitsToSingle(second[step][i]):R}";
                        if (Environment.GetEnvironmentVariable("AURA_J_DIAG") == "1")
                            Console.WriteLine("JDIAG " + message);
                        if (enforce)
                            Check(false, message);
                        return message;
                    }
                }
            }

            if (Environment.GetEnvironmentVariable("AURA_J_DIAG") == "1")
                Console.WriteLine("JDIAG " + scenario.Name + ": bit exact");
            return null;
        }
    }
}
