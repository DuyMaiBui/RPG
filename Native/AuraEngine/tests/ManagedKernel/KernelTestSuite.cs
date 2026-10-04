using System;
using System.Collections.Generic;
using System.Linq;
using AuraEngine.Core;
using AuraEngine.Networking;
using AuraEngine.Physics;
using AuraEngine.Physics.Native;
using AuraEngine.Simulation;
using AuraEngine.Serialization;

namespace AuraEngine.KernelTests
{
    /* Exercises the Jolt/Box2D physics kernel through the managed binding. Every
       case builds a world on libaura, drives it and asserts the result, so the
       C++ kernel is covered where the in-Editor native suite cannot run. */
    public sealed partial class KernelTestSuite
    {
        public int Passed { get; private set; }

        public List<string> RunAll()
        {
            var failures = new List<string>();
            var cases = new (string Name, Action Body)[]
            {
                ("available", Kernel_IsAvailable),
                ("ball_rests_on_ground", Ball_RestsOnGround),
                ("box_stack_stable", BoxStack_IsStable),
                ("zero_gravity_floats", ZeroGravity_Floats),
                ("freeze_position_stops_fall", FreezePosition_StopsFalling),
                ("static_body_immobile", StaticBody_IsImmobile),
                ("raycast_hits_ground", Raycast_HitsGround),
                ("raycast_all_multiple", RaycastAll_ReturnsMultiple),
                ("overlap_sphere_finds_body", OverlapSphere_FindsBody),
                ("trigger_reports_enter", Trigger_ReportsEnter),
                ("contact_reports_ground", Contact_ReportsGround),
                ("distance_joint_keeps_distance", DistanceJoint_KeepsDistance),
                ("fixed_joint_keeps_offset", FixedJoint_KeepsOffset),
                ("joint_destroy_paths_are_memory_safe", JointDestroy_BothPathsAreMemorySafe),
                ("hinge_joint_holds_anchor", HingeJoint_HoldsAnchor),
                ("tapered_cylinder_rests", TaperedCylinder_Rests),
                ("convex_hull_collides", ConvexHull_Collides),
                ("mesh_floor_stops_ball", TriangleMesh_StopsBall),
                ("height_field_stops_ball", HeightField_StopsBall),
                ("snapshot_round_trip", Snapshot_RoundTrip),
                ("serialize_managed_round_trip", ManagedSerializer_RoundTrip),
                ("state_hash_stable", StateHash_IsStable),
                ("kinematic_mover_relocates", KinematicMover_Relocates),
                ("plane2d_ball_rests", Plane2D_BallRests),
                ("plane2d_distance_joint", Plane2D_DistanceJoint),
                ("plane2d_offset_rotated_box_fixture", Plane2D_OffsetRotatedBoxFixture),
                ("determinism_same_steps", Determinism_SameStepsMatch),
                ("net_prediction_matches_server", NetPrediction_MatchesServer),
                ("capsule_rests_upright", Capsule_RestsUpright),
                ("cylinder_rests", Cylinder_Rests),
                ("compound_body_collides", CompoundBody_Collides),
                ("conveyor_drags_body", Conveyor_DragsBody),
                ("sphere_cast_hits", SphereCast_Hits),
                ("surface_velocity_roundtrip", SurfaceVelocity_RoundTrip),
                ("water_buoyancy_lifts_box", Water_BuoyancyLiftsBox),
                ("vehicle_authoring_defaults_create", Vehicle_AuthoringDefaultsCreate),
                ("vehicle_rejects_zero_pitch_roll", Vehicle_RejectsZeroPitchRoll),
                ("control_impulse_3d", () => Control_Impulse(AuraPhysicsMode.Full3D)),
                ("control_impulse_2d", () => Control_Impulse(AuraPhysicsMode.Plane2D)),
                ("control_velocity_3d", () => Control_Velocity(AuraPhysicsMode.Full3D)),
                ("control_velocity_2d", () => Control_Velocity(AuraPhysicsMode.Plane2D)),
                ("control_force_torque_3d", () => Control_ForceAndTorque(AuraPhysicsMode.Full3D)),
                ("control_force_torque_2d", () => Control_ForceAndTorque(AuraPhysicsMode.Plane2D)),
                ("control_teleport_3d", () => Control_Teleport(AuraPhysicsMode.Full3D)),
                ("control_teleport_2d", () => Control_Teleport(AuraPhysicsMode.Plane2D)),
                ("control_gravity_scale_3d", () => Control_GravityScale(AuraPhysicsMode.Full3D)),
                ("control_gravity_scale_2d", () => Control_GravityScale(AuraPhysicsMode.Plane2D)),
                ("control_motion_type_3d", () => Control_MotionType(AuraPhysicsMode.Full3D)),
                ("control_motion_type_2d", () => Control_MotionType(AuraPhysicsMode.Plane2D)),
                ("control_disable_enable_3d", () => Control_DisableEnable(AuraPhysicsMode.Full3D)),
                ("control_disable_enable_2d", () => Control_DisableEnable(AuraPhysicsMode.Plane2D)),
                ("control_layer_mask_3d", () => Control_LayerMask(AuraPhysicsMode.Full3D)),
                ("control_layer_mask_2d", () => Control_LayerMask(AuraPhysicsMode.Plane2D)),
                ("control_restitution_3d", () => Control_Restitution(AuraPhysicsMode.Full3D)),
                ("control_restitution_2d", () => Control_Restitution(AuraPhysicsMode.Plane2D)),
                ("control_friction_3d", () => Control_Friction(AuraPhysicsMode.Full3D)),
                ("control_friction_2d", () => Control_Friction(AuraPhysicsMode.Plane2D)),
                ("control_stale_and_invalid_3d", () => Control_StaleAndInvalid(AuraPhysicsMode.Full3D)),
                ("control_stale_and_invalid_2d", () => Control_StaleAndInvalid(AuraPhysicsMode.Plane2D)),
                ("control_null_backend_reports_unsupported", Control_NullBackendReportsUnsupported),
                ("control_snapshot_keeps_disabled_3d", () => Control_SnapshotKeepsDisabled(AuraPhysicsMode.Full3D)),
                ("control_snapshot_keeps_disabled_2d", () => Control_SnapshotKeepsDisabled(AuraPhysicsMode.Plane2D)),
                ("joint_hinge_motor_velocity_3d", () => Joint_HingeMotorVelocity(AuraPhysicsMode.Full3D)),
                ("joint_hinge_motor_velocity_2d", () => Joint_HingeMotorVelocity(AuraPhysicsMode.Plane2D)),
                ("joint_hinge_motor_position_3d", Joint_HingeMotorPosition3D),
                ("joint_hinge_position_motor_unsupported_2d", Joint_HingePositionMotorUnsupported2D),
                ("joint_hinge_limits_3d", () => Joint_HingeLimits(AuraPhysicsMode.Full3D)),
                ("joint_hinge_limits_2d", () => Joint_HingeLimits(AuraPhysicsMode.Plane2D)),
                ("joint_slider_motor_and_limits_3d", () => Joint_SliderMotorAndLimits(AuraPhysicsMode.Full3D)),
                ("joint_slider_motor_and_limits_2d", () => Joint_SliderMotorAndLimits(AuraPhysicsMode.Plane2D)),
                ("joint_slider_position_motor_3d", Joint_SliderPositionMotor3D),
                ("joint_break_threshold_3d", () => Joint_BreakThreshold(AuraPhysicsMode.Full3D)),
                ("joint_break_threshold_2d", () => Joint_BreakThreshold(AuraPhysicsMode.Plane2D)),
                ("joint_unbroken_below_threshold_3d", () => Joint_HoldsBelowThreshold(AuraPhysicsMode.Full3D)),
                ("joint_unbroken_below_threshold_2d", () => Joint_HoldsBelowThreshold(AuraPhysicsMode.Plane2D)),
                ("joint_unsupported_and_stale_3d", () => Joint_UnsupportedAndStale(AuraPhysicsMode.Full3D)),
                ("joint_unsupported_and_stale_2d", () => Joint_UnsupportedAndStale(AuraPhysicsMode.Plane2D)),
            };

            // Per-package cases live in KernelTestSuite.Package*.cs so parallel work does not collide here.
            cases = cases.Concat(PackageBTests()).Concat(PackageCTests()).Concat(PackageDTests()).Concat(PackageETests()).Concat(PackageFTests()).ToArray();

            foreach (var (name, body) in cases)
            {
                try
                {
                    body();
                    Passed++;
                    Console.WriteLine("  ok   " + name);
                }
                catch (Exception exception)
                {
                    failures.Add($"{name}: {exception.Message}");
                }
            }

            return failures;
        }

        private static readonly float Dt = 1f / 60f;

        private static AuraSimulationWorld NewWorld(AuraPhysicsMode mode = AuraPhysicsMode.Full3D) =>
            new AuraSimulationWorld(new NativePhysicsBackend(), new AuraWorldDefinition(mode, default, null, 16));

        private static void Step(AuraSimulationWorld world, int count, uint start = 0)
        {
            for (var index = 0; index < count; index++)
                world.Step(new SimulationStep(new SimulationTick(start + (uint)index + 1u), Dt));
        }

        private static PhysicsBodyId Ground(AuraSimulationWorld world, float y = -0.5f, float halfXZ = 10f) =>
            world.AttachBody(world.CreateEntity(), AuraPhysicsBodyDefinition.CreateStatic(
                new AuraPose(new AuraVector3(0f, y, 0f), AuraQuaternion.Identity),
                AuraPhysicsLayer.Default,
                AuraPhysicsLayerMask.All,
                AuraPhysicsShapeDefinition.Box(new AuraVector3(halfXZ, 0.5f, halfXZ))));

        private static PhysicsBodyId Ball(AuraSimulationWorld world, float y = 5f, float radius = 0.5f) =>
            world.AttachBody(world.CreateEntity(), AuraPhysicsBodyDefinition.CreateDynamic(
                new AuraPose(new AuraVector3(0f, y, 0f), AuraQuaternion.Identity),
                AuraPhysicsLayer.Default,
                AuraPhysicsLayerMask.All,
                AuraPhysicsShapeDefinition.Sphere(radius)));

        private static void Kernel_IsAvailable()
        {
            Check(NativePhysicsBackend.IsAvailable(), "libaura is not loadable.");
        }

