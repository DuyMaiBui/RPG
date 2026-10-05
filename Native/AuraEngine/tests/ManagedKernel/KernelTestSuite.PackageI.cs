using System;
using System.Collections.Generic;
using AuraEngine.Core;
using AuraEngine.Physics;
using AuraEngine.Physics.Native;
using AuraEngine.Simulation;

namespace AuraEngine.KernelTests
{
    /* Kernel cases for package I: Box2D joint stability and the broken-joint handle contract. */
    public sealed partial class KernelTestSuite
    {
        private static IEnumerable<(string Name, Action Body)> PackageITests()
        {
            return new (string Name, Action Body)[]
            {
                ("box2d_slider_limit_far_outside_settles", PackageI_SliderLimitFarOutsideSettles),
                ("box2d_hinge_limit_relative_to_creation_pose", PackageI_HingeLimitRelativeToCreationPose),
                ("box2d_distance_far_start_is_eased", () => PackageI_FarStartIsEased(AuraJointType.Distance)),
                ("box2d_rope_far_start_is_eased", () => PackageI_FarStartIsEased(AuraJointType.Rope)),
                ("box2d_spring_large_step_stays_finite", PackageI_SpringLargeStepStaysFinite),
                ("box2d_impossible_joint_definitions_rejected", PackageI_ImpossibleDefinitionsRejected),
                ("box2d_ill_conditioned_anchors_rejected", PackageI_IllConditionedAnchorsRejected),
                ("box2d_broken_joint_handle_stays_valid", PackageI_BrokenJointHandleStaysValid),
            };
        }

        private static PhysicsBodyId PackageI_Static(AuraSimulationWorld world, AuraVector3 position) =>
            world.AttachBody(world.CreateEntity(), AuraPhysicsBodyDefinition.CreateStatic(
                new AuraPose(position, AuraQuaternion.Identity),
                AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All, AuraPhysicsShapeDefinition.Box(V(0.1f, 0.1f, 0.1f))));

        /* Steps with an explicit delta time and returns the largest speed seen (the larger of the reported velocity and the
           displacement per step: Box2D reports the velocity after its relax pass, which hides a bias-driven jump). Throws on
           a non-finite pose. */
        private static float PackageI_StepTracking(AuraSimulationWorld world, int count, float dt, params PhysicsBodyId[] bodies)
        {
            var maxSpeed = 0f;
            var previous = new AuraVector3[bodies.Length];
            for (var i = 0; i < bodies.Length; i++)
                previous[i] = StateOf(world, bodies[i]).Pose.Position;
            for (var index = 0; index < count; index++)
            {
                world.Step(new SimulationStep(new SimulationTick((uint)index + 1u), dt));
                for (var i = 0; i < bodies.Length; i++)
                {
                    var state = StateOf(world, bodies[i]);
                    var position = state.Pose.Position;
                    var moved = (float)Math.Sqrt((position.X - previous[i].X) * (position.X - previous[i].X)
                        + (position.Y - previous[i].Y) * (position.Y - previous[i].Y)) / dt;
                    var speed = (float)Math.Sqrt(state.LinearVelocity.X * state.LinearVelocity.X + state.LinearVelocity.Y * state.LinearVelocity.Y);
                    Check(float.IsFinite(speed) && float.IsFinite(moved), $"non-finite body state at step {index}.");
                    maxSpeed = Math.Max(maxSpeed, Math.Max(speed, moved));
                    previous[i] = position;
                }
            }

            return maxSpeed;
        }

        /* A cart starts 19 m beyond the upper slider limit (the creation anchors are 20 m apart along the axis). The limit
           used to be enforced through Box2D's uncapped soft bias: >1000 m/s, a body flung across the world. */
        private static void PackageI_SliderLimitFarOutsideSettles()
        {
            using var world = NewWorld(AuraPhysicsMode.Plane2D);
            var anchor = PackageI_Static(world, V(0f, 5f, 0f));
            var cart = Dyn(world, V(20f, 5f, 0f), AuraPhysicsShapeDefinition.Box(V(0.5f, 0.5f, 0.5f)), gravityScale: 0f);
            var joint = world.CreateJoint(FindEntity(world, anchor), FindEntity(world, cart),
                AuraJointDefinition.CreateSlider(anchor, cart, V(0f, 5f, 0f), V(20f, 5f, 0f), AuraVector3.UnitX, AuraVector3.UnitX, true, -1f, 1f));
            Check(joint.IsValid, "slider with a violated limit must be created.");

            var maxSpeed = PackageI_StepTracking(world, 360, Dt, cart);
            Check(maxSpeed < 20f, $"slider limit recovery speed {maxSpeed} m/s is not bounded.");
            Near(StateOf(world, cart).Pose.Position.X, 1f, 0.1f, "cart settles at the upper limit");
            Near(StateOf(world, cart).Pose.Position.Y, 5f, 0.1f, "cart stays on the axis");

            /* Enabling limits later eases from the current pose as well. */
            Ok(world.JointControl.SetLimits(joint, true, -0.5f, 0f), "SetLimits");
            maxSpeed = PackageI_StepTracking(world, 240, Dt, cart);
            Check(maxSpeed < 20f, $"slider limit change speed {maxSpeed} m/s is not bounded.");
            Near(StateOf(world, cart).Pose.Position.X, 0f, 0.1f, "cart follows the tightened limit");
        }

