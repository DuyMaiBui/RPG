using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using AuraEngine.Core;
using AuraEngine.Physics.Native;
using AuraEngine.Simulation;

namespace AuraEngine.KernelTests
{
    /* Kernel cases for package K: event order, broken joint handles, concurrent world creation and stale world handles. */
    public sealed partial class KernelTestSuite
    {
        private static IEnumerable<(string Name, Action Body)> PackageKTests()
        {
            /* AURA_K_ONLY=<substring> runs only the matching package K cases (to check one against a crashing older libaura). */
            var only = Environment.GetEnvironmentVariable("AURA_K_ONLY");
            return new (string Name, Action Body)[]
            {
                ("k_events_deterministic_order_3d", () => K_EventOrder(AuraPhysicsMode.Full3D)),
                ("k_events_deterministic_order_2d", () => K_EventOrder(AuraPhysicsMode.Plane2D)),
                ("k_broken_joint_handle_stays_valid_3d", () => K_BrokenJointHandle(AuraPhysicsMode.Full3D)),
                ("k_broken_joint_handle_stays_valid_2d", () => K_BrokenJointHandle(AuraPhysicsMode.Plane2D)),
                ("k_concurrent_world_create_destroy", K_ConcurrentWorlds),
                ("k_stale_world_handles_rejected", K_StaleWorldHandles),
            }.Where(test => string.IsNullOrEmpty(only) || test.Name.Contains(only));
        }

        /* A pile of overlapping spheres plus a trigger volume produces many enter/exit events per step from several
           Jolt worker threads. Replaying must give the same events in the same order, and every step's events must be
           sorted by (type, bodyA, bodyB). */
        private static string K_RunEventScenario(AuraPhysicsMode mode, ref int eventTotal)
        {
            using var world = NewWorld(mode);
            Ground(world);
            world.AttachBody(world.CreateEntity(), AuraPhysicsBodyDefinition.CreateStatic(
                new AuraPose(V(0f, 1.5f, 0f), AuraQuaternion.Identity),
                AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All,
                AuraPhysicsShapeDefinition.Box(V(1.5f, 1f, 1.5f)).AsTrigger()));
            var layers = mode == AuraPhysicsMode.Full3D ? 3 : 5;
            for (var layer = 0; layer < layers; layer++)
                for (var x = 0; x < 6; x++)
                    for (var z = 0; z < (mode == AuraPhysicsMode.Full3D ? 6 : 1); z++)
                        Dyn(world, V(-2.2f + x * 0.9f, 1f + layer * 0.9f, -2.2f + z * 0.9f), AuraPhysicsShapeDefinition.Sphere(0.5f));

            var trace = new System.Text.StringBuilder();
            var buffer = new AuraPhysicsEvent[1024];
            for (var step = 0; step < 150; step++)
            {
                Step(world, 1, (uint)step);
                var count = world.CopyEvents(buffer);
                eventTotal += count;
                for (var index = 0; index < count; index++)
                {
                    var current = buffer[index];
                    if (index > 0)
                    {
                        var previous = buffer[index - 1];
                        var order = (int)previous.Type != (int)current.Type ? ((int)previous.Type).CompareTo((int)current.Type)
                            : previous.BodyA.Index != current.BodyA.Index ? previous.BodyA.Index.CompareTo(current.BodyA.Index)
                            : previous.BodyA.Generation != current.BodyA.Generation ? previous.BodyA.Generation.CompareTo(current.BodyA.Generation)
                            : previous.BodyB.Index != current.BodyB.Index ? previous.BodyB.Index.CompareTo(current.BodyB.Index)
                            : previous.BodyB.Generation.CompareTo(current.BodyB.Generation);
                        Check(order <= 0, $"step {step}: events not sorted ({previous.Type} {previous.BodyA}/{previous.BodyB} before {current.Type} {current.BodyA}/{current.BodyB}).");
                    }
                    trace.Append((int)current.Type).Append(':').Append(current.BodyA.Index).Append(',').Append(current.BodyB.Index).Append(';');
                }
                trace.Append('|');
            }

            return trace.ToString();
        }

        private static void K_EventOrder(AuraPhysicsMode mode)
        {
            var total = 0;
            var reference = K_RunEventScenario(mode, ref total);
            var digest = 14695981039346656037ul;
            foreach (var character in reference)
                digest = (digest ^ character) * 1099511628211ul;
            // Printed so runs with different AURA_JOLT_THREADS values can be compared from outside.
            Console.WriteLine($"  k_events_digest {mode} {digest:X16}");
            Check(total > 40, $"scenario produced only {total} events.");
            for (var run = 1; run < 8; run++)
            {
                var other = K_RunEventScenario(mode, ref total);
                Check(string.Equals(reference, other, StringComparison.Ordinal), $"replay {run}: event order differs from the first run.");
            }
        }