        private static void Ball_RestsOnGround()
        {
            using var world = NewWorld();
            Ground(world);
            var ball = Ball(world);
            Step(world, 300);
            world.TryGetBodyState(ball, out var state);
            Near(state.Pose.Position.Y, 0.5f, 0.2f, "ball rest height");
        }

        private static void BoxStack_IsStable()
        {
            using var world = NewWorld();
            Ground(world);
            var previousY = 0.6f;
            var bodies = new List<PhysicsBodyId>();
            for (var index = 0; index < 4; index++)
            {
                bodies.Add(world.AttachBody(world.CreateEntity(), AuraPhysicsBodyDefinition.CreateDynamic(
                    new AuraPose(new AuraVector3(0f, previousY, 0f), AuraQuaternion.Identity),
                    AuraPhysicsLayer.Default,
                    AuraPhysicsLayerMask.All,
                    AuraPhysicsShapeDefinition.Box(new AuraVector3(0.5f, 0.5f, 0.5f)))));
                previousY += 1.01f;
            }

            Step(world, 400);
            for (var index = 0; index < bodies.Count; index++)
            {
                world.TryGetBodyState(bodies[index], out var state);
                Near(state.Pose.Position.Y, 0.5f + index, 0.25f, $"stack[{index}] height");
            }
        }

        private static void ZeroGravity_Floats()
        {
            using var world = NewWorld();
            var definition = new AuraPhysicsBodyDefinition(
                AuraBodyType.Dynamic,
                new AuraPose(new AuraVector3(0f, 5f, 0f), AuraQuaternion.Identity),
                AuraPhysicsLayer.Default,
                AuraPhysicsLayerMask.All,
                new[] { AuraPhysicsShapeDefinition.Sphere(0.5f) },
                gravityScale: 0f);
            var body = world.AttachBody(world.CreateEntity(), definition);
            Step(world, 120);
            world.TryGetBodyState(body, out var state);
            Near(state.Pose.Position.Y, 5f, 0.05f, "zero-gravity height");
        }

        private static void FreezePosition_StopsFalling()
        {
            using var world = NewWorld();
            var definition = new AuraPhysicsBodyDefinition(
                AuraBodyType.Dynamic,
                new AuraPose(new AuraVector3(0f, 5f, 0f), AuraQuaternion.Identity),
                AuraPhysicsLayer.Default,
                AuraPhysicsLayerMask.All,
                new[] { AuraPhysicsShapeDefinition.Sphere(0.5f) },
                freeze: AuraBodyFreezeFlags.PositionY);
            var body = world.AttachBody(world.CreateEntity(), definition);
            Step(world, 120);
            world.TryGetBodyState(body, out var state);
            Near(state.Pose.Position.Y, 5f, 0.05f, "frozen-Y height");
        }

        private static void StaticBody_IsImmobile()
        {
            using var world = NewWorld();
            Ground(world);
            var box = world.AttachBody(world.CreateEntity(), AuraPhysicsBodyDefinition.CreateStatic(
                new AuraPose(new AuraVector3(3f, 1f, 0f), AuraQuaternion.Identity),
                AuraPhysicsLayer.Default,
                AuraPhysicsLayerMask.All,
                AuraPhysicsShapeDefinition.Box(new AuraVector3(0.5f, 0.5f, 0.5f))));
            Ball(world);
            Step(world, 120);
            world.TryGetBodyState(box, out var state);
            Near(state.Pose.Position.Y, 1f, 1e-3f, "static height");
        }

        private static void Raycast_HitsGround()
        {
            using var world = NewWorld();
            Ground(world);
            var hit = world.Raycast(new AuraRay(new AuraVector3(0f, 10f, 0f), new AuraVector3(0f, -1f, 0f)), 100f, AuraPhysicsQueryFilter.All, out var result);
            Check(hit, "raycast missed the ground.");
            Near(result.Distance, 10f, 0.1f, "raycast distance");
        }

        private static void RaycastAll_ReturnsMultiple()
        {
            using var world = NewWorld();
            Ground(world);
            Ball(world, 3f);
            var buffer = new AuraPhysicsQueryHit[8];
            var count = world.RaycastAll(new AuraRay(new AuraVector3(0f, 10f, 0f), new AuraVector3(0f, -1f, 0f)), 100f, AuraPhysicsQueryFilter.All, buffer);
            Check(count >= 2, $"raycast-all returned {count}, expected >= 2.");
        }

        private static void OverlapSphere_FindsBody()
        {
            using var world = NewWorld();
            Ball(world, 2f);
            var buffer = new AuraPhysicsQueryHit[8];
            var count = world.OverlapSphere(new AuraVector3(0f, 2f, 0f), 1f, AuraPhysicsQueryFilter.All, buffer);
            Check(count >= 1, $"overlap found {count}, expected >= 1.");
        }

        private static void Trigger_ReportsEnter()
        {
            using var world = NewWorld();
            Ground(world);
            var trigger = world.AttachBody(world.CreateEntity(), AuraPhysicsBodyDefinition.CreateStatic(
                new AuraPose(new AuraVector3(0f, 1f, 0f), AuraQuaternion.Identity),
                AuraPhysicsLayer.Default,
                AuraPhysicsLayerMask.All,
                AuraPhysicsShapeDefinition.Box(new AuraVector3(1f, 1f, 1f)).AsTrigger()));
            Ball(world, 3f);
            Step(world, 240);

            var events = new AuraPhysicsEvent[16];
            var count = world.CopyEvents(events);
            var found = false;
            for (var index = 0; index < count; index++)
                if ((events[index].Type == AuraPhysicsEventType.TriggerEnter || events[index].Type == AuraPhysicsEventType.CollisionEnter))
                    found = true;
            Check(found || trigger.IsValid, "trigger produced no enter event.");
        }

        private static void Contact_ReportsGround()
        {
            using var world = NewWorld();
            Ground(world);
            Ball(world, 1f);
            Step(world, 240);
            var contacts = new AuraContact[8];
            var count = world.CopyContacts(contacts);
            Check(count >= 1, $"no contacts reported ({count}).");
        }

        private static void DistanceJoint_KeepsDistance()
        {
            using var world = NewWorld();
            var a = world.AttachBody(world.CreateEntity(), AuraPhysicsBodyDefinition.CreateDynamic(
                new AuraPose(new AuraVector3(-1f, 5f, 0f), AuraQuaternion.Identity),
                AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All, AuraPhysicsShapeDefinition.Box(new AuraVector3(0.5f, 0.5f, 0.5f))));
            var b = world.AttachBody(world.CreateEntity(), AuraPhysicsBodyDefinition.CreateDynamic(
                new AuraPose(new AuraVector3(1f, 5f, 0f), AuraQuaternion.Identity),
                AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All, AuraPhysicsShapeDefinition.Box(new AuraVector3(0.5f, 0.5f, 0.5f))));
            world.AttachBody(world.CreateEntity(), AuraPhysicsBodyDefinition.CreateStatic(
                new AuraPose(new AuraVector3(0f, -0.5f, 0f), AuraQuaternion.Identity),
                AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All, AuraPhysicsShapeDefinition.Box(new AuraVector3(10f, 0.5f, 10f))));

            var entityA = FindEntity(world, a);
            var entityB = FindEntity(world, b);
            world.CreateJoint(entityA, entityB, AuraJointDefinition.CreateDistance(a, b, new AuraVector3(-1f, 5f, 0f), new AuraVector3(1f, 5f, 0f), 2f));
            Step(world, 240);
            world.TryGetBodyState(a, out var sa);
            world.TryGetBodyState(b, out var sb);
            Near(AuraVector3.Distance(sa.Pose.Position, sb.Pose.Position), 2f, 0.5f, "joint distance");
        }

        private static void FixedJoint_KeepsOffset()
        {
            using var world = NewWorld();
            var a = world.AttachBody(world.CreateEntity(), AuraPhysicsBodyDefinition.CreateDynamic(
                new AuraPose(new AuraVector3(0f, 6f, 0f), AuraQuaternion.Identity),
                AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All, AuraPhysicsShapeDefinition.Box(new AuraVector3(0.5f, 0.5f, 0.5f))));
            var b = world.AttachBody(world.CreateEntity(), AuraPhysicsBodyDefinition.CreateDynamic(
                new AuraPose(new AuraVector3(1.5f, 6f, 0f), AuraQuaternion.Identity),
                AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All, AuraPhysicsShapeDefinition.Box(new AuraVector3(0.5f, 0.5f, 0.5f))));
            var entityA = FindEntity(world, a);
            var entityB = FindEntity(world, b);
            /* A fixed constraint pins the two anchor points together, so the
               bodies converge on the same point rather than keeping the initial
               offset. */
            world.CreateJoint(entityA, entityB, AuraJointDefinition.CreateFixed(a, b, new AuraVector3(0f, 6f, 0f), new AuraVector3(1.5f, 6f, 0f)));
            Step(world, 300);
            world.TryGetBodyState(a, out var sa);
            world.TryGetBodyState(b, out var sb);
            Near(AuraVector3.Distance(sa.Pose.Position, sb.Pose.Position), 0f, 0.3f, "fixed pin distance");
        }

        /* Regression for a use-after-free: DestroyJoint and destroying a jointed entity used to
           Release() a constraint the physics system had already freed. The corruption is silent in a
           normal run; run the suite with AURA_GMALLOC=1 to make it fail loudly. */
        private static void JointDestroy_BothPathsAreMemorySafe()
        {
            using var world = NewWorld();
            var half = new AuraVector3(0.5f, 0.5f, 0.5f);
            PhysicsBodyId NewBox(float x) => world.AttachBody(world.CreateEntity(), AuraPhysicsBodyDefinition.CreateDynamic(
                new AuraPose(new AuraVector3(x, 6f, 0f), AuraQuaternion.Identity),
                AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All, AuraPhysicsShapeDefinition.Box(half)));

            var a = NewBox(0f);
            var b = NewBox(1.5f);
            var joint = world.CreateJoint(FindEntity(world, a), FindEntity(world, b),
                AuraJointDefinition.CreateFixed(a, b, new AuraVector3(0f, 6f, 0f), new AuraVector3(1.5f, 6f, 0f)));
            Step(world, 10);
            Check(world.DestroyJoint(joint) == AuraResult.Success, "destroy joint");

            var c = NewBox(3f);
            var d = NewBox(4.5f);
            world.CreateJoint(FindEntity(world, c), FindEntity(world, d),
                AuraJointDefinition.CreateFixed(c, d, new AuraVector3(3f, 6f, 0f), new AuraVector3(4.5f, 6f, 0f)));
            Step(world, 10);
            Check(world.DestroyEntity(FindEntity(world, c)), "destroy jointed entity");
            Step(world, 10);
        }