        /* Limits are documented relative to the creation pose: a hinge whose bodies are created rotated against each other
           must not start with the whole creation angle as a limit violation. */
        private static void PackageI_HingeLimitRelativeToCreationPose()
        {
            using var world = NewWorld(AuraPhysicsMode.Plane2D);
            var anchor = PackageI_Static(world, V(0f, 5f, 0f));
            var rotation = new AuraQuaternion(0f, 0f, (float)Math.Sin(1.0), (float)Math.Cos(1.0)); /* 2 rad about Z */
            var bob = world.AttachBody(world.CreateEntity(), new AuraPhysicsBodyDefinition(
                AuraBodyType.Dynamic, new AuraPose(V(0f, 5f, 0f), rotation), AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All,
                new[] { AuraPhysicsShapeDefinition.Box(V(0.5f, 0.5f, 0.5f)) }, mass: 1f, gravityScale: 0f));
            var joint = world.CreateJoint(FindEntity(world, anchor), FindEntity(world, bob),
                new AuraJointDefinition(AuraJointType.Hinge, anchor, bob, V(0f, 5f, 0f), V(0f, 5f, 0f), axisA: AuraVector3.UnitZ, axisB: AuraVector3.UnitZ,
                    enableLimit: true, minLimit: -0.3f, maxLimit: 0.3f));
            Check(joint.IsValid, "hinge creation failed.");
            PackageI_StepTracking(world, 60, Dt, bob);
            Ok(world.JointControl.GetFeedback(joint, out var feedback), "GetFeedback");
            Near(feedback.Position, 0f, 0.05f, "hinge angle is measured from the creation pose");
            Near(StateOf(world, bob).AngularVelocity.Z, 0f, 0.1f, "hinge at rest, no snap to the limit");
        }

        /* Two spheres start 20 m apart on a 1 m distance joint / 1 m rope. The length target is eased towards the rest
           length instead of asking Box2D for a >1000 m/s correction. */
        private static void PackageI_FarStartIsEased(AuraJointType type)
        {
            using var world = NewWorld(AuraPhysicsMode.Plane2D);
            var a = Dyn(world, V(0f, 5f, 0f), AuraPhysicsShapeDefinition.Sphere(0.5f), gravityScale: 0f);
            var b = Dyn(world, V(20f, 5f, 0f), AuraPhysicsShapeDefinition.Sphere(0.5f), gravityScale: 0f);
            var joint = world.CreateJoint(FindEntity(world, a), FindEntity(world, b),
                new AuraJointDefinition(type, a, b, V(0f, 5f, 0f), V(20f, 5f, 0f), distance: 1f));
            Check(joint.IsValid, $"{type} with a violated length must be created.");

            var maxSpeed = PackageI_StepTracking(world, 600, Dt, a, b);
            Check(maxSpeed < 30f, $"{type} recovery speed {maxSpeed} m/s is not bounded.");
            var dx = StateOf(world, b).Pose.Position.X - StateOf(world, a).Pose.Position.X;
            Near(dx, 1f, 0.15f, $"{type} settles at its length");
        }

        /* Box2D explodes a spring faster than the step can resolve (random springs at 4-8 Hz and dt 0.25 reach 60 m/s
           and more on their own, the soak saw 1e7). The frequency is capped to 0.5 / dt after the first step. */
        private static void PackageI_SpringLargeStepStaysFinite()
        {
            var seed = 12345u;
            float Next(float low, float high)
            {
                seed = seed * 1664525u + 1013904223u;
                return low + (high - low) * ((seed >> 8) / 16777216f);
            }

            var worst = 0f;
            for (var trial = 0; trial < 400; trial++)
            {
                using var world = NewWorld(AuraPhysicsMode.Plane2D);
                var posA = V(Next(-1f, 1f), Next(0f, 3f), 0f);
                var posB = V(Next(-1f, 1f), Next(0f, 3f), 0f);
                var a = Dyn(world, posA, AuraPhysicsShapeDefinition.Sphere(0.5f), gravityScale: 0f);
                var b = Dyn(world, posB, AuraPhysicsShapeDefinition.Sphere(0.5f), gravityScale: 0f);
                var anchorA = V(posA.X + Next(-0.5f, 0.5f), posA.Y + Next(-0.5f, 0.5f), 0f);
                var anchorB = V(posB.X + Next(-0.5f, 0.5f), posB.Y + Next(-0.5f, 0.5f), 0f);
                var joint = world.CreateJoint(FindEntity(world, a), FindEntity(world, b),
                    new AuraJointDefinition(AuraJointType.Spring, a, b, anchorA, anchorB,
                        distance: Next(0.2f, 3f), springFrequency: Next(4f, 8f), springDamping: Next(0.05f, 1f)));
                Check(joint.IsValid, "spring creation failed.");
                var trialSpeed = PackageI_StepTracking(world, 400, 0.25f, a, b);
                worst = Math.Max(worst, trialSpeed);
            }

            Console.WriteLine($"  i_spring_worst_speed {worst:F1} m/s");
            Check(worst < 20f, $"spring at dt 0.25 reached {worst} m/s.");
        }