        /* Contract: after a break the handle is live until DestroyJoint. HasJoint stays true, IsBroken reports true,
           feedback stays readable; control calls find no constraint left to act on and return UnsupportedOperation (InvalidHandle is reserved
           for destroyed, stale or garbage handles). */
        private static void K_BrokenJointHandle(AuraPhysicsMode mode)
        {
            using var world = NewWorld(mode);
            var rig = HangingRig(world, mode);
            Step(world, 60);
            Check(world.HasJoint(rig.Joint), "live joint not reported live.");
            Ok(world.JointControl.SetBreakThreshold(rig.Joint, rig.Newtons * 0.5f, 0f), "SetBreakThreshold");
            Step(world, 10, 60);
            Ok(world.JointControl.IsBroken(rig.Joint, out var broken), "IsBroken");
            Check(broken, "joint did not break.");
            Check(world.HasJoint(rig.Joint), "HasJoint must stay true for a broken joint until DestroyJoint.");
            Ok(world.JointControl.GetFeedback(rig.Joint, out var feedback), "feedback after break");
            Check(feedback.IsBroken, "feedback does not report the break.");
            Expect(world.JointControl.SetBreakThreshold(rig.Joint, 1f, 0f), AuraResult.UnsupportedOperation, "threshold on broken joint");
            Step(world, 20, 70);
            Check(world.HasJoint(rig.Joint), "HasJoint turned false while the broken joint was still owned.");
            Ok(world.DestroyJoint(rig.Joint), "DestroyJoint of a broken joint");
            Check(!world.HasJoint(rig.Joint), "HasJoint still true after DestroyJoint.");
            Expect(world.JointControl.IsBroken(rig.Joint, out _), AuraResult.InvalidHandle, "IsBroken after destroy");
        }

        /* Many threads create, step and destroy worlds at once (also the first creation of each backend). */
        private static void K_ConcurrentWorlds()
        {
            const int threadCount = 16;
            var failures = new List<string>();
            var barrier = new Barrier(threadCount);
            var threads = Enumerable.Range(0, threadCount).Select(t => new Thread(() =>
            {
                try
                {
                    barrier.SignalAndWait();
                    for (var round = 0; round < 6; round++)
                    {
                        using var world = NewWorld((t + round) % 2 == 0 ? AuraPhysicsMode.Full3D : AuraPhysicsMode.Plane2D);
                        Ground(world);
                        Ball(world, 2f);
                        Step(world, 5);
                    }
                }
                catch (Exception exception)
                {
                    lock (failures)
                        failures.Add($"thread {t}: {exception.Message}");
                }
            })).ToArray();
            foreach (var thread in threads)
                thread.Start();
            foreach (var thread in threads)
                thread.Join();
            Check(failures.Count == 0, string.Join("; ", failures));
        }

        /* Stale, doubled and garbage world handles answer InvalidWorld instead of touching freed memory. */
        private static void K_StaleWorldHandles()
        {
            foreach (var mode in new[] { 0, 1 })
            {
                var desc = new NativeWorldDesc { Mode = mode, Gravity = new NativeVector3 { Y = -9.81f }, InitialBodyCapacity = 4, FixedDeltaTime = 1f / 60f };
                Expect((AuraResult)NativeMethods.Aura_CreateWorld(ref desc, out var world), AuraResult.Success, "CreateWorld");
                Check(world.Opaque != 0, "CreateWorld returned a null handle.");
                Expect((AuraResult)NativeMethods.Aura_Step(world, 1, 1f / 60f), AuraResult.Success, "step live world");
                Expect((AuraResult)NativeMethods.Aura_DestroyWorld(world), AuraResult.Success, "destroy");
                K_ExpectWorldInvalid(world, "destroyed world");
                Expect((AuraResult)NativeMethods.Aura_DestroyWorld(world), AuraResult.InvalidWorld, "second destroy");

                // The slot is recycled for the next world; the stale handle must not alias it.
                var desc2 = desc;
                Expect((AuraResult)NativeMethods.Aura_CreateWorld(ref desc2, out var fresh), AuraResult.Success, "CreateWorld 2");
                Check(fresh.Opaque != world.Opaque, "a new world reused the exact stale handle value.");
                K_ExpectWorldInvalid(world, "stale handle after slot reuse");
                Expect((AuraResult)NativeMethods.Aura_Step(fresh, 1, 1f / 60f), AuraResult.Success, "step new world");
                Expect((AuraResult)NativeMethods.Aura_DestroyWorld(fresh), AuraResult.Success, "destroy new world");
            }

            foreach (var garbage in new ulong[] { 0ul, ulong.MaxValue, 0x00007FFF12345678ul, (0xDEADBEEFul << 32) | 1ul, 1ul << 32 })
                K_ExpectWorldInvalid(new NativeWorldHandle { Opaque = garbage }, $"garbage handle 0x{garbage:X}");
        }

        private static void K_ExpectWorldInvalid(NativeWorldHandle world, string label)
        {
            Expect((AuraResult)NativeMethods.Aura_Step(world, 1, 1f / 60f), AuraResult.InvalidWorld, label + " Step");
            Expect((AuraResult)NativeMethods.Aura_WorldBodyCount(world, out _), AuraResult.InvalidWorld, label + " WorldBodyCount");
            Expect((AuraResult)NativeMethods.Aura_PendingEventCount(world, out _), AuraResult.InvalidWorld, label + " PendingEventCount");
            Expect((AuraResult)NativeMethods.Aura_HasJoint(world, 1ul, out _), AuraResult.InvalidWorld, label + " HasJoint");
            Expect((AuraResult)NativeMethods.Aura_CopyBodyStates(world, IntPtr.Zero, 0, out _), AuraResult.InvalidWorld, label + " CopyBodyStates");
        }
    }
}