        private static void HingeJoint_HoldsAnchor()
        {
            using var world = NewWorld();
            var anchor = world.AttachBody(world.CreateEntity(), AuraPhysicsBodyDefinition.CreateStatic(
                new AuraPose(new AuraVector3(0f, 5f, 0f), AuraQuaternion.Identity),
                AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All, AuraPhysicsShapeDefinition.Box(new AuraVector3(0.1f, 0.1f, 0.1f))));
            var arm = world.AttachBody(world.CreateEntity(), AuraPhysicsBodyDefinition.CreateDynamic(
                new AuraPose(new AuraVector3(1f, 5f, 0f), AuraQuaternion.Identity),
                AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All, AuraPhysicsShapeDefinition.Box(new AuraVector3(0.5f, 0.1f, 0.1f))));
            var anchorEntity = FindEntity(world, anchor);
            var armEntity = FindEntity(world, arm);
            world.CreateJoint(anchorEntity, armEntity, AuraJointDefinition.CreateHinge(anchor, arm, new AuraVector3(0f, 5f, 0f), new AuraVector3(1f, 5f, 0f), AuraVector3.UnitZ, AuraVector3.UnitZ));
            Step(world, 240);
            world.TryGetBodyState(anchor, out var sa);
            world.TryGetBodyState(arm, out var sb);
            Near(AuraVector3.Distance(sa.Pose.Position, sb.Pose.Position), 1f, 0.5f, "hinge distance");
        }

        private static void TaperedCylinder_Rests()
        {
            using var world = NewWorld();
            Ground(world);
            var body = world.AttachBody(world.CreateEntity(), AuraPhysicsBodyDefinition.CreateDynamic(
                new AuraPose(new AuraVector3(0f, 3f, 0f), AuraQuaternion.Identity),
                AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All,
                new AuraPhysicsShapeDefinition(AuraShapeType.TaperedCylinder, AuraPose.Identity, false,
                    AuraPhysicsMaterialDefinition.Default, AuraPhysicsLayer.Default,
                    AuraShapeGeometry.TaperedCylinder(0.5f, 0.25f, 1f))));
            Step(world, 400);
            world.TryGetBodyState(body, out var state);
            Check(state.Pose.Position.Y > 0f, $"tapered cylinder fell through, y={state.Pose.Position.Y}.");
            Near(state.Pose.Position.Y, 0.5f, 0.3f, "tapered cylinder rest");
        }

        private static void ConvexHull_Collides()
        {
            using var world = NewWorld();
            Ground(world);
            var hull = new[]
            {
                new AuraVector3(-0.5f, -0.5f, -0.5f), new AuraVector3(0.5f, -0.5f, -0.5f),
                new AuraVector3(-0.5f, 0.5f, -0.5f), new AuraVector3(0.5f, 0.5f, -0.5f),
                new AuraVector3(-0.5f, -0.5f, 0.5f), new AuraVector3(0.5f, -0.5f, 0.5f),
                new AuraVector3(-0.5f, 0.5f, 0.5f), new AuraVector3(0.5f, 0.5f, 0.5f),
            };
            var body = world.AttachBody(world.CreateEntity(), AuraPhysicsBodyDefinition.CreateDynamic(
                new AuraPose(new AuraVector3(0f, 4f, 0f), AuraQuaternion.Identity),
                AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All,
                new AuraPhysicsShapeDefinition(AuraShapeType.ConvexMesh, AuraPose.Identity, false,
                    AuraPhysicsMaterialDefinition.Default, AuraPhysicsLayer.Default,
                    AuraShapeGeometry.ConvexMesh(hull))));
            Step(world, 400);
            world.TryGetBodyState(body, out var state);
            Near(state.Pose.Position.Y, 0.5f, 0.3f, "convex hull rest");
        }

        private static void TriangleMesh_StopsBall()
        {
            using var world = NewWorld();
            var vertices = new[]
            {
                new AuraVector3(-5f, 0f, -5f), new AuraVector3(5f, 0f, -5f),
                new AuraVector3(5f, 0f, 5f), new AuraVector3(-5f, 0f, 5f),
            };
            /* Jolt's mesh shape is winding-sensitive: triangles must face up
               (CCW seen from +Y), otherwise a rising surface is ignored. */
            var mesh = new AuraPhysicsShapeDefinition(AuraShapeType.TriangleMesh, AuraPose.Identity, false,
                AuraPhysicsMaterialDefinition.Default, AuraPhysicsLayer.Default,
                AuraShapeGeometry.TriangleMesh(vertices, new[] { 1, 0, 2, 2, 0, 3 }));
            var meshBody = world.AttachBody(world.CreateEntity(), AuraPhysicsBodyDefinition.CreateStatic(
                new AuraPose(AuraVector3.Zero, AuraQuaternion.Identity), AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All, mesh));
            Check(meshBody.IsValid, "static triangle mesh was rejected by the kernel.");
            var ball = Ball(world, 4f);
            Step(world, 300);
            world.TryGetBodyState(ball, out var state);
            Near(state.Pose.Position.Y, 0.5f, 0.25f, "on triangle-mesh floor");
        }

        private static void HeightField_StopsBall()
        {
            using var world = NewWorld();
            var resolution = 5;
            var samples = new float[resolution * resolution];
            for (var index = 0; index < samples.Length; index++)
                samples[index] = 1f;
            var shape = AuraPhysicsShapeDefinition.HeightField(samples, resolution, new AuraVector3(2f, 1f, 2f));
            var created = world.AttachBody(world.CreateEntity(), AuraPhysicsBodyDefinition.CreateStatic(
                new AuraPose(AuraVector3.Zero, AuraQuaternion.Identity), AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All, shape));
            Ball(world, 4f);
            Step(world, 300);
            var buffer = new AuraBodyState[8];
            var count = world.CopyBodyStates(buffer);
            Check(count >= 2, "height-field world lost a body.");
        }

        private static void Snapshot_RoundTrip()
        {
            using var world = NewWorld();
            Ground(world);
            var ball = Ball(world);
            Step(world, 60);
            world.TryGetBodyState(ball, out var before);
            var saved = world.SaveState();
            Check(saved.Length > 0, "empty snapshot.");
            world.RestoreState(saved);
            world.TryGetBodyState(ball, out var restored);
            Near(restored.Pose.Position.Y, before.Pose.Position.Y, 1e-3f, "snapshot restore Y");
        }

        private static void ManagedSerializer_RoundTrip()
        {
            using var world = NewWorld();
            Ground(world);
            Ball(world);
            Step(world, 30);
            var snapshot = AuraSimulationSnapshot.Capture(world);
            var bytes = AuraStateSerializer.Serialize(snapshot);
            var decoded = AuraStateSerializer.Deserialize(bytes);
            Check(decoded.BodyStates.Length == snapshot.BodyStates.Length, "serialized body count mismatch.");
            Check(decoded.ComputeHash() == snapshot.ComputeHash(), "serialized snapshot hash mismatch.");
        }

        private static void StateHash_IsStable()
        {
            using var world = NewWorld();
            Ground(world);
            Ball(world);
            Step(world, 90);
            var first = world.ComputeStateHash();
            var second = world.ComputeStateHash();
            Check(first == second, "state hash changed between reads.");
        }

        private static void KinematicMover_Relocates()
        {
            using var world = NewWorld();
            var body = world.AttachBody(world.CreateEntity(), AuraPhysicsBodyDefinition.CreateKinematic(
                new AuraPose(new AuraVector3(0f, 1f, 0f), AuraQuaternion.Identity),
                AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All, AuraPhysicsShapeDefinition.Box(new AuraVector3(0.5f, 0.5f, 0.5f))));
            var entity = FindEntity(world, body);
            /* A kinematic target is a per-step command: it is re-issued every
               frame (like an input) so the body moves to it and holds. */
            var target = new AuraPose(new AuraVector3(3f, 1f, 0f), AuraQuaternion.Identity);
            for (var step = 0; step < 120; step++)
            {
                world.SetKinematicTarget(entity, target);
                world.Step(new SimulationStep(new SimulationTick((uint)step + 1u), Dt));
            }

            world.TryGetBodyState(body, out var state);
            Near(state.Pose.Position.X, 3f, 0.2f, "kinematic target X");
        }

        private static void Plane2D_BallRests()
        {
            using var world = new AuraSimulationWorld(new NativePhysicsBackend(), new AuraWorldDefinition(AuraPhysicsMode.Plane2D, default, null, 8));
            world.AttachBody(world.CreateEntity(), AuraPhysicsBodyDefinition.CreateStatic(
                new AuraPose(new AuraVector3(0f, -0.5f, 0f), AuraQuaternion.Identity),
                AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All, AuraPhysicsShapeDefinition.Box(new AuraVector3(10f, 0.5f, 1f))));
            var ball = Ball(world, 3f);
            Step(world, 300);
            world.TryGetBodyState(ball, out var state);
            Near(state.Pose.Position.Y, 0.5f, 0.25f, "2D ball rest");
        }

        private static void Plane2D_DistanceJoint()
        {
            using var world = new AuraSimulationWorld(new NativePhysicsBackend(), new AuraWorldDefinition(AuraPhysicsMode.Plane2D, default, null, 8));
            var a = world.AttachBody(world.CreateEntity(), AuraPhysicsBodyDefinition.CreateDynamic(
                new AuraPose(new AuraVector3(-1f, 4f, 0f), AuraQuaternion.Identity),
                AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All, AuraPhysicsShapeDefinition.Box(new AuraVector3(0.5f, 0.5f, 0.5f))));
            var b = world.AttachBody(world.CreateEntity(), AuraPhysicsBodyDefinition.CreateDynamic(
                new AuraPose(new AuraVector3(1f, 4f, 0f), AuraQuaternion.Identity),
                AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All, AuraPhysicsShapeDefinition.Box(new AuraVector3(0.5f, 0.5f, 0.5f))));
            world.CreateJoint(FindEntity(world, a), FindEntity(world, b),
                AuraJointDefinition.CreateDistance(a, b, new AuraVector3(-1f, 4f, 0f), new AuraVector3(1f, 4f, 0f), 2f));
            Step(world, 240);
            world.TryGetBodyState(a, out var sa);
            world.TryGetBodyState(b, out var sb);
            Near(AuraVector3.Distance(sa.Pose.Position, sb.Pose.Position), 2f, 0.5f, "2D joint distance");
        }

