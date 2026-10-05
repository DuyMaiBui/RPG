using System;
using System.Collections.Generic;
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
    }
}
