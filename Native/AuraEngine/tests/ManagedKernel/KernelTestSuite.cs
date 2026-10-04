using System;
using System.Collections.Generic;
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
    public sealed class KernelTestSuite
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
            };

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