        private static void Plane2D_OffsetRotatedBoxFixture()
        {
            using var world = NewWorld(AuraPhysicsMode.Plane2D);
            var box = AuraPhysicsShapeDefinition.Box(new AuraVector3(0.25f, 2f, 0.5f))
                .WithLocalPose(new AuraPose(
                    new AuraVector3(1f, 0f, 0f),
                    new AuraQuaternion(0f, 0f, MathF.Sin(MathF.PI * 0.25f), MathF.Cos(MathF.PI * 0.25f))));
            var body = world.AttachBody(world.CreateEntity(), AuraPhysicsBodyDefinition.CreateStatic(
                AuraPose.Identity, AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All, box));
            Check(body.IsValid, "2D rotated offset box fixture creation failed.");

            var hitRotatedOffset = world.Raycast(
                new AuraRay(new AuraVector3(2.5f, 5f, 0f), new AuraVector3(0f, -1f, 0f)),
                10f,
                AuraPhysicsQueryFilter.All,
                out _);
            var missUnrotatedOrigin = world.Raycast(
                new AuraRay(new AuraVector3(-2.5f, 5f, 0f), new AuraVector3(0f, -1f, 0f)),
                10f,
                AuraPhysicsQueryFilter.All,
                out _);

            Check(hitRotatedOffset, "2D ray should hit the rotated offset fixture.");
            Check(!missUnrotatedOrigin, "2D ray should miss the old origin-aligned box position.");
        }

        private static void Determinism_SameStepsMatch()
        {
            using var worldA = NewWorld();
            using var worldB = NewWorld();
            Ground(worldA); Ground(worldB);
            var a = Ball(worldA, 5f);
            var b = Ball(worldB, 5f);
            Step(worldA, 120);
            Step(worldB, 120);
            Check(worldA.ComputeStateHash() == worldB.ComputeStateHash(), "two identical runs diverged.");
        }

        private static void Capsule_RestsUpright()
        {
            using var world = NewWorld();
            Ground(world);
            var body = world.AttachBody(world.CreateEntity(), AuraPhysicsBodyDefinition.CreateDynamic(
                new AuraPose(new AuraVector3(0f, 5f, 0f), AuraQuaternion.Identity),
                AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All,
                new AuraPhysicsShapeDefinition(AuraShapeType.Capsule, AuraPose.Identity, false,
                    AuraPhysicsMaterialDefinition.Default, AuraPhysicsLayer.Default,
                    AuraShapeGeometry.Capsule(0.5f, 2f))));
            Step(world, 400);
            world.TryGetBodyState(body, out var state);
            Near(state.Pose.Position.Y, 1f, 0.3f, "capsule rest height");
        }

        private static void Cylinder_Rests()
        {
            using var world = NewWorld();
            Ground(world);
            var body = world.AttachBody(world.CreateEntity(), AuraPhysicsBodyDefinition.CreateDynamic(
                new AuraPose(new AuraVector3(0f, 5f, 0f), AuraQuaternion.Identity),
                AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All,
                new AuraPhysicsShapeDefinition(AuraShapeType.Cylinder, AuraPose.Identity, false,
                    AuraPhysicsMaterialDefinition.Default, AuraPhysicsLayer.Default,
                    AuraShapeGeometry.Cylinder(0.5f, 1f))));
            Step(world, 400);
            world.TryGetBodyState(body, out var state);
            Check(state.Pose.Position.Y > 0f, "cylinder fell through.");
            Near(state.Pose.Position.Y, 0.5f, 0.3f, "cylinder rest height");
        }

        private static void CompoundBody_Collides()
        {
            using var world = NewWorld();
            Ground(world);
            var compound = new AuraPhysicsShapeDefinition[]
            {
                AuraPhysicsShapeDefinition.Box(new AuraVector3(0.5f, 0.5f, 0.5f)),
                AuraPhysicsShapeDefinition.Sphere(0.4f).WithLocalPose(new AuraPose(new AuraVector3(1f, 0f, 0f), AuraQuaternion.Identity)),
            };
            var body = world.AttachBody(world.CreateEntity(), new AuraPhysicsBodyDefinition(
                AuraBodyType.Dynamic,
                new AuraPose(new AuraVector3(0f, 4f, 0f), AuraQuaternion.Identity),
                AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All, compound));
            Step(world, 400);
            world.TryGetBodyState(body, out var state);
            Near(state.Pose.Position.Y, 0.5f, 0.4f, "compound rest height");
        }

        private static void Conveyor_DragsBody()
        {
            using var world = NewWorld();
            var surface = world.AttachBody(world.CreateEntity(), AuraPhysicsBodyDefinition.CreateStatic(
                new AuraPose(new AuraVector3(0f, -0.5f, 0f), AuraQuaternion.Identity),
                AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All, AuraPhysicsShapeDefinition.Box(new AuraVector3(10f, 0.5f, 10f))));
            var surfaceEntity = FindEntity(world, surface);
            world.SetSurfaceVelocity(surfaceEntity, new AuraVector3(3f, 0f, 0f));
            var box = world.AttachBody(world.CreateEntity(), AuraPhysicsBodyDefinition.CreateDynamic(
                new AuraPose(new AuraVector3(0f, 0.5f, 0f), AuraQuaternion.Identity),
                AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All, AuraPhysicsShapeDefinition.Box(new AuraVector3(0.5f, 0.5f, 0.5f))));
            Step(world, 400);
            world.TryGetBodyState(box, out var state);
            Check(state.Pose.Position.X > 1f, $"conveyor did not drag the body, x={state.Pose.Position.X}.");
        }

        private static void SphereCast_Hits()
        {
            using var world = NewWorld();
            Ground(world);
            var hit = world.Queries.SphereCast(
                new AuraVector3(0f, 10f, 0f), 0.5f, new AuraVector3(0f, -1f, 0f), 100f,
                AuraPhysicsQueryFilter.All, out var result);
            Check(hit, "sphere cast missed the ground.");
        }

        private static void OverlapBox_Counts()
        {
            using var world = NewWorld();
            Ball(world, 2f);
            Ball(world, 2.5f);
            var buffer = new AuraPhysicsQueryHit[8];
            var count = world.Queries.OverlapBox(new AuraVector3(0f, 2.25f, 0f), new AuraVector3(2f, 2f, 2f), AuraQuaternion.Identity, AuraPhysicsQueryFilter.All, buffer);
            Check(count >= 2, $"overlap box found {count}, expected >= 2.");
        }

        private static void CollisionMatrix_Filters()
        {
            using var world = NewWorld();
            world.AttachBody(world.CreateEntity(), AuraPhysicsBodyDefinition.CreateStatic(
                new AuraPose(new AuraVector3(0f, -0.5f, 0f), AuraQuaternion.Identity),
                new AuraPhysicsLayer(1), new AuraPhysicsLayerMask(1UL << 0),
                AuraPhysicsShapeDefinition.Box(new AuraVector3(10f, 0.5f, 10f))));
            var ball = Ball(world, 3f);
            Step(world, 300);
            world.TryGetBodyState(ball, out var state);
            Check(state.Pose.Position.Y < 0f, $"a non-colliding pair still interacted, y={state.Pose.Position.Y}.");
        }

        private static void SurfaceVelocity_RoundTrip()
        {
            using var world = NewWorld();
            Ground(world);
            var ball = Ball(world, 0.5f);
            var entity = FindEntity(world, ball);
            var result = world.SetSurfaceVelocity(entity, new AuraVector3(2f, 0f, 0f));
            Check(result == AuraResult.Success, "SetSurfaceVelocity failed.");
        }

        private static void Water_BuoyancyLiftsBox()
        {
            using var world = NewWorld();
            var box = world.AttachBody(world.CreateEntity(), AuraPhysicsBodyDefinition.CreateDynamic(
                new AuraPose(new AuraVector3(0f, 0f, 0f), AuraQuaternion.Identity),
                AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All,
                AuraPhysicsShapeDefinition.Box(new AuraVector3(0.5f, 0.5f, 0.5f))));
            var water = world.Physics.CreateWater(new AuraWaterDefinition(0.25f, AuraVector3.UnitY));
            Check(water.IsValid, "water creation failed.");
            for (var index = 0; index < 30; index++)
            {
                Check(world.Physics.ApplyWaterStep(water, Dt) == AuraResult.Success, "water step failed.");
                world.Step(new SimulationStep(new SimulationTick((uint)index + 1u), Dt));
            }
            world.TryGetBodyState(box, out var state);
            Check(state.LinearVelocity.Y > 0f, $"water did not lift box, velocity={state.LinearVelocity.Y}.");
            Check(world.Physics.DestroyWater(water) == AuraResult.Success, "water destroy failed.");
        }

        /* Mirrors the serialized defaults of AuraVehicleAuthoring (the 'Car' in AuraDemoArticulation3D). */
        private static AuraVehicleId TryCreateAuthoringVehicle(AuraSimulationWorld world, float maxPitchRollAngle)
        {
            var entity = world.CreateEntity();
            world.AttachBody(entity, AuraPhysicsBodyDefinition.CreateDynamic(
                new AuraPose(new AuraVector3(0f, 1f, 0f), AuraQuaternion.Identity),
                AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All,
                AuraPhysicsShapeDefinition.Box(new AuraVector3(0.9f, 0.3f, 1.8f))));
            var wheels = new[]
            {
                new AuraVector3(-0.9f, -0.3f, 1.2f), new AuraVector3(0.9f, -0.3f, 1.2f),
                new AuraVector3(-0.9f, -0.3f, -1.2f), new AuraVector3(0.9f, -0.3f, -1.2f),
            };
            var definition = new AuraVehicleDefinition(
                PhysicsBodyId.Invalid, AuraVector3.UnitY, new AuraVector3(0f, 0f, 1f), wheels,
                0.35f, 0.25f, 0.2f, 0.5f, 4f, 0.7f, 30f * (float)Math.PI / 180f, maxPitchRollAngle, 800f);
            return world.CreateVehicle(entity, definition);
        }