        private static void PackageI_ImpossibleDefinitionsRejected()
        {
            using var world = NewWorld(AuraPhysicsMode.Plane2D);
            var anchor = PackageI_Static(world, V(0f, 5f, 0f));
            var body = Dyn(world, V(1f, 5f, 0f), AuraPhysicsShapeDefinition.Sphere(0.5f), gravityScale: 0f);
            var ea = FindEntity(world, anchor);
            var eb = FindEntity(world, body);
            var p = V(0.5f, 5f, 0f);

            Check(!world.CreateJoint(ea, eb, AuraJointDefinition.CreateSlider(anchor, body, p, p, AuraVector3.UnitX, AuraVector3.UnitX, true, 1f, -1f)).IsValid,
                "slider with min > max must be rejected.");
            Check(!world.CreateJoint(ea, eb, AuraJointDefinition.CreateSlider(anchor, body, p, p, AuraVector3.UnitZ, AuraVector3.UnitZ)).IsValid,
                "slider axis perpendicular to the plane must be rejected.");
            Check(!world.CreateJoint(ea, eb, new AuraJointDefinition(AuraJointType.Hinge, anchor, body, p, p, axisA: AuraVector3.UnitZ, axisB: AuraVector3.UnitZ,
                enableLimit: true, minLimit: -4f, maxLimit: 0f)).IsValid, "hinge limit beyond -pi must be rejected.");
            Check(!world.CreateJoint(ea, eb, AuraJointDefinition.CreateDistance(anchor, body, p, p, -1f)).IsValid,
                "negative distance must be rejected.");
            Check(!world.CreateJoint(ea, eb, new AuraJointDefinition(AuraJointType.Spring, anchor, body, p, p, distance: 1f, springFrequency: -1f)).IsValid,
                "negative spring frequency must be rejected.");
            Check(!world.CreateJoint(ea, eb, new AuraJointDefinition(AuraJointType.Rope, anchor, body, p, p, distance: 0f)).IsValid,
                "zero length rope must be rejected.");
            Check(!world.CreateJoint(ea, eb, new AuraJointDefinition(AuraJointType.Rope, anchor, body, p, p, distance: 1f, enableLimit: true, minLimit: 2f)).IsValid,
                "rope with min > max must be rejected.");
            Check(!world.CreateJoint(ea, eb, new AuraJointDefinition(AuraJointType.Distance, anchor, body, p, p, distance: float.NaN)).IsValid,
                "NaN distance must be rejected.");

            /* A zero rest length is clamped to Box2D's linear slop instead of being rejected, and stays finite. */
            var zero = world.CreateJoint(ea, eb, AuraJointDefinition.CreateDistance(anchor, body, p, p, 0f));
            Check(zero.IsValid, "zero rest length distance joint is valid (clamped to the linear slop).");
            Check(PackageI_StepTracking(world, 120, Dt, body) < 30f, "zero length distance joint exploded.");
        }

