using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AuraEngine.Core;
using AuraEngine.Physics;
using AuraEngine.Physics.Native;
using AuraEngine.Simulation;

namespace AuraEngine.KernelTests
{
    /* Kernel cases for package O: scale and safety. A dense awake pile must keep every body above the ground (Jolt drops
       contacts silently once its pair and constraint caches fill), and hitting the body limit must fail cleanly. */
    public sealed partial class KernelTestSuite
    {
        private static IEnumerable<(string Name, Action Body)> PackageOTests()
        {
            return new (string Name, Action Body)[]
            {
                ("o_dense_pile_of_6000_boxes_stays_on_the_ground_3d", () => OPile(AuraPhysicsMode.Full3D, 6000)),
                ("o_dense_pile_of_12000_boxes_stays_on_the_ground_2d", () => OPile(AuraPhysicsMode.Plane2D, 12000)),
                ("o_body_limit_fails_cleanly_and_world_keeps_stepping_3d", OBodyLimit),
                ("o_jolt_step_reports_full_cache_failure_3d", OJoltStepReportsFullCache),
            };
        }

        private static void OPile(AuraPhysicsMode mode, int count)
        {
            var is3D = mode == AuraPhysicsMode.Full3D;
            using var world = new AuraSimulationWorld(new NativePhysicsBackend(), new AuraWorldDefinition(mode, initialBodyCapacity: count + 16));
            world.AttachBody(world.CreateEntity(), AuraPhysicsBodyDefinition.CreateStatic(
                new AuraPose(new AuraVector3(0f, -0.5f, 0f), AuraQuaternion.Identity),
                AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All,
                AuraPhysicsShapeDefinition.Box(new AuraVector3(400f, 0.5f, is3D ? 400f : 1f))));
            var entities = new List<SimulationEntityId>(count);
            var columns = is3D ? 40 : 100;
            for (var index = 0; index < count; index++)
            {
                var x = (index % columns) * 1.02f - columns * 0.51f;
                var z = is3D ? ((index / columns) % columns) * 1.02f - columns * 0.51f : 0f;
                var y = 0.5f + (index / (is3D ? columns * columns : columns)) * 1.02f;
                var entity = world.CreateEntity();
                entities.Add(entity);
                world.AttachBody(entity, new AuraPhysicsBodyDefinition(
                    AuraBodyType.Dynamic,
                    new AuraPose(new AuraVector3(x, y, z), AuraQuaternion.Identity),
                    AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All,
                    new[] { AuraPhysicsShapeDefinition.Box(new AuraVector3(0.5f, 0.5f, 0.5f)) },
                    allowSleeping: false));
            }

            uint tick = 0;
            for (var step = 0; step < 90; step++)
                world.Step(new SimulationStep(new SimulationTick(++tick), 1f / 60f));

            var invalid = 0;
            foreach (var entity in entities)
            {
                if (!world.TryGetBodyState(entity, out var state))
                {
                    invalid++;
                    continue;
                }

                var p = state.Pose.Position;
                if (float.IsNaN(p.X) || float.IsNaN(p.Y) || float.IsNaN(p.Z) || p.Y < -0.6f)
                    invalid++;
            }

            Check(invalid == 0, $"{invalid} of {count} boxes fell through the ground or became NaN");
        }

        private static void OBodyLimit()
        {
            using var world = new AuraSimulationWorld(new NativePhysicsBackend(), new AuraWorldDefinition(AuraPhysicsMode.Full3D, initialBodyCapacity: 1024));
            AuraPhysicsBodyDefinition Sphere(int index) => new AuraPhysicsBodyDefinition(
                AuraBodyType.Static,
                new AuraPose(new AuraVector3((index % 256) * 2f, 0f, (index / 256) * 2f), AuraQuaternion.Identity),
                AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All,
                new[] { AuraPhysicsShapeDefinition.Sphere(0.5f) });

            var failedAt = -1;
            var lastEntity = default(SimulationEntityId);
            for (var index = 0; index < 70000 && failedAt < 0; index++)
            {
                var entity = world.CreateEntity();
                if (world.AttachBody(entity, Sphere(index)).IsValid)
                    lastEntity = entity;
                else
                    failedAt = index;
            }

            Check(failedAt > 60000 && failedAt < 70000, $"the body limit was reported at {failedAt}, expected roughly 65536");
            world.Step(new SimulationStep(new SimulationTick(1), 1f / 60f));

            world.DestroyEntity(lastEntity);
            Check(world.AttachBody(world.CreateEntity(), Sphere(failedAt)).IsValid, "a freed body slot cannot be reused after hitting the limit");
        }