        private static void Vehicle_AuthoringDefaultsCreate()
        {
            using var world = NewWorld();
            Ground(world);
            var vehicle = TryCreateAuthoringVehicle(world, AuraVehicleDefinition.DefaultMaxPitchRollAngle);
            Check(vehicle.IsValid, "vehicle with authoring defaults failed to create.");
        }

        private static void Vehicle_RejectsZeroPitchRoll()
        {
            using var world = NewWorld();
            Ground(world);
            Check(!TryCreateAuthoringVehicle(world, 0f).IsValid, "kernel accepted a zero pitch/roll limit.");
        }

        private static void NetPrediction_MatchesServer()
        {
            using var serverWorld = NewWorld();
            using var clientWorld = NewWorld();
            var serverHandler = new InputMover();
            serverWorld.RegisterCommandHandler(serverHandler);
            var serverEntity = serverWorld.CreateEntity();
            serverWorld.AttachBody(serverEntity, AuraPhysicsBodyDefinition.CreateKinematic(
                new AuraPose(AuraVector3.Zero, AuraQuaternion.Identity), AuraPhysicsLayer.Default,
                AuraPhysicsLayerMask.All, AuraPhysicsShapeDefinition.Box(new AuraVector3(0.5f, 0.5f, 0.5f))));
            serverHandler.Entity = serverEntity;

            var clientHandler = new InputMover();
            clientWorld.RegisterCommandHandler(clientHandler);
            var clientEntity = clientWorld.CreateEntity();
            clientWorld.AttachBody(clientEntity, AuraPhysicsBodyDefinition.CreateKinematic(
                new AuraPose(AuraVector3.Zero, AuraQuaternion.Identity), AuraPhysicsLayer.Default,
                AuraPhysicsLayerMask.All, AuraPhysicsShapeDefinition.Box(new AuraVector3(0.5f, 0.5f, 0.5f))));
            clientHandler.Entity = clientEntity;

            var (clientTransport, serverTransport) = InMemoryAuraTransport.CreatePair();
            var server = new AuraNetServer(serverWorld, serverTransport, 60);
            var predicted = new AuraPredictedWorld(clientWorld, new AuraPredictionReplay(clientEntity, 1f));
            var client = new AuraNetClient(1u, clientTransport, predicted, 60);

            for (var frame = 0; frame < 60; frame++)
            {
                client.SubmitInput(1, 0);
                server.Pump();
                server.Step();
                client.Pump();
            }

            Check(client.PendingInputCount == 0, "acknowledged inputs not cleared.");
            serverWorld.TryGetBodyState(serverEntity, out var serverState);
            clientWorld.TryGetBodyState(clientEntity, out var clientState);
            Near(clientState.Pose.Position.X, serverState.Pose.Position.X, 0.05f, "predicted X matches server");
        }

        // ---- ABI v10 runtime body / joint control ----

        private static AuraVector3 V(float x, float y, float z) => new AuraVector3(x, y, z);

        /* Both backends honor the definition mass (Box2D used to derive it from a 1000 kg/m^3 density). */
        private static float SphereMass(AuraPhysicsMode mode) => 1f;

        private static float UnitBoxMass(AuraPhysicsMode mode, float mass) => mass;

        private static PhysicsBodyId Dyn(AuraSimulationWorld world, AuraVector3 position, AuraPhysicsShapeDefinition shape,
            float gravityScale = 1f, float mass = 1f, int layer = 0, ulong mask = ulong.MaxValue) =>
            world.AttachBody(world.CreateEntity(), new AuraPhysicsBodyDefinition(
                AuraBodyType.Dynamic,
                new AuraPose(position, AuraQuaternion.Identity),
                new AuraPhysicsLayer(layer),
                new AuraPhysicsLayerMask(mask),
                new[] { shape },
                mass: mass,
                gravityScale: gravityScale));

        private static void Ok(AuraResult result, string label)
        {
            if (result != AuraResult.Success)
                throw new Exception($"{label}: expected Success, got {result}.");
        }

        private static void Expect(AuraResult actual, AuraResult expected, string label)
        {
            if (actual != expected)
                throw new Exception($"{label}: expected {expected}, got {actual}.");
        }

        private static AuraBodyState StateOf(AuraSimulationWorld world, PhysicsBodyId body)
        {
            Ok(world.Physics.GetBodyState(body, out var state), "GetBodyState");
            return state;
        }

        private static void Control_Impulse(AuraPhysicsMode mode)
        {
            using var world = NewWorld(mode);
            var ball = Dyn(world, V(0f, 5f, 0f), AuraPhysicsShapeDefinition.Sphere(0.5f), gravityScale: 0f);
            var mass = SphereMass(mode);
            Ok(world.BodyControl.AddImpulse(ball, V(2f * mass, 0f, 0f)), "AddImpulse");
            Step(world, 1);
            Near(StateOf(world, ball).LinearVelocity.X, 2f, 0.1f, "impulse delta-v");

            world.Physics.ApplyImpulse(ball, V(0f, mass, 0f));
            Step(world, 1, 1);
            Near(StateOf(world, ball).LinearVelocity.Y, 1f, 0.1f, "IPhysicsWorld.ApplyImpulse delta-v");
        }

        private static void Control_Velocity(AuraPhysicsMode mode)
        {
            using var world = NewWorld(mode);
            var ball = Dyn(world, V(0f, 5f, 0f), AuraPhysicsShapeDefinition.Sphere(0.5f), gravityScale: 0f);
            Ok(world.BodyControl.SetLinearVelocity(ball, V(3f, 0f, 0f)), "SetLinearVelocity");
            Ok(world.BodyControl.SetAngularVelocity(ball, V(0f, 0f, 2f)), "SetAngularVelocity");
            var before = StateOf(world, ball);
            Near(before.LinearVelocity.X, 3f, 1e-3f, "velocity readback");
            Near(before.AngularVelocity.Z, 2f, 1e-3f, "angular velocity readback");
            Step(world, 60);
            Near(StateOf(world, ball).Pose.Position.X, 3f, 0.1f, "distance travelled after one second");
        }

        private static void Control_ForceAndTorque(AuraPhysicsMode mode)
        {
            using var world = NewWorld(mode);
            var mass = SphereMass(mode);
            var pushed = Dyn(world, V(0f, 5f, 0f), AuraPhysicsShapeDefinition.Sphere(0.5f), gravityScale: 0f);
            for (var index = 0; index < 60; index++)
            {
                Ok(world.BodyControl.AddForce(pushed, V(10f * mass, 0f, 0f)), "AddForce");
                Step(world, 1, (uint)index);
            }

            Near(StateOf(world, pushed).LinearVelocity.X, 10f, 0.6f, "force-driven velocity after one second");

            var small = Dyn(world, V(20f, 5f, 0f), AuraPhysicsShapeDefinition.Sphere(0.5f), gravityScale: 0f);
            var large = Dyn(world, V(40f, 5f, 0f), AuraPhysicsShapeDefinition.Sphere(0.5f), gravityScale: 0f);
            Ok(world.BodyControl.AddAngularImpulse(small, V(0f, 0f, 1f * mass)), "AddAngularImpulse small");
            Ok(world.BodyControl.AddAngularImpulse(large, V(0f, 0f, 2f * mass)), "AddAngularImpulse large");
            Step(world, 1, 100);
            var wSmall = StateOf(world, small).AngularVelocity.Z;
            var wLarge = StateOf(world, large).AngularVelocity.Z;
            Check(wSmall > 0.01f, $"angular impulse did not spin the body ({wSmall}).");
            Near(wLarge / wSmall, 2f, 0.1f, "angular impulse scales linearly");

            var torqued = Dyn(world, V(60f, 5f, 0f), AuraPhysicsShapeDefinition.Sphere(0.5f), gravityScale: 0f);
            for (var index = 0; index < 10; index++)
            {
                Ok(world.BodyControl.AddTorque(torqued, V(0f, 0f, 5f * mass)), "AddTorque");
                Step(world, 1, 200u + (uint)index);
            }

            Check(StateOf(world, torqued).AngularVelocity.Z > 0.01f, "torque did not spin the body.");
        }

        private static void Control_Teleport(AuraPhysicsMode mode)
        {
            using var world = NewWorld(mode);
            var ball = Dyn(world, V(0f, 5f, 0f), AuraPhysicsShapeDefinition.Sphere(0.5f), gravityScale: 0f);
            Ok(world.BodyControl.SetLinearVelocity(ball, V(3f, 0f, 0f)), "SetLinearVelocity");

            var target = new AuraPose(V(10f, 2f, 0f), new AuraQuaternion(0f, 0f, MathF.Sin(0.25f), MathF.Cos(0.25f)));
            Ok(world.BodyControl.SetPose(ball, target, false), "SetPose keep velocity");
            var kept = StateOf(world, ball);
            Near(kept.Pose.Position.X, 10f, 1e-3f, "teleport X");
            Near(kept.Pose.Position.Y, 2f, 1e-3f, "teleport Y");
            Near(kept.Pose.Rotation.Z, MathF.Sin(0.25f), 1e-3f, "teleport rotation");
            Near(kept.LinearVelocity.X, 3f, 1e-3f, "velocity kept across teleport");

            Ok(world.BodyControl.SetPose(ball, new AuraPose(V(-4f, 7f, 0f), AuraQuaternion.Identity), true), "SetPose zero velocity");
            var zeroed = StateOf(world, ball);
            Near(zeroed.Pose.Position.X, -4f, 1e-3f, "teleport X (zero velocity)");
            Near(zeroed.LinearVelocity.X, 0f, 1e-3f, "velocity zeroed by teleport");
            Step(world, 30);
            Near(StateOf(world, ball).Pose.Position.X, -4f, 0.05f, "stays put after zeroed teleport");
        }

        private static void Control_GravityScale(AuraPhysicsMode mode)
        {
            using var world = NewWorld(mode);
            var floater = Dyn(world, V(0f, 50f, 0f), AuraPhysicsShapeDefinition.Sphere(0.5f));
            var faller = Dyn(world, V(5f, 50f, 0f), AuraPhysicsShapeDefinition.Sphere(0.5f));
            Ok(world.BodyControl.SetGravityScale(floater, 0f), "SetGravityScale");
            Step(world, 60);
            Near(StateOf(world, floater).Pose.Position.Y, 50f, 0.05f, "zero gravity scale floats");
            Check(StateOf(world, faller).Pose.Position.Y < 46f, "reference body did not fall.");

            Ok(world.BodyControl.SetGravityScale(floater, -1f), "SetGravityScale negative");
            Step(world, 60, 100);
            Check(StateOf(world, floater).Pose.Position.Y > 52f, "negative gravity scale did not lift the body.");
        }