        /* Anchors far from a light body make Box2D's solver diverge (condition number r^2 m / I): rods tolerate a lever of
           about 4 body radii, a slider or wheel about 16 (r^2 m / I <= 500). */
        private static void PackageI_IllConditionedAnchorsRejected()
        {
            using var world = NewWorld(AuraPhysicsMode.Plane2D);
            var anchor = PackageI_Static(world, V(0f, 5f, 0f));
            var ball = Dyn(world, V(0f, 10f, 0f), AuraPhysicsShapeDefinition.Sphere(0.5f), gravityScale: 0f);
            var ea = FindEntity(world, anchor);
            var eb = FindEntity(world, ball);

            Check(!world.CreateJoint(ea, eb, AuraJointDefinition.CreateDistance(anchor, ball, V(0f, 5f, 0f), V(0f, 7.5f, 0f), 2f)).IsValid,
                "distance anchor 2.5 m from a 0.5 m ball must be rejected.");
            Check(!world.CreateJoint(ea, eb, new AuraJointDefinition(AuraJointType.Rope, anchor, ball, V(0f, 5f, 0f), V(0f, 7.5f, 0f), distance: 2f)).IsValid,
                "rope anchor 2.5 m from a 0.5 m ball must be rejected.");
            Check(world.CreateJoint(ea, eb, AuraJointDefinition.CreateDistance(anchor, ball, V(0f, 5f, 0f), V(0f, 9.6f, 0f), 4f)).IsValid,
                "distance anchor on the ball surface must be accepted.");
            Check(world.CreateJoint(ea, eb, AuraJointDefinition.CreateHinge(anchor, ball, V(0f, 5f, 0f), V(0f, 5f, 0f), AuraVector3.UnitZ, AuraVector3.UnitZ)).IsValid,
                "a pendulum hinge anchor far from the ball is stable and stays valid.");

            var slideBody = Dyn(world, V(30f, 5f, 0f), AuraPhysicsShapeDefinition.Sphere(0.5f), gravityScale: 0f);
            var es = FindEntity(world, slideBody);
            Check(!world.CreateJoint(ea, es, AuraJointDefinition.CreateSlider(anchor, slideBody, V(0f, 5f, 0f), V(0f, 5f, 0f), AuraVector3.UnitX, AuraVector3.UnitX, true, -1f, 1f)).IsValid,
                "slider with a limit and an anchor 30 m from the body must be rejected.");
            Check(!world.CreateJoint(ea, es, AuraJointDefinition.CreateSlider(anchor, slideBody, V(0f, 5f, 0f), V(0f, 5f, 0f), AuraVector3.UnitX, AuraVector3.UnitX)).IsValid,
                "the same slider without limits is ill conditioned too (spring and motor rows can be enabled later).");
            Check(world.CreateJoint(ea, es, AuraJointDefinition.CreateSlider(anchor, slideBody, V(30f, 5f, 0f), V(30f, 5f, 0f), AuraVector3.UnitX, AuraVector3.UnitX, true, -1f, 1f)).IsValid,
                "a slider anchored on the body is accepted.");
        }

        /* Documented contract: a broken joint handle stays valid until DestroyJoint. */
        private static void PackageI_BrokenJointHandleStaysValid()
        {
            using var world = NewWorld(AuraPhysicsMode.Plane2D);
            var anchor = PackageI_Static(world, V(0f, 6f, 0f));
            var weight = Dyn(world, V(0f, 5f, 0f), AuraPhysicsShapeDefinition.Box(V(0.5f, 0.5f, 0.5f)), mass: 10f);
            var joint = world.CreateJoint(FindEntity(world, anchor), FindEntity(world, weight),
                AuraJointDefinition.CreateSlider(anchor, weight, V(0f, 5.5f, 0f), V(0f, 5.5f, 0f), AuraVector3.UnitX, AuraVector3.UnitX));
            Check(joint.IsValid, "slider creation failed.");
            Ok(world.JointControl.SetBreakThreshold(joint, 1f, 0f), "SetBreakThreshold");
            Step(world, 60);

            Ok(world.JointControl.IsBroken(joint, out var broken), "IsBroken");
            Check(broken, "slider carrying a weight did not break above a 1 N threshold.");
            Check(world.HasJoint(joint), "HasJoint must stay true for a broken joint.");
            Ok(world.JointControl.GetFeedback(joint, out var feedback), "GetFeedback on a broken joint");
            Check(feedback.IsBroken && feedback.Force > 1f, "broken feedback lost the breaking load.");

            Expect(world.JointControl.SetLimits(joint, true, -1f, 1f), AuraResult.UnsupportedOperation, "limits on a broken joint");
            Expect(world.JointControl.SetMotor(joint, AuraJointMotorDefinition.Velocity(1f, 10f)), AuraResult.UnsupportedOperation, "motor on a broken joint");

            Ok(world.DestroyJoint(joint), "DestroyJoint frees the broken handle");
            Check(!world.HasJoint(joint), "HasJoint must be false after DestroyJoint.");
            Expect(world.JointControl.IsBroken(joint, out _), AuraResult.InvalidHandle, "IsBroken after destroy");
            Expect(world.DestroyJoint(joint), AuraResult.InvalidHandle, "double destroy");

            var replacement = world.CreateJoint(FindEntity(world, anchor), FindEntity(world, weight),
                AuraJointDefinition.CreateSlider(anchor, weight, V(0f, 5.5f, 0f), V(0f, 5.5f, 0f), AuraVector3.UnitX, AuraVector3.UnitX));
            Check(replacement.IsValid && world.HasJoint(replacement) && !world.HasJoint(joint), "a recycled slot must not revive the stale handle.");
            Step(world, 30, 100);
        }
    }
}
