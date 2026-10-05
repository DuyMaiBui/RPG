using System;
using System.Collections.Generic;
using AuraEngine.Core;
using AuraEngine.Physics;
using AuraEngine.Physics.Native;

namespace AuraEngine.KernelTests
{
    /* Kernel cases for package F: kinematic targets under variable frame rates. */
    public sealed partial class KernelTestSuite
    {
        private static IEnumerable<(string Name, Action Body)> PackageFTests()
        {
            return new (string Name, Action Body)[]
            {
                ("kinematic_target_is_consumed_by_one_step_3d", () => KinematicTarget_IsConsumedByOneStep(AuraPhysicsMode.Full3D)),
                ("kinematic_target_is_consumed_by_one_step_2d", () => KinematicTarget_IsConsumedByOneStep(AuraPhysicsMode.Plane2D)),
                ("live_world_count_tracks_create_and_destroy", LiveWorldCount_TracksCreateAndDestroy),
            };
        }

        private static void LiveWorldCount_TracksCreateAndDestroy()
        {
            var before = NativePhysicsDiagnostics.LiveWorldCount();
            var first = NewWorld(AuraPhysicsMode.Full3D);
            var second = NewWorld(AuraPhysicsMode.Plane2D);
            Check(NativePhysicsDiagnostics.LiveWorldCount() == before + 2, "two live worlds expected.");
            ((IDisposable)first).Dispose();
            Check(NativePhysicsDiagnostics.LiveWorldCount() == before + 1, "one world should remain after disposing the first.");
            ((IDisposable)second).Dispose();
            Check(NativePhysicsDiagnostics.LiveWorldCount() == before, "the counter must return to its start after disposal.");
        }

        /* A slow frame runs several fixed steps per kinematic target. The body must stop at the target instead of
           carrying on with the velocity that reached it (which compounded into runaway positions at low frame rates). */
        private static void KinematicTarget_IsConsumedByOneStep(AuraPhysicsMode mode)
        {
            using var world = NewWorld(mode);
            var body = world.AttachBody(world.CreateEntity(), AuraPhysicsBodyDefinition.CreateKinematic(
                new AuraPose(new AuraVector3(0f, 5f, 0f), AuraQuaternion.Identity),
                AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All, AuraPhysicsShapeDefinition.Box(new AuraVector3(0.5f, 0.5f, 0.5f))));
            var entity = FindEntity(world, body);
            var x = 0f;
            for (var frame = 0; frame < 20; frame++)
            {
                x += 0.5f;
                world.SetKinematicTarget(entity, new AuraPose(new AuraVector3(x, 5f, 0f), AuraQuaternion.Identity));
                Step(world, 6, (uint)(frame * 6));
                world.TryGetBodyState(body, out var state);
                Near(state.Pose.Position.X, x, 0.05f, "kinematic body overshot its target on frame " + frame);
            }
        }
    }
}