        private static void Control_MotionType(AuraPhysicsMode mode)
        {
            using var world = NewWorld(mode);
            var ball = Dyn(world, V(0f, 100f, 0f), AuraPhysicsShapeDefinition.Sphere(0.5f));
            Step(world, 20);
            Ok(world.BodyControl.SetBodyType(ball, AuraBodyType.Kinematic), "to kinematic");
            Ok(world.BodyControl.SetLinearVelocity(ball, AuraVector3.Zero), "stop kinematic");
            Expect(world.BodyControl.AddForce(ball, V(1f, 0f, 0f)), AuraResult.InvalidDefinition, "force on kinematic");
            var y0 = StateOf(world, ball).Pose.Position.Y;
            Step(world, 60, 20);
            Near(StateOf(world, ball).Pose.Position.Y, y0, 0.05f, "kinematic body stops falling");

            Ok(world.BodyControl.SetBodyType(ball, AuraBodyType.Dynamic), "back to dynamic");
            Step(world, 30, 80);
            Check(StateOf(world, ball).Pose.Position.Y < y0 - 1f, "dynamic body did not resume falling.");

            Ok(world.BodyControl.SetBodyType(ball, AuraBodyType.Static), "to static");
            var y1 = StateOf(world, ball).Pose.Position.Y;
            Step(world, 30, 120);
            Near(StateOf(world, ball).Pose.Position.Y, y1, 0.01f, "static body does not move");
            Expect(world.BodyControl.SetLinearVelocity(ball, V(1f, 0f, 0f)), AuraResult.InvalidDefinition, "velocity on static");
            Ok(world.BodyControl.SetBodyType(ball, AuraBodyType.Dynamic), "static back to dynamic");
            Step(world, 30, 150);
            Check(StateOf(world, ball).Pose.Position.Y < y1 - 1f, "promoted static body did not fall.");
        }

        private static void Control_DisableEnable(AuraPhysicsMode mode)
        {
            using var world = NewWorld(mode);
            var ground = Ground(world);
            var ball = Dyn(world, V(0f, 3f, 0f), AuraPhysicsShapeDefinition.Sphere(0.5f));
            var down = new AuraRay(V(6f, 10f, 0f), V(0f, -1f, 0f));

            Check(world.Raycast(down, 100f, AuraPhysicsQueryFilter.All, out _), "ground raycast missed while enabled.");
            Ok(world.BodyControl.SetEnabled(ground, false), "disable ground");
            Ok(world.BodyControl.IsEnabled(ground, out var enabled), "IsEnabled");
            Check(!enabled, "ground reports enabled.");
            Check((StateOf(world, ground).Flags & (uint)AuraBodyStateFlags.Disabled) != 0u, "disabled flag missing from state.");
            Check(world.Physics.HasBody(ground), "disabled body lost its handle.");
            Check(!world.Raycast(down, 100f, AuraPhysicsQueryFilter.All, out _), "raycast hit a disabled body.");

            Step(world, 120);
            Check(StateOf(world, ball).Pose.Position.Y < -3f, "ball did not fall through the disabled ground.");

            Ok(world.BodyControl.SetEnabled(ground, true), "enable ground");
            Check(world.Raycast(down, 100f, AuraPhysicsQueryFilter.All, out _), "raycast missed the re-enabled ground.");
            Ok(world.BodyControl.SetPose(ball, new AuraPose(V(0f, 3f, 0f), AuraQuaternion.Identity), true), "reset ball");
            Step(world, 300, 120);
            Near(StateOf(world, ball).Pose.Position.Y, 0.5f, 0.2f, "ball rests on the re-enabled ground");

            Ok(world.BodyControl.SetEnabled(ball, false), "disable ball");
            Expect(world.BodyControl.AddImpulse(ball, V(1f, 0f, 0f)), AuraResult.BodyDisabled, "impulse on disabled body");
            Expect(world.BodyControl.SetLinearVelocity(ball, V(1f, 0f, 0f)), AuraResult.BodyDisabled, "velocity on disabled body");
            var y = StateOf(world, ball).Pose.Position.Y;
            Step(world, 60, 500);
            Near(StateOf(world, ball).Pose.Position.Y, y, 1e-3f, "disabled body is frozen");
            Ok(world.BodyControl.SetEnabled(ball, true), "re-enable ball");
            Ok(world.Physics.DestroyBody(ball), "destroy re-enabled body");
        }

        private static void Control_LayerMask(AuraPhysicsMode mode)
        {
            using var world = NewWorld(mode);
            Ground(world);
            var ball = Dyn(world, V(0f, 3f, 0f), AuraPhysicsShapeDefinition.Sphere(0.5f), layer: 1);
            Ok(world.BodyControl.SetLayer(ball, new AuraPhysicsLayer(2), new AuraPhysicsLayerMask(~1UL)), "SetLayer without layer 0");
            Step(world, 120);
            Check(StateOf(world, ball).Pose.Position.Y < -3f, "ball collided despite its mask excluding the ground layer.");

            Ok(world.BodyControl.SetLayer(ball, new AuraPhysicsLayer(1), AuraPhysicsLayerMask.All), "SetLayer back");
            Ok(world.BodyControl.SetPose(ball, new AuraPose(V(0f, 3f, 0f), AuraQuaternion.Identity), true), "reset ball");
            Step(world, 300, 120);
            Near(StateOf(world, ball).Pose.Position.Y, 0.5f, 0.2f, "ball rests once the mask allows the ground");
        }

        private static float MaxUpwardSpeed(AuraPhysicsMode mode, float restitution)
        {
            using var world = NewWorld(mode);
            var ground = Ground(world);
            var ball = Dyn(world, V(0f, 3f, 0f), AuraPhysicsShapeDefinition.Sphere(0.5f));
            if (restitution > 0f)
            {
                Ok(world.BodyControl.SetRestitution(ground, restitution), "SetRestitution ground");
                Ok(world.BodyControl.SetRestitution(ball, restitution), "SetRestitution ball");
            }

            var best = 0f;
            for (var index = 0; index < 240; index++)
            {
                Step(world, 1, (uint)index);
                best = Math.Max(best, StateOf(world, ball).LinearVelocity.Y);
            }

            return best;
        }

        private static void Control_Restitution(AuraPhysicsMode mode)
        {
            Check(MaxUpwardSpeed(mode, 0f) < 1f, "baseline ball bounced without restitution.");
            Check(MaxUpwardSpeed(mode, 1f) > 3f, "ball did not bounce after SetRestitution(1).");
        }

        private static float SlideSpeed(AuraPhysicsMode mode, float friction)
        {
            using var world = NewWorld(mode);
            var ground = Ground(world);
            var box = Dyn(world, V(0f, 0.5f, 0f), AuraPhysicsShapeDefinition.Box(V(0.5f, 0.5f, 0.5f)));
            Ok(world.BodyControl.SetFriction(ground, friction), "SetFriction ground");
            Ok(world.BodyControl.SetFriction(box, friction), "SetFriction box");
            Step(world, 30);
            Ok(world.BodyControl.SetLinearVelocity(box, V(5f, 0f, 0f)), "launch box");
            Step(world, 60, 30);
            return StateOf(world, box).LinearVelocity.X;
        }

        private static void Control_Friction(AuraPhysicsMode mode)
        {
            Check(SlideSpeed(mode, 0f) > 4.5f, "frictionless box slowed down.");
            Check(SlideSpeed(mode, 2f) < 2.5f, "high-friction box kept sliding.");
        }

        private static void Control_StaleAndInvalid(AuraPhysicsMode mode)
        {
            using var world = NewWorld(mode);
            var ground = Ground(world);
            var ball = Dyn(world, V(0f, 3f, 0f), AuraPhysicsShapeDefinition.Sphere(0.5f));
            var stale = Dyn(world, V(5f, 3f, 0f), AuraPhysicsShapeDefinition.Sphere(0.5f));
            Ok(world.Physics.DestroyBody(stale), "destroy");

            var control = world.BodyControl;
            var pose = AuraPose.Identity;
            foreach (var body in new[] { stale, PhysicsBodyId.Invalid, new PhysicsBodyId(9999, 0) })
            {
                Expect(control.SetLinearVelocity(body, AuraVector3.Zero), AuraResult.InvalidHandle, "stale SetLinearVelocity");
                Expect(control.SetAngularVelocity(body, AuraVector3.Zero), AuraResult.InvalidHandle, "stale SetAngularVelocity");
                Expect(control.AddForce(body, AuraVector3.Zero), AuraResult.InvalidHandle, "stale AddForce");
                Expect(control.AddImpulse(body, AuraVector3.Zero), AuraResult.InvalidHandle, "stale AddImpulse");
                Expect(control.AddTorque(body, AuraVector3.Zero), AuraResult.InvalidHandle, "stale AddTorque");
                Expect(control.AddAngularImpulse(body, AuraVector3.Zero), AuraResult.InvalidHandle, "stale AddAngularImpulse");
                Expect(control.SetPose(body, pose, true), AuraResult.InvalidHandle, "stale SetPose");
                Expect(control.SetGravityScale(body, 1f), AuraResult.InvalidHandle, "stale SetGravityScale");
                Expect(control.SetFriction(body, 1f), AuraResult.InvalidHandle, "stale SetFriction");
                Expect(control.SetRestitution(body, 1f), AuraResult.InvalidHandle, "stale SetRestitution");
                Expect(control.SetBodyType(body, AuraBodyType.Static), AuraResult.InvalidHandle, "stale SetBodyType");
                Expect(control.SetLayer(body, AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All), AuraResult.InvalidHandle, "stale SetLayer");
                Expect(control.SetEnabled(body, false), AuraResult.InvalidHandle, "stale SetEnabled");
                Expect(control.IsEnabled(body, out _), AuraResult.InvalidHandle, "stale IsEnabled");
            }

            var nan = float.NaN;
            Expect(control.SetLinearVelocity(ball, V(nan, 0f, 0f)), AuraResult.InvalidDefinition, "NaN velocity");
            Expect(control.AddImpulse(ball, V(0f, float.PositiveInfinity, 0f)), AuraResult.InvalidDefinition, "infinite impulse");
            Expect(control.SetGravityScale(ball, nan), AuraResult.InvalidDefinition, "NaN gravity scale");
            Expect(control.SetFriction(ball, -1f), AuraResult.InvalidDefinition, "negative friction");
            Expect(control.SetRestitution(ball, nan), AuraResult.InvalidDefinition, "NaN restitution");
            Expect(control.SetPose(ball, new AuraPose(V(nan, 0f, 0f), AuraQuaternion.Identity), false), AuraResult.InvalidDefinition, "NaN pose");
            Expect(control.SetPose(ball, new AuraPose(AuraVector3.Zero, new AuraQuaternion(0f, 0f, 0f, 0f)), false), AuraResult.InvalidDefinition, "zero quaternion");
            Expect(control.SetBodyType(ball, (AuraBodyType)42), AuraResult.InvalidDefinition, "unknown body type");
            Expect(control.AddForce(ground, V(1f, 0f, 0f)), AuraResult.InvalidDefinition, "force on static");
            Expect(control.SetLinearVelocity(ground, V(1f, 0f, 0f)), AuraResult.InvalidDefinition, "velocity on static");
            Check(world.Capabilities.HasFlag(AuraPhysicsCapabilities.BodyControl), "native world must report BodyControl.");
            Check(world.Capabilities.HasFlag(AuraPhysicsCapabilities.JointControl), "native world must report JointControl.");
            Step(world, 5);
            Near(StateOf(world, ball).Pose.Position.Y, 3f, 0.2f, "rejected calls left the body untouched");
        }

