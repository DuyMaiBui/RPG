using System;
using System.Collections.Generic;
using AuraEngine.Core;
using AuraEngine.Physics;

namespace AuraEngine.KernelTests
{
    /* Kernel cases for package H: defects found by the soak runner (see tests/stress/repro). */
    public sealed partial class KernelTestSuite
    {
        private static IEnumerable<(string Name, Action Body)> PackageHTests()
        {
            return new (string Name, Action Body)[]
            {
                ("h_overlap_never_exceeds_capacity_2d", H_OverlapNeverExceedsCapacity2D),
                ("h_joint_on_disabled_body_is_rejected_3d", () => H_JointOnDisabledBodyIsRejected(AuraPhysicsMode.Full3D)),
                ("h_joint_on_disabled_body_is_rejected_2d", () => H_JointOnDisabledBodyIsRejected(AuraPhysicsMode.Plane2D)),
                ("h_joint_between_non_dynamic_bodies_is_rejected_3d", () => H_JointBetweenNonDynamicBodiesIsRejected(AuraPhysicsMode.Full3D)),
                ("h_joint_between_non_dynamic_bodies_is_rejected_2d", () => H_JointBetweenNonDynamicBodiesIsRejected(AuraPhysicsMode.Plane2D)),
                ("h_mouse_joint_needs_a_dynamic_body_2d", H_MouseJointNeedsADynamicBody2D),
                ("h_kinematic_target_on_non_kinematic_body_is_safe_3d", H_KinematicTargetOnNonKinematicBodyIsSafe3D),
                ("h_kinematic_target_after_zero_dt_stays_finite_3d", () => H_KinematicTargetAfterZeroDtStaysFinite(AuraPhysicsMode.Full3D)),
                ("h_kinematic_target_after_zero_dt_stays_finite_2d", () => H_KinematicTargetAfterZeroDtStaysFinite(AuraPhysicsMode.Plane2D)),
            };
        }

        /* Box2D invoked the overlap callback again for the second and third broad-phase trees after the first one asked
           to stop, so an overlap query with a small buffer wrote past its capacity (heap overrun; run under Guard Malloc). */
        private static void H_OverlapNeverExceedsCapacity2D()
        {
            using var world = NewWorld(AuraPhysicsMode.Plane2D);
            var half = new AuraVector3(0.5f, 0.5f, 0.5f);
            world.AttachBody(world.CreateEntity(), AuraPhysicsBodyDefinition.CreateStatic(
                new AuraPose(AuraVector3.Zero, AuraQuaternion.Identity), AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All, AuraPhysicsShapeDefinition.Box(half)));
            world.AttachBody(world.CreateEntity(), AuraPhysicsBodyDefinition.CreateKinematic(
                new AuraPose(AuraVector3.Zero, AuraQuaternion.Identity), AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All, AuraPhysicsShapeDefinition.Box(half)));
            world.AttachBody(world.CreateEntity(), AuraPhysicsBodyDefinition.CreateDynamic(
                new AuraPose(AuraVector3.Zero, AuraQuaternion.Identity), AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All, AuraPhysicsShapeDefinition.Sphere(0.25f)));
            Step(world, 1);

            foreach (var capacity in new[] { 1, 2 })
            {
                var buffer = new AuraPhysicsQueryHit[capacity];
                var count = world.Queries.OverlapBox(AuraVector3.Zero, new AuraVector3(2f, 2f, 2f), AuraQuaternion.Identity, AuraPhysicsQueryFilter.All, buffer);
                Check(count <= capacity, $"overlap returned {count} hits for a buffer of {capacity}.");
            }
        }

        /* A joint created on a disabled body was accepted and the next Step crashed inside Jolt's broad phase. */
        private static void H_JointOnDisabledBodyIsRejected(AuraPhysicsMode mode)
        {
            using var world = NewWorld(mode);
            var a = Dyn(world, V(0f, 5f, 0f), AuraPhysicsShapeDefinition.Box(V(0.5f, 0.5f, 0.5f)));
            var b = Dyn(world, V(2f, 5f, 0f), AuraPhysicsShapeDefinition.Box(V(0.5f, 0.5f, 0.5f)));
            Ok(world.BodyControl.SetEnabled(b, false), "SetEnabled(false)");
            var accepted = false;
            try
            {
                var joint = world.CreateJoint(FindEntity(world, a), FindEntity(world, b),
                    AuraJointDefinition.CreateDistance(a, b, V(0f, 5f, 0f), V(2f, 5f, 0f), 2f));
                accepted = joint.IsValid;
            }
            catch (AuraException)
            {
            }

            Check(!accepted, "a joint on a disabled body must be rejected.");
            Step(world, 5);
        }