        /* P0: a full Jolt body-pair / contact-constraint cache is a fatal step error that used to be swallowed. A tiny
           cache (via AURA_JOLT_MAX_BODY_PAIRS / AURA_JOLT_MAX_CONTACT_CONSTRAINTS) forces the error on a crowded step,
           and Aura_Step must report AURA_BACKEND_FAILURE so the host can react instead of silently dropping contacts.
           The override is set through native setenv: the Unity-bundled .NET host does not propagate
           Environment.SetEnvironmentVariable into the C environment that libaura's std::getenv reads. */
        [DllImport("libc")]
        private static extern int setenv(string name, string value, int overwrite);

        [DllImport("libc")]
        private static extern int unsetenv(string name);

        private static void OJoltStepReportsFullCache()
        {
            // These variables are test-only and no other world is created after this case, so unset in finally suffices.
            Check(setenv("AURA_JOLT_MAX_BODY_PAIRS", "4", 1) == 0, "setenv AURA_JOLT_MAX_BODY_PAIRS failed");
            Check(setenv("AURA_JOLT_MAX_CONTACT_CONSTRAINTS", "4", 1) == 0, "setenv AURA_JOLT_MAX_CONTACT_CONSTRAINTS failed");
            try
            {
                var desc = new NativeWorldDesc
                {
                    Mode = 0,
                    Gravity = new NativeVector3 { Y = -9.81f },
                    InitialBodyCapacity = 64,
                    FixedDeltaTime = 1f / 60f,
                };
                Expect((AuraResult)NativeMethods.Aura_CreateWorld(ref desc, out var world), AuraResult.Success, "CreateWorld");

                try
                {
                    OJoltBox(world, type: 0, x: 0f, y: -0.5f, halfX: 20f, halfY: 0.5f, halfZ: 20f);
                    for (var ix = 0; ix < 4; ix++)
                    for (var iy = 0; iy < 2; iy++)
                    for (var iz = 0; iz < 4; iz++)
                        OJoltBox(world, type: 1, x: (ix - 1.5f) * 0.9f, y: 0.5f + iy * 0.9f, halfX: 0.5f, halfY: 0.5f, halfZ: 0.5f, z: (iz - 1.5f) * 0.9f);

                    Expect((AuraResult)NativeMethods.Aura_Step(world, 1, 1f / 60f), AuraResult.BackendFailure,
                        "a crowded step with a tiny pair cache must report a backend failure");
                    Expect((AuraResult)NativeMethods.Aura_Step(world, 2, 1f / 60f), AuraResult.BackendFailure,
                        "the failure is reported again on the next step");

                    Expect((AuraResult)NativeMethods.Aura_WorldBodyCount(world, out var count), AuraResult.Success, "world stays alive after the failure");
                    Check(count == 33, $"body count after the failed step was {count}, expected 33");
                }
                finally
                {
                    NativeMethods.Aura_DestroyWorld(world);
                }
            }
            finally
            {
                unsetenv("AURA_JOLT_MAX_BODY_PAIRS");
                unsetenv("AURA_JOLT_MAX_CONTACT_CONSTRAINTS");
            }
        }

        private static void OJoltBox(NativeWorldHandle world, int type, float x, float y, float halfX, float halfY, float halfZ, float z = 0f)
        {
            var shape = new NativeShapeDesc
            {
                Type = 0,
                LocalPose = new NativePose { Rotation = new NativeQuaternion { W = 1f } },
                Friction = 0.5f,
                Density = 1f,
                HalfExtents = new NativeVector3 { X = halfX, Y = halfY, Z = halfZ },
                Radius = 0.5f,
                Height = 1f,
                PlaneNormal = new NativeVector3 { Y = 1f },
                ShapeFilterMask = uint.MaxValue,
            };
            var block = Marshal.AllocHGlobal(Marshal.SizeOf<NativeShapeDesc>());
            try
            {
                Marshal.StructureToPtr(shape, block, false);
                var body = new NativeBodyDesc
                {
                    Type = type,
                    CollisionMask = ulong.MaxValue,
                    Mass = 1f,
                    GravityScale = 1f,
                    Friction = 0.5f,
                    Density = 1f,
                    InitialPose = new NativePose { Position = new NativeVector3 { X = x, Y = y, Z = z }, Rotation = new NativeQuaternion { W = 1f } },
                    Shapes = block,
                    ShapeCount = 1,
                    InertiaMultiplier = 1f,
                    AllowSleeping = 0,
                };
                Check(NativeMethods.Aura_AttachBody(world, default, ref body, out _) == 0, "AttachBody failed.");
            }
            finally
            {
                Marshal.FreeHGlobal(block);
            }
        }
    }
}