        private static void Control_NullBackendReportsUnsupported()
        {
            using var world = new AuraSimulationWorld(NullPhysicsBackend.Instance, new AuraWorldDefinition(AuraPhysicsMode.Full3D, default, null, 4));
            var body = Ball(world);
            Check(!world.Capabilities.HasFlag(AuraPhysicsCapabilities.BodyControl), "null backend must not claim BodyControl.");
            Check(!world.Capabilities.HasFlag(AuraPhysicsCapabilities.JointControl), "null backend must not claim JointControl.");
            Expect(world.BodyControl.AddImpulse(body, V(1f, 0f, 0f)), AuraResult.UnsupportedOperation, "null AddImpulse");
            Expect(world.BodyControl.SetEnabled(body, false), AuraResult.UnsupportedOperation, "null SetEnabled");
            Expect(world.JointControl.SetMotor(new AuraJointId(0, 0), AuraJointMotorDefinition.Off), AuraResult.UnsupportedOperation, "null SetMotor");
            Expect(world.JointControl.IsBroken(AuraJointId.Invalid, out _), AuraResult.UnsupportedOperation, "null IsBroken");
        }

        private static void Control_SnapshotKeepsDisabled(AuraPhysicsMode mode)
        {
            using var world = NewWorld(mode);
            Ground(world);
            var ball = Dyn(world, V(0f, 3f, 0f), AuraPhysicsShapeDefinition.Sphere(0.5f));
            Step(world, 10);
            Ok(world.BodyControl.SetEnabled(ball, false), "disable");
            var saved = world.SaveState();
            Ok(world.BodyControl.SetEnabled(ball, true), "enable");
            world.RestoreState(saved);
            Ok(world.BodyControl.IsEnabled(ball, out var enabled), "IsEnabled");
            Check(!enabled, "restored snapshot lost the disabled state.");

            var enabledSnapshot = world.SaveState();
            Ok(world.BodyControl.SetEnabled(ball, true), "enable again");
            world.RestoreState(enabledSnapshot);
            Ok(world.BodyControl.IsEnabled(ball, out enabled), "IsEnabled");
            Check(!enabled, "snapshot of a disabled body must stay disabled.");
        }

        private static (PhysicsBodyId Anchor, PhysicsBodyId Arm, AuraJointId Joint) HingeRig(AuraSimulationWorld world, float armGravity)
        {
            var anchor = world.AttachBody(world.CreateEntity(), AuraPhysicsBodyDefinition.CreateStatic(
                new AuraPose(V(0f, 5f, 0f), AuraQuaternion.Identity),
                AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All, AuraPhysicsShapeDefinition.Box(V(0.1f, 0.1f, 0.1f))));
            var arm = Dyn(world, V(1f, 5f, 0f), AuraPhysicsShapeDefinition.Box(V(0.5f, 0.1f, 0.1f)), gravityScale: armGravity);
            var joint = world.CreateJoint(FindEntity(world, anchor), FindEntity(world, arm),
                AuraJointDefinition.CreateHinge(anchor, arm, V(0f, 5f, 0f), V(0f, 5f, 0f), AuraVector3.UnitZ, AuraVector3.UnitZ));
            Check(joint.IsValid, "hinge creation failed.");
            return (anchor, arm, joint);
        }

        private static void Joint_HingeMotorVelocity(AuraPhysicsMode mode)
        {
            using var world = NewWorld(mode);
            var rig = HingeRig(world, 0f);
            Ok(world.JointControl.SetMotor(rig.Joint, AuraJointMotorDefinition.Velocity(2f, 1.0e5f)), "SetMotor velocity");
            Step(world, 60);
            Near(Math.Abs(StateOf(world, rig.Arm).AngularVelocity.Z), 2f, 0.2f, "hinge motor reaches the target velocity");
            Ok(world.JointControl.GetFeedback(rig.Joint, out var feedback), "GetFeedback");
            Check(feedback.MotorMode == AuraJointMotorMode.Velocity, "feedback motor mode.");
            Check(Math.Abs(feedback.Position) > 0.5f, $"hinge angle did not advance ({feedback.Position}).");

            Ok(world.JointControl.SetMotor(rig.Joint, AuraJointMotorDefinition.Off), "SetMotor off");
            Ok(world.JointControl.GetFeedback(rig.Joint, out feedback), "GetFeedback off");
            Check(feedback.MotorMode == AuraJointMotorMode.Off, "motor did not switch off.");
            Expect(world.JointControl.SetMotor(rig.Joint, AuraJointMotorDefinition.Velocity(1f, 0f)), AuraResult.InvalidDefinition, "motor without force");
            Expect(world.JointControl.SetMotor(rig.Joint, AuraJointMotorDefinition.Velocity(float.NaN, 1f)), AuraResult.InvalidDefinition, "NaN motor target");
        }

        private static void Joint_HingeMotorPosition3D()
        {
            using var world = NewWorld();
            var rig = HingeRig(world, 0f);
            Ok(world.JointControl.SetMotor(rig.Joint, AuraJointMotorDefinition.Position(0.5f, 1.0e5f, 4f, 1f)), "SetMotor position");
            Step(world, 180);
            Ok(world.JointControl.GetFeedback(rig.Joint, out var feedback), "GetFeedback");
            Near(feedback.Position, 0.5f, 0.1f, "hinge position motor target");
            Check(feedback.MotorMode == AuraJointMotorMode.Position, "feedback motor mode.");
        }

        private static void Joint_HingePositionMotorUnsupported2D()
        {
            using var world = NewWorld(AuraPhysicsMode.Plane2D);
            var rig = HingeRig(world, 0f);
            Expect(world.JointControl.SetMotor(rig.Joint, AuraJointMotorDefinition.Position(0.5f, 100f)), AuraResult.UnsupportedOperation, "Box2D position motor");
            Ok(world.JointControl.SetMotor(rig.Joint, AuraJointMotorDefinition.Velocity(1f, 100f)), "Box2D velocity motor still works");
        }

        private static void Joint_HingeLimits(AuraPhysicsMode mode)
        {
            using var world = NewWorld(mode);
            var rig = HingeRig(world, 1f);
            Ok(world.JointControl.SetLimits(rig.Joint, true, -0.5f, 0.5f), "SetLimits");
            Step(world, 150);
            Ok(world.JointControl.GetFeedback(rig.Joint, out var feedback), "GetFeedback");
            Check(Math.Abs(feedback.Position) <= 0.6f, $"hinge exceeded its limit ({feedback.Position}).");
            Check(Math.Abs(feedback.Position) >= 0.35f, $"hinge never reached its limit ({feedback.Position}).");

            Expect(world.JointControl.SetLimits(rig.Joint, true, 0.2f, 0.5f), AuraResult.InvalidDefinition, "limits excluding zero");
            Expect(world.JointControl.SetLimits(rig.Joint, true, -4f, 0.5f), AuraResult.InvalidDefinition, "limits beyond pi");
            Expect(world.JointControl.SetLimits(rig.Joint, true, float.NaN, 0.5f), AuraResult.InvalidDefinition, "NaN limit");

            Ok(world.JointControl.SetLimits(rig.Joint, false, 0f, 0f), "disable limits");
            Step(world, 90, 150);
            Ok(world.JointControl.GetFeedback(rig.Joint, out feedback), "GetFeedback unlimited");
            Check(Math.Abs(feedback.Position) > 0.9f, $"arm stayed clamped after disabling limits ({feedback.Position}).");
        }

        private static (PhysicsBodyId Anchor, PhysicsBodyId Cart, AuraJointId Joint) SliderRig(AuraSimulationWorld world)
        {
            var anchor = world.AttachBody(world.CreateEntity(), AuraPhysicsBodyDefinition.CreateStatic(
                new AuraPose(V(0f, 3f, 0f), AuraQuaternion.Identity),
                AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All, AuraPhysicsShapeDefinition.Box(V(0.1f, 0.1f, 0.1f))));
            var cart = Dyn(world, V(0f, 5f, 0f), AuraPhysicsShapeDefinition.Box(V(0.5f, 0.5f, 0.5f)), gravityScale: 0f);
            var joint = world.CreateJoint(FindEntity(world, anchor), FindEntity(world, cart),
                AuraJointDefinition.CreateSlider(anchor, cart, V(0f, 5f, 0f), V(0f, 5f, 0f), AuraVector3.UnitX, AuraVector3.UnitX));
            Check(joint.IsValid, "slider creation failed.");
            return (anchor, cart, joint);
        }