        /* A constraint between a static and a kinematic body has no effective mass: the soak run crashed Box2D's solver
           (mouse joint) and produced NaN. It must be rejected at creation. */
        private static void H_JointBetweenNonDynamicBodiesIsRejected(AuraPhysicsMode mode)
        {
            using var world = NewWorld(mode);
            var half = V(0.5f, 0.5f, 0.5f);
            var fixedBody = world.AttachBody(world.CreateEntity(), AuraPhysicsBodyDefinition.CreateStatic(
                new AuraPose(V(0f, 5f, 0f), AuraQuaternion.Identity), AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All, AuraPhysicsShapeDefinition.Box(half)));
            var moving = world.AttachBody(world.CreateEntity(), AuraPhysicsBodyDefinition.CreateKinematic(
                new AuraPose(V(2f, 5f, 0f), AuraQuaternion.Identity), AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All, AuraPhysicsShapeDefinition.Box(half)));
            var accepted = false;
            try
            {
                accepted = world.CreateJoint(FindEntity(world, fixedBody), FindEntity(world, moving),
                    AuraJointDefinition.CreateDistance(fixedBody, moving, V(0f, 5f, 0f), V(2f, 5f, 0f), 2f)).IsValid;
            }
            catch (AuraException)
            {
            }

            Check(!accepted, "a joint between two non-dynamic bodies must be rejected.");
            Step(world, 5);
        }

        private static void H_MouseJointNeedsADynamicBody2D()
        {
            using var world = NewWorld(AuraPhysicsMode.Plane2D);
            var half = V(0.5f, 0.5f, 0.5f);
            var anchor = world.AttachBody(world.CreateEntity(), AuraPhysicsBodyDefinition.CreateStatic(
                new AuraPose(V(0f, 0f, 0f), AuraQuaternion.Identity), AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All, AuraPhysicsShapeDefinition.Box(half)));
            var kinematic = world.AttachBody(world.CreateEntity(), AuraPhysicsBodyDefinition.CreateKinematic(
                new AuraPose(V(2f, 2f, 0f), AuraQuaternion.Identity), AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All, AuraPhysicsShapeDefinition.Box(half)));
            var accepted = false;
            try
            {
                accepted = world.CreateJoint(FindEntity(world, anchor), FindEntity(world, kinematic),
                    AuraJointDefinition.CreateMouse(anchor, kinematic, V(2f, 2f, 0f), 5f, 0.7f, 1000f)).IsValid;
            }
            catch (AuraException)
            {
            }

            Check(!accepted, "a mouse joint on a kinematic body must be rejected.");
            Step(world, 5);
        }

        /* Plane, mesh and height-field bodies have no motion properties; a kinematic target on them crashed Jolt. A target
           on a non-kinematic body is also meaningless. Neither may crash or move the body. */
        private static void H_KinematicTargetOnNonKinematicBodyIsSafe3D()
        {
            using var world = NewWorld(AuraPhysicsMode.Full3D);
            var resolution = 5;
            var samples = new float[resolution * resolution];
            var terrain = world.AttachBody(world.CreateEntity(), AuraPhysicsBodyDefinition.CreateStatic(
                new AuraPose(AuraVector3.Zero, AuraQuaternion.Identity), AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All,
                AuraPhysicsShapeDefinition.HeightField(samples, resolution, new AuraVector3(2f, 1f, 2f))));
            var ball = Dyn(world, V(0f, 4f, 0f), AuraPhysicsShapeDefinition.Sphere(0.5f), gravityScale: 0f);
            var target = new AuraPose(V(5f, 1f, 5f), AuraQuaternion.Identity);
            world.SetKinematicTarget(FindEntity(world, terrain), target);
            world.SetKinematicTarget(FindEntity(world, ball), target);
            Step(world, 10);

            Near(StateOf(world, terrain).Pose.Position.X, 0f, 1e-4f, "terrain moved");
            Near(StateOf(world, ball).Pose.Position.X, 0f, 1e-3f, "a non-kinematic body followed a kinematic target");
        }

        /* A zero-length step left lastDelta at 0, so the next kinematic target divided by zero (inf or NaN pose). */
        private static void H_KinematicTargetAfterZeroDtStaysFinite(AuraPhysicsMode mode)
        {
            using var world = NewWorld(mode);
            var body = world.AttachBody(world.CreateEntity(), AuraPhysicsBodyDefinition.CreateKinematic(
                new AuraPose(V(0f, 5f, 0f), AuraQuaternion.Identity), AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All,
                AuraPhysicsShapeDefinition.Box(V(0.5f, 0.5f, 0.5f))));
            world.Physics.Step(0f);
            world.SetKinematicTarget(FindEntity(world, body), new AuraPose(V(1f, 5f, 0f), AuraQuaternion.Identity));
            Step(world, 3);

            var position = StateOf(world, body).Pose.Position;
            Check(!float.IsNaN(position.X + position.Y) && !float.IsInfinity(position.X + position.Y), "kinematic pose is not finite.");
            Near(position.X, 1f, 0.1f, "kinematic body should reach its target");
        }
    }
}