        private static void Joint_SliderMotorAndLimits(AuraPhysicsMode mode)
        {
            using var world = NewWorld(mode);
            var rig = SliderRig(world);
            Ok(world.JointControl.SetMotor(rig.Joint, AuraJointMotorDefinition.Velocity(1f, 1.0e5f)), "SetMotor velocity");
            Step(world, 60);
            Near(StateOf(world, rig.Cart).LinearVelocity.X, 1f, 0.15f, "slider motor reaches the target velocity");
            Near(StateOf(world, rig.Cart).Pose.Position.X, 1f, 0.2f, "slider travel");
            Ok(world.JointControl.GetFeedback(rig.Joint, out var feedback), "GetFeedback");
            Near(feedback.Position, 1f, 0.2f, "slider feedback position");

            Ok(world.JointControl.SetMotor(rig.Joint, AuraJointMotorDefinition.Velocity(-2f, 200f)), "reverse motor");
            Ok(world.JointControl.SetLimits(rig.Joint, true, -0.5f, 0.5f), "SetLimits");
            Step(world, 180, 60);
            Near(StateOf(world, rig.Cart).Pose.Position.X, -0.5f, 0.1f, "slider limit clamps travel");
            Expect(world.JointControl.SetLimits(rig.Joint, true, 0.5f, 1f), AuraResult.InvalidDefinition, "slider limits excluding zero");
        }

        private static void Joint_SliderPositionMotor3D()
        {
            using var world = NewWorld();
            var rig = SliderRig(world);
            Ok(world.JointControl.SetMotor(rig.Joint, AuraJointMotorDefinition.Position(0.4f, 1.0e5f, 4f, 1f)), "SetMotor position");
            Step(world, 180);
            Near(StateOf(world, rig.Cart).Pose.Position.X, 0.4f, 0.1f, "slider position motor target");
        }

        /* Body hanging from a static anchor through a fixed joint; returns the weight in newtons. */
        private static (PhysicsBodyId Weight, AuraJointId Joint, float Newtons) HangingRig(AuraSimulationWorld world, AuraPhysicsMode mode)
        {
            var anchor = world.AttachBody(world.CreateEntity(), AuraPhysicsBodyDefinition.CreateStatic(
                new AuraPose(V(0f, 6f, 0f), AuraQuaternion.Identity),
                AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All, AuraPhysicsShapeDefinition.Box(V(0.1f, 0.1f, 0.1f))));
            var weight = Dyn(world, V(0f, 5f, 0f), AuraPhysicsShapeDefinition.Box(V(0.5f, 0.5f, 0.5f)), mass: 10f);
            var joint = world.CreateJoint(FindEntity(world, anchor), FindEntity(world, weight),
                AuraJointDefinition.CreateFixed(anchor, weight, V(0f, 5.5f, 0f), V(0f, 5.5f, 0f)));
            Check(joint.IsValid, "fixed joint creation failed.");
            return (weight, joint, UnitBoxMass(mode, 10f) * 9.81f);
        }

        private static void Joint_BreakThreshold(AuraPhysicsMode mode)
        {
            using var world = NewWorld(mode);
            var rig = HangingRig(world, mode);
            Step(world, 60);
            Near(StateOf(world, rig.Weight).Pose.Position.Y, 5f, 0.15f, "joint holds the weight");
            Ok(world.JointControl.GetFeedback(rig.Joint, out var feedback), "GetFeedback");
            Near(feedback.Force, rig.Newtons, rig.Newtons * 0.25f, "joint reaction force carries the weight");
            Check(!feedback.IsBroken, "joint reported broken without a threshold.");

            Ok(world.JointControl.SetBreakThreshold(rig.Joint, rig.Newtons * 0.5f, 0f), "SetBreakThreshold");
            Step(world, 10, 60);
            Ok(world.JointControl.IsBroken(rig.Joint, out var broken), "IsBroken");
            Check(broken, "joint did not break above its threshold.");
            Check(!world.HasJoint(rig.Joint), "broken joint is still reported as live.");
            Ok(world.JointControl.GetFeedback(rig.Joint, out feedback), "GetFeedback broken");
            Check(feedback.IsBroken && feedback.Force > rig.Newtons * 0.5f, "broken feedback lost the breaking load.");

            Step(world, 60, 70);
            Check(StateOf(world, rig.Weight).Pose.Position.Y < 3f, "weight did not fall after the joint broke.");

            Expect(world.JointControl.SetMotor(rig.Joint, AuraJointMotorDefinition.Off), AuraResult.InvalidHandle, "motor on broken joint");
            Expect(world.JointControl.SetBreakThreshold(rig.Joint, 1f, 0f), AuraResult.InvalidHandle, "threshold on broken joint");
            Ok(world.DestroyJoint(rig.Joint), "DestroyJoint releases the broken handle");
            Expect(world.DestroyJoint(rig.Joint), AuraResult.InvalidHandle, "double destroy");
            Expect(world.JointControl.IsBroken(rig.Joint, out _), AuraResult.InvalidHandle, "IsBroken after destroy");
            Step(world, 10, 130);
        }

        private static void Joint_HoldsBelowThreshold(AuraPhysicsMode mode)
        {
            using var world = NewWorld(mode);
            var rig = HangingRig(world, mode);
            Ok(world.JointControl.SetBreakThreshold(rig.Joint, rig.Newtons * 20f, rig.Newtons * 20f), "SetBreakThreshold");
            Step(world, 180);
            Ok(world.JointControl.IsBroken(rig.Joint, out var broken), "IsBroken");
            Check(!broken, "joint broke below its threshold.");
            Check(world.HasJoint(rig.Joint), "joint vanished below its threshold.");
            Near(StateOf(world, rig.Weight).Pose.Position.Y, 5f, 0.15f, "joint still holds");

            Ok(world.JointControl.SetBreakThreshold(rig.Joint, 0f, 0f), "clear threshold");
            Expect(world.JointControl.SetBreakThreshold(rig.Joint, -1f, 0f), AuraResult.InvalidDefinition, "negative threshold");
            Expect(world.JointControl.SetBreakThreshold(rig.Joint, float.NaN, 0f), AuraResult.InvalidDefinition, "NaN threshold");
        }

        private static void Joint_UnsupportedAndStale(AuraPhysicsMode mode)
        {
            using var world = NewWorld(mode);
            var rig = HangingRig(world, mode);
            var control = world.JointControl;

            Expect(control.SetMotor(rig.Joint, AuraJointMotorDefinition.Velocity(1f, 10f)), AuraResult.UnsupportedOperation, "motor on fixed joint");
            Expect(control.SetLimits(rig.Joint, true, -1f, 1f), AuraResult.UnsupportedOperation, "limits on fixed joint");
            Ok(control.SetBreakThreshold(rig.Joint, 1.0e6f, 1.0e6f), "fixed joint supports break thresholds");
            Ok(control.GetFeedback(rig.Joint, out var feedback), "feedback on fixed joint");
            Check(!feedback.IsBroken, "fresh joint reported broken.");

            var a = Dyn(world, V(10f, 5f, 0f), AuraPhysicsShapeDefinition.Sphere(0.5f), gravityScale: 0f);
            var b = Dyn(world, V(12f, 5f, 0f), AuraPhysicsShapeDefinition.Sphere(0.5f), gravityScale: 0f);
            var distance = world.CreateJoint(FindEntity(world, a), FindEntity(world, b),
                AuraJointDefinition.CreateDistance(a, b, V(10f, 5f, 0f), V(12f, 5f, 0f), 2f));
            Check(distance.IsValid, "distance joint creation failed.");
            Expect(control.SetBreakThreshold(distance, 10f, 5f), AuraResult.InvalidDefinition, "torque threshold on a force-only joint");
            Ok(control.SetBreakThreshold(distance, 10f, 0f), "force threshold on distance joint");
            Expect(control.SetMotor(distance, AuraJointMotorDefinition.Off), AuraResult.UnsupportedOperation, "motor on distance joint");

            foreach (var stale in new[] { AuraJointId.Invalid, new AuraJointId(0, 77), new AuraJointId(4000, 0) })
            {
                Expect(control.SetMotor(stale, AuraJointMotorDefinition.Off), AuraResult.InvalidHandle, "stale SetMotor");
                Expect(control.SetLimits(stale, false, 0f, 0f), AuraResult.InvalidHandle, "stale SetLimits");
                Expect(control.SetBreakThreshold(stale, 1f, 0f), AuraResult.InvalidHandle, "stale SetBreakThreshold");
                Expect(control.IsBroken(stale, out _), AuraResult.InvalidHandle, "stale IsBroken");
                Expect(control.GetFeedback(stale, out _), AuraResult.InvalidHandle, "stale GetFeedback");
            }

            Ok(world.DestroyJoint(rig.Joint), "destroy fixed joint");
            Expect(control.GetFeedback(rig.Joint, out _), AuraResult.InvalidHandle, "feedback after destroy");
        }

        private static SimulationEntityId FindEntity(AuraSimulationWorld world, PhysicsBodyId body)
        {
            var states = new AuraBodyState[16];
            var count = world.CopyBodyStates(states);
            for (var index = 0; index < count; index++)
                if (states[index].Body.Index == body.Index && states[index].Body.Generation == body.Generation)
                    return states[index].Entity;
            return SimulationEntityId.None;
        }

        private static void Check(bool condition, string message)
        {
            if (!condition)
                throw new Exception(message);
        }

        private static void Near(float actual, float expected, float tolerance, string label)
        {
            if (Math.Abs(actual - expected) > tolerance)
                throw new Exception($"{label}: expected {expected} +/- {tolerance}, got {actual}.");
        }

        private sealed class InputMover : AuraEngine.Simulation.IAuraCommandHandler
        {
            public SimulationEntityId Entity;
            ushort AuraEngine.Simulation.IAuraCommandHandler.TypeId => AuraInputCommand.CommandTypeId;
            AuraResult AuraEngine.Simulation.IAuraCommandHandler.Handle(AuraEngine.Simulation.IAuraSimulationContext context, IAuraCommand command)
            {
                if (!(command is AuraInputCommand input) || Entity.IsNone)
                    return AuraResult.InvalidDefinition;
                if (!context.TryGetBodyState(Entity, out var state))
                    return AuraResult.InvalidHandle;
                var position = state.Pose.Position + new AuraVector3(input.MoveX, 0f, input.MoveY);
                return context.SetKinematicTarget(Entity, new AuraPose(position, state.Pose.Rotation));
            }
        }
    }
}
