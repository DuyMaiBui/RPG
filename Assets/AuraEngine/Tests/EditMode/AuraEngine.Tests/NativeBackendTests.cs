using System;
using AuraEngine.Core;
using AuraEngine.Physics.Native;
using AuraEngine.Simulation;
using NUnit.Framework;

namespace AuraEngine.Tests
{
    public sealed class NativeBackendTests
    {
        private static AuraSimulationWorld CreateWorld(out NativePhysicsBackend backend)
        {
            backend = new NativePhysicsBackend();
            return new AuraSimulationWorld(backend, new AuraWorldDefinition(initialBodyCapacity: 8));
        }

        [Test]
        public void NativeBackend_IsAvailableAndRunsSimulation()
        {
            Assert.IsTrue(NativePhysicsBackend.IsAvailable(), "libaura is not loadable.");

            using var world = CreateWorld(out _);
            world.AttachBody(
                world.CreateEntity(),
                AuraPhysicsBodyDefinition.CreateStatic(
                    new AuraPose(new AuraVector3(0f, -0.5f, 0f), AuraQuaternion.Identity),
                    AuraPhysicsLayer.Default,
                    AuraPhysicsLayerMask.All,
                    AuraPhysicsShapeDefinition.Box(new AuraVector3(10f, 0.5f, 10f))));

            var ball = world.AttachBody(
                world.CreateEntity(),
                AuraPhysicsBodyDefinition.CreateDynamic(
                    new AuraPose(new AuraVector3(0f, 5f, 0f), AuraQuaternion.Identity),
                    AuraPhysicsLayer.Default,
                    AuraPhysicsLayerMask.All,
                    AuraPhysicsShapeDefinition.Sphere(0.5f)));

            for (var tick = 1; tick <= 300; tick++)
                world.Step(new SimulationStep(new SimulationTick((uint)tick), 1f / 60f));

            Assert.IsTrue(world.TryGetBodyState(ball, out var state));
            Assert.Less(Math.Abs(state.Pose.Position.Y - 0.5f), 0.3f);

            var hit = world.Raycast(
                new AuraRay(new AuraVector3(0f, 10f, 0f), new AuraVector3(0f, -1f, 0f)),
                100f,
                AuraPhysicsQueryFilter.All,
                out var result);
            Assert.IsTrue(hit);
            Assert.Greater(result.Distance, 0f);
        }

        [Test]
        public void NativeBackend_ConvexHull_CollidesWithGround()
        {
            using var world = CreateWorld(out _);
            world.AttachBody(
                world.CreateEntity(),
                AuraPhysicsBodyDefinition.CreateStatic(
                    new AuraPose(new AuraVector3(0f, -0.5f, 0f), AuraQuaternion.Identity),
                    AuraPhysicsLayer.Default,
                    AuraPhysicsLayerMask.All,
                    AuraPhysicsShapeDefinition.Box(new AuraVector3(10f, 0.5f, 10f))));

            var hull = new[]
            {
                new AuraVector3(-0.5f, -0.5f, -0.5f), new AuraVector3(0.5f, -0.5f, -0.5f),
                new AuraVector3(-0.5f, 0.5f, -0.5f), new AuraVector3(0.5f, 0.5f, -0.5f),
                new AuraVector3(-0.5f, -0.5f, 0.5f), new AuraVector3(0.5f, -0.5f, 0.5f),
                new AuraVector3(-0.5f, 0.5f, 0.5f), new AuraVector3(0.5f, 0.5f, 0.5f),
            };

            var shape = new AuraPhysicsShapeDefinition(
                AuraShapeType.ConvexMesh,
                AuraPose.Identity,
                false,
                AuraPhysicsMaterialDefinition.Default,
                AuraPhysicsLayer.Default,
                AuraShapeGeometry.ConvexMesh(hull));

            var body = world.AttachBody(
                world.CreateEntity(),
                AuraPhysicsBodyDefinition.CreateDynamic(
                    new AuraPose(new AuraVector3(0f, 4f, 0f), AuraQuaternion.Identity),
                    AuraPhysicsLayer.Default,
                    AuraPhysicsLayerMask.All,
                    shape));

            for (var tick = 1; tick <= 300; tick++)
                world.Step(new SimulationStep(new SimulationTick((uint)tick), 1f / 60f));

            Assert.IsTrue(world.TryGetBodyState(body, out var state));
            Assert.Less(System.Math.Abs(state.Pose.Position.Y - 0.5f), 0.3f);
        }

        [Test]
        public void NativeBackend_TaperedCapsule_CollidesWithGround()
        {
            using var world = CreateWorld(out _);
            world.AttachBody(
                world.CreateEntity(),
                AuraPhysicsBodyDefinition.CreateStatic(
                    new AuraPose(new AuraVector3(0f, -0.5f, 0f), AuraQuaternion.Identity),
                    AuraPhysicsLayer.Default,
                    AuraPhysicsLayerMask.All,
                    AuraPhysicsShapeDefinition.Box(new AuraVector3(10f, 0.5f, 10f))));

            var shape = new AuraPhysicsShapeDefinition(
                AuraShapeType.TaperedCapsule,
                AuraPose.Identity,
                false,
                AuraPhysicsMaterialDefinition.Default,
                AuraPhysicsLayer.Default,
                AuraShapeGeometry.TaperedCapsule(0.5f, 0.2f, 2f));

            var body = world.AttachBody(
                world.CreateEntity(),
                AuraPhysicsBodyDefinition.CreateDynamic(
                    new AuraPose(new AuraVector3(0f, 4f, 0f), AuraQuaternion.Identity),
                    AuraPhysicsLayer.Default,
                    AuraPhysicsLayerMask.All,
                    shape));

            for (var tick = 1; tick <= 300; tick++)
                world.Step(new SimulationStep(new SimulationTick((uint)tick), 1f / 60f));

            Assert.IsTrue(world.TryGetBodyState(body, out var state));
            Assert.IsFalse(float.IsNaN(state.Pose.Position.Y), "tapered capsule produced NaN");
            Assert.Greater(state.Pose.Position.Y, 0.1f, $"tapered capsule fell through, y={state.Pose.Position.Y}");
        }

        [Test]
        public void NativeBackend_TaperedCylinder_CollidesWithGround()
        {
            using var world = CreateWorld(out _);
            world.AttachBody(
                world.CreateEntity(),
                AuraPhysicsBodyDefinition.CreateStatic(
                    new AuraPose(new AuraVector3(0f, -0.5f, 0f), AuraQuaternion.Identity),
                    AuraPhysicsLayer.Default,
                    AuraPhysicsLayerMask.All,
                    AuraPhysicsShapeDefinition.Box(new AuraVector3(10f, 0.5f, 10f))));

            var shape = new AuraPhysicsShapeDefinition(
                AuraShapeType.TaperedCylinder,
                AuraPose.Identity,
                false,
                AuraPhysicsMaterialDefinition.Default,
                AuraPhysicsLayer.Default,
                AuraShapeGeometry.TaperedCylinder(0.5f, 0.15f, 2f));

            var body = world.AttachBody(
                world.CreateEntity(),
                AuraPhysicsBodyDefinition.CreateDynamic(
                    new AuraPose(new AuraVector3(0f, 4f, 0f), AuraQuaternion.Identity),
                    AuraPhysicsLayer.Default,
                    AuraPhysicsLayerMask.All,
                    shape));

            for (var tick = 1; tick <= 300; tick++)
                world.Step(new SimulationStep(new SimulationTick((uint)tick), 1f / 60f));

            Assert.IsTrue(world.TryGetBodyState(body, out var state));
            Assert.IsFalse(float.IsNaN(state.Pose.Position.X) || float.IsNaN(state.Pose.Position.Y) || float.IsNaN(state.Pose.Position.Z), "tapered cylinder produced NaN");
            Assert.Less(System.Math.Abs(state.Pose.Position.Y - 1f), 0.05f, $"tapered cylinder rest y={state.Pose.Position.Y}");
        }

        [Test]
        public void NativeBackend_FrictionSlowsSlidingBody()
        {
            using var world = CreateWorld(out _);
            var material = new AuraPhysicsMaterialDefinition(0.9f, 0f, 1000f);

            world.AttachBody(
                world.CreateEntity(),
                new AuraPhysicsBodyDefinition(
                    AuraBodyType.Static,
                    new AuraPose(new AuraVector3(0f, -0.5f, 0f), AuraQuaternion.Identity),
                    AuraPhysicsLayer.Default,
                    AuraPhysicsLayerMask.All,
                    new[] { AuraPhysicsShapeDefinition.Box(new AuraVector3(20f, 0.5f, 20f)) },
                    1f,
                    1f,
                    0,
                    material));

            var body = world.AttachBody(
                world.CreateEntity(),
                new AuraPhysicsBodyDefinition(
                    AuraBodyType.Dynamic,
                    new AuraPose(new AuraVector3(0f, 0.5f, 0f), AuraQuaternion.Identity),
                    AuraPhysicsLayer.Default,
                    AuraPhysicsLayerMask.All,
                    new[] { AuraPhysicsShapeDefinition.Box(new AuraVector3(0.5f, 0.5f, 0.5f)) },
                    1f,
                    1f,
                    0,
                    material,
                    new AuraVector3(5f, 0f, 0f)));

            for (var tick = 1; tick <= 300; tick++)
                world.Step(new SimulationStep(new SimulationTick((uint)tick), 1f / 60f));

            Assert.IsTrue(world.TryGetBodyState(body, out var state));
            Assert.Less(System.Math.Abs(state.LinearVelocity.X), 1.5f, $"vx={state.LinearVelocity.X}");
        }

        [Test]
        public void NativeBackend_FreezePosition_StopsFalling()
        {
            using var world = CreateWorld(out _);
            var body = world.AttachBody(
                world.CreateEntity(),
                new AuraPhysicsBodyDefinition(
                    AuraBodyType.Dynamic,
                    new AuraPose(new AuraVector3(0f, 5f, 0f), AuraQuaternion.Identity),
                    AuraPhysicsLayer.Default,
                    AuraPhysicsLayerMask.All,
                    new[] { AuraPhysicsShapeDefinition.Sphere(0.5f) },
                    freeze: AuraBodyFreezeFlags.Position));

            for (var tick = 1; tick <= 120; tick++)
                world.Step(new SimulationStep(new SimulationTick((uint)tick), 1f / 60f));

            Assert.IsTrue(world.TryGetBodyState(body, out var state));
            Assert.Less(System.Math.Abs(state.Pose.Position.Y - 5f), 0.05f, $"y={state.Pose.Position.Y}");
        }

        [Test]
        public void NativeBackend_ZeroGravity_DoesNotFall()
        {
            using var world = CreateWorld(out _);
            var body = world.AttachBody(
                world.CreateEntity(),
                new AuraPhysicsBodyDefinition(
                    AuraBodyType.Dynamic,
                    new AuraPose(new AuraVector3(0f, 5f, 0f), AuraQuaternion.Identity),
                    AuraPhysicsLayer.Default,
                    AuraPhysicsLayerMask.All,
                    new[] { AuraPhysicsShapeDefinition.Sphere(0.5f) },
                    gravityScale: 0f));

            for (var tick = 1; tick <= 120; tick++)
                world.Step(new SimulationStep(new SimulationTick((uint)tick), 1f / 60f));

            Assert.IsTrue(world.TryGetBodyState(body, out var state));
            Assert.Less(System.Math.Abs(state.Pose.Position.Y - 5f), 0.1f, $"y={state.Pose.Position.Y}");
        }

        [Test]
        public void NativeBackend_FreezeRotation_ZeroesAngularVelocity()
        {
            using var world = CreateWorld(out _);
            var body = world.AttachBody(
                world.CreateEntity(),
                new AuraPhysicsBodyDefinition(
                    AuraBodyType.Dynamic,
                    AuraPose.Identity,
                    AuraPhysicsLayer.Default,
                    AuraPhysicsLayerMask.All,
                    new[] { AuraPhysicsShapeDefinition.Box(new AuraVector3(0.5f, 0.5f, 0.5f)) },
                    freeze: AuraBodyFreezeFlags.Rotation,
                    initialAngularVelocity: new AuraVector3(0f, 3f, 0f)));

            for (var tick = 1; tick <= 30; tick++)
                world.Step(new SimulationStep(new SimulationTick((uint)tick), 1f / 60f));

            Assert.IsTrue(world.TryGetBodyState(body, out var state));
            Assert.Less(state.AngularVelocity.Length, 0.01f, $"w={state.AngularVelocity}");
        }

        [Test]
        public void NativeBackend_Plane_CollidesWithSphere()
        {
            using var world = CreateWorld(out _);
            world.AttachBody(
                world.CreateEntity(),
                AuraPhysicsBodyDefinition.CreateStatic(
                    AuraPose.Identity,
                    AuraPhysicsLayer.Default,
                    AuraPhysicsLayerMask.All,
                    new AuraPhysicsShapeDefinition(
                        AuraShapeType.Plane,
                        AuraPose.Identity,
                        false,
                        AuraPhysicsMaterialDefinition.Default,
                        AuraPhysicsLayer.Default,
                        AuraShapeGeometry.Plane(AuraVector3.UnitY))));

            var body = world.AttachBody(
                world.CreateEntity(),
                AuraPhysicsBodyDefinition.CreateDynamic(
                    new AuraPose(new AuraVector3(0f, 3f, 0f), AuraQuaternion.Identity),
                    AuraPhysicsLayer.Default,
                    AuraPhysicsLayerMask.All,
                    AuraPhysicsShapeDefinition.Sphere(0.5f)));

            for (var tick = 1; tick <= 300; tick++)
                world.Step(new SimulationStep(new SimulationTick((uint)tick), 1f / 60f));

            Assert.IsTrue(world.TryGetBodyState(body, out var state));
            Assert.Less(System.Math.Abs(state.Pose.Position.Y - 0.5f), 0.3f);
        }

        [Test]
        public void NativeBackend_FixedJoint_KeepsRelativeDistance()
        {
            using var world = CreateWorld(out _);
            var entityA = world.CreateEntity();
            var bodyA = world.AttachBody(entityA, AuraPhysicsBodyDefinition.CreateDynamic(
                new AuraPose(new AuraVector3(-1f, 4f, 0f), AuraQuaternion.Identity),
                AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All, AuraPhysicsShapeDefinition.Box(new AuraVector3(0.5f, 0.5f, 0.5f))));
            var entityB = world.CreateEntity();
            var bodyB = world.AttachBody(entityB, AuraPhysicsBodyDefinition.CreateDynamic(
                new AuraPose(new AuraVector3(1f, 4f, 0f), AuraQuaternion.Identity),
                AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All, AuraPhysicsShapeDefinition.Box(new AuraVector3(0.5f, 0.5f, 0.5f))));

            var joint = world.CreateJoint(entityA, entityB, AuraJointDefinition.CreateFixed(
                bodyA, bodyB, new AuraVector3(0f, 4f, 0f), new AuraVector3(0f, 4f, 0f)));
            Assert.IsTrue(joint.IsValid);
            Assert.IsTrue(world.HasJoint(joint));

            for (var tick = 1; tick <= 240; tick++)
                world.Step(new SimulationStep(new SimulationTick((uint)tick), 1f / 60f));

            Assert.IsTrue(world.TryGetBodyState(bodyA, out var a));
            Assert.IsTrue(world.TryGetBodyState(bodyB, out var b));
            Assert.Less(System.Math.Abs(AuraVector3.Distance(a.Pose.Position, b.Pose.Position) - 2f), 0.35f);
        }

        [Test]
        public void NativeBackend_PointJoint_KeepsAnchorsCoincident()
        {
            using var world = CreateWorld(out _);
            var entityA = world.CreateEntity();
            var bodyA = world.AttachBody(entityA, AuraPhysicsBodyDefinition.CreateDynamic(
                new AuraPose(new AuraVector3(-1f, 4f, 0f), AuraQuaternion.Identity),
                AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All, AuraPhysicsShapeDefinition.Box(new AuraVector3(0.5f, 0.5f, 0.5f))));
            var entityB = world.CreateEntity();
            var bodyB = world.AttachBody(entityB, AuraPhysicsBodyDefinition.CreateDynamic(
                new AuraPose(new AuraVector3(1f, 4f, 0f), AuraQuaternion.Identity),
                AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All, AuraPhysicsShapeDefinition.Box(new AuraVector3(0.5f, 0.5f, 0.5f))));

            world.CreateJoint(entityA, entityB, AuraJointDefinition.CreatePoint(
                bodyA, bodyB, new AuraVector3(0f, 4f, 0f), new AuraVector3(0f, 4f, 0f)));

            for (var tick = 1; tick <= 240; tick++)
                world.Step(new SimulationStep(new SimulationTick((uint)tick), 1f / 60f));

            Assert.IsTrue(world.TryGetBodyState(bodyA, out var a));
            Assert.IsTrue(world.TryGetBodyState(bodyB, out var b));
            Assert.Less(System.Math.Abs(AuraVector3.Distance(a.Pose.Position, b.Pose.Position) - 2f), 0.4f);
        }

        [Test]
        public void NativeBackend_SliderJoint_ConstrainsPerpendicularAxes()
        {
            using var world = CreateWorld(out _);
            var entityA = world.CreateEntity();
            var bodyA = world.AttachBody(entityA, AuraPhysicsBodyDefinition.CreateDynamic(
                new AuraPose(new AuraVector3(-1f, 4f, 0f), AuraQuaternion.Identity),
                AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All, AuraPhysicsShapeDefinition.Box(new AuraVector3(0.5f, 0.5f, 0.5f))));
            var entityB = world.CreateEntity();
            var bodyB = world.AttachBody(entityB, AuraPhysicsBodyDefinition.CreateDynamic(
                new AuraPose(new AuraVector3(1f, 4f, 0f), AuraQuaternion.Identity),
                AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All, AuraPhysicsShapeDefinition.Box(new AuraVector3(0.5f, 0.5f, 0.5f))));

            world.CreateJoint(entityA, entityB, AuraJointDefinition.CreateSlider(
                bodyA, bodyB, new AuraVector3(0f, 4f, 0f), new AuraVector3(0f, 4f, 0f),
                AuraVector3.UnitX, AuraVector3.UnitX));

            for (var tick = 1; tick <= 240; tick++)
                world.Step(new SimulationStep(new SimulationTick((uint)tick), 1f / 60f));

            Assert.IsTrue(world.TryGetBodyState(bodyA, out var a));
            Assert.IsTrue(world.TryGetBodyState(bodyB, out var b));
            Assert.Less(System.Math.Abs(a.Pose.Position.Y - b.Pose.Position.Y), 0.15f);
        }

        [Test]
        public void NativeBackend_Joint_Destroy_RemovesJoint()
        {
            using var world = CreateWorld(out _);
            var entityA = world.CreateEntity();
            var bodyA = world.AttachBody(entityA, AuraPhysicsBodyDefinition.CreateDynamic(
                new AuraPose(new AuraVector3(-1f, 4f, 0f), AuraQuaternion.Identity),
                AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All, AuraPhysicsShapeDefinition.Box(new AuraVector3(0.5f, 0.5f, 0.5f))));
            var entityB = world.CreateEntity();
            var bodyB = world.AttachBody(entityB, AuraPhysicsBodyDefinition.CreateDynamic(
                new AuraPose(new AuraVector3(1f, 4f, 0f), AuraQuaternion.Identity),
                AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All, AuraPhysicsShapeDefinition.Box(new AuraVector3(0.5f, 0.5f, 0.5f))));

            var joint = world.CreateJoint(entityA, entityB, AuraJointDefinition.CreateDistance(
                bodyA, bodyB, new AuraVector3(-1f, 4f, 0f), new AuraVector3(1f, 4f, 0f), 2f));
            Assert.IsTrue(world.HasJoint(joint));
            Assert.AreEqual(AuraResult.Success, world.DestroyJoint(joint));
            Assert.IsFalse(world.HasJoint(joint));
        }

        [Test]
        public void NativeBackend_ConveyorSurfaceVelocity_DragsBody()
        {
            using var world = CreateWorld(out _);
            var groundEntity = world.CreateEntity();
            world.AttachBody(
                groundEntity,
                AuraPhysicsBodyDefinition.CreateStatic(
                    new AuraPose(new AuraVector3(0f, -0.5f, 0f), AuraQuaternion.Identity),
                    AuraPhysicsLayer.Default,
                    AuraPhysicsLayerMask.All,
                    AuraPhysicsShapeDefinition.Box(new AuraVector3(20f, 0.5f, 20f))));
            Assert.AreEqual(AuraResult.Success, world.SetSurfaceVelocity(groundEntity, new AuraVector3(3f, 0f, 0f)));

            var body = world.AttachBody(
                world.CreateEntity(),
                AuraPhysicsBodyDefinition.CreateDynamic(
                    new AuraPose(new AuraVector3(0f, 0.6f, 0f), AuraQuaternion.Identity),
                    AuraPhysicsLayer.Default,
                    AuraPhysicsLayerMask.All,
                    AuraPhysicsShapeDefinition.Box(new AuraVector3(0.5f, 0.5f, 0.5f))));

            for (var tick = 1; tick <= 300; tick++)
                world.Step(new SimulationStep(new SimulationTick((uint)tick), 1f / 60f));

            Assert.IsTrue(world.TryGetBodyState(body, out var state));
            Assert.Greater(state.Pose.Position.X, 1f, "a conveyor surface velocity should drag the resting box in +X.");
        }

        [Test]
        public void NativeBackend_ScaledBox_RestsAtScaledHeight()
        {
            using var world = CreateWorld(out _);
            world.AttachBody(
                world.CreateEntity(),
                AuraPhysicsBodyDefinition.CreateStatic(
                    new AuraPose(new AuraVector3(0f, -0.5f, 0f), AuraQuaternion.Identity),
                    AuraPhysicsLayer.Default,
                    AuraPhysicsLayerMask.All,
                    AuraPhysicsShapeDefinition.Box(new AuraVector3(10f, 0.5f, 10f))));

            var scaled = new AuraPhysicsShapeDefinition(
                AuraShapeType.Box,
                AuraPose.Identity,
                false,
                AuraPhysicsMaterialDefinition.Default,
                AuraPhysicsLayer.Default,
                AuraShapeGeometry.Box(new AuraVector3(0.5f, 0.5f, 0.5f)).Scaled(new AuraVector3(2f, 2f, 2f)));

            var body = world.AttachBody(
                world.CreateEntity(),
                AuraPhysicsBodyDefinition.CreateDynamic(
                    new AuraPose(new AuraVector3(0f, 3f, 0f), AuraQuaternion.Identity),
                    AuraPhysicsLayer.Default,
                    AuraPhysicsLayerMask.All,
                    scaled));

            for (var tick = 1; tick <= 300; tick++)
                world.Step(new SimulationStep(new SimulationTick((uint)tick), 1f / 60f));

            Assert.IsTrue(world.TryGetBodyState(body, out var state));
            Assert.AreEqual(1f, state.Pose.Position.Y, 0.25f, "a box scaled to half-height 1 should rest at y = 1.");
        }

        [Test]
        public void NativeBackend_SaveState_RestoresBodyState()
        {
            using var world = CreateWorld(out _);
            var body = world.AttachBody(
                world.CreateEntity(),
                AuraPhysicsBodyDefinition.CreateDynamic(
                    new AuraPose(new AuraVector3(0f, 5f, 0f), AuraQuaternion.Identity),
                    AuraPhysicsLayer.Default,
                    AuraPhysicsLayerMask.All,
                    AuraPhysicsShapeDefinition.Sphere(0.5f)));

            for (var tick = 1; tick <= 60; tick++)
                world.Step(new SimulationStep(new SimulationTick((uint)tick), 1f / 60f));

            Assert.IsTrue(world.TryGetBodyState(body, out var before));
            var saved = world.SaveState();
            Assert.Greater(saved.Length, 0);

            for (var tick = 61; tick <= 180; tick++)
                world.Step(new SimulationStep(new SimulationTick((uint)tick), 1f / 60f));

            Assert.IsTrue(world.TryGetBodyState(body, out var fallen));
            Assert.Less(fallen.Pose.Position.Y, before.Pose.Position.Y - 0.5f);

            world.RestoreState(saved);

            Assert.IsTrue(world.TryGetBodyState(body, out var restored));
            Assert.AreEqual(before.Pose.Position.X, restored.Pose.Position.X, 1e-3f);
            Assert.AreEqual(before.Pose.Position.Y, restored.Pose.Position.Y, 1e-3f);
            Assert.AreEqual(before.Pose.Position.Z, restored.Pose.Position.Z, 1e-3f);
        }

        [Test]
        public void NativeBackend_Character_FallsAndGetsGrounded()
        {
            using var world = CreateWorld(out _);
            world.AttachBody(
                world.CreateEntity(),
                AuraPhysicsBodyDefinition.CreateStatic(
                    new AuraPose(new AuraVector3(0f, -0.5f, 0f), AuraQuaternion.Identity),
                    AuraPhysicsLayer.Default,
                    AuraPhysicsLayerMask.All,
                    AuraPhysicsShapeDefinition.Box(new AuraVector3(10f, 0.5f, 10f))));

            var character = world.CreateCharacter(new AuraCharacterDefinition(
                new AuraPose(new AuraVector3(0f, 3f, 0f), AuraQuaternion.Identity),
                0.4f,
                1.8f));
            Assert.IsTrue(character.IsValid);

            for (var tick = 1; tick <= 180; tick++)
            {
                world.MoveCharacter(character, AuraVector3.Zero, 1f / 60f);
                world.Step(new SimulationStep(new SimulationTick((uint)tick), 1f / 60f));
            }

            Assert.IsTrue(world.TryGetCharacterState(character, out var state));
            Assert.IsTrue(state.IsGrounded, $"grounded={state.IsGrounded} y={state.Position.Y} vy={state.Velocity.Y}");
            Assert.Less(System.Math.Abs(state.Position.Y - 0.9f), 0.25f);
        }

        [Test]
        public void NativeBackend_Character_MovesHorizontally()
        {
            using var world = CreateWorld(out _);
            world.AttachBody(
                world.CreateEntity(),
                AuraPhysicsBodyDefinition.CreateStatic(
                    new AuraPose(new AuraVector3(0f, -0.5f, 0f), AuraQuaternion.Identity),
                    AuraPhysicsLayer.Default,
                    AuraPhysicsLayerMask.All,
                    AuraPhysicsShapeDefinition.Box(new AuraVector3(20f, 0.5f, 20f))));

            var character = world.CreateCharacter(new AuraCharacterDefinition(
                new AuraPose(new AuraVector3(0f, 0.5f, 0f), AuraQuaternion.Identity),
                0.4f,
                1.8f));

            for (var tick = 1; tick <= 180; tick++)
            {
                world.MoveCharacter(character, AuraVector3.Zero, 1f / 60f);
                world.Step(new SimulationStep(new SimulationTick((uint)tick), 1f / 60f));
            }

            for (var tick = 181; tick <= 240; tick++)
            {
                world.MoveCharacter(character, new AuraVector3(0.05f, 0f, 0f), 1f / 60f);
                world.Step(new SimulationStep(new SimulationTick((uint)tick), 1f / 60f));
            }

            Assert.IsTrue(world.TryGetCharacterState(character, out var state));
            Assert.Greater(state.Position.X, 1.5f, "character should walk in +X.");
            Assert.Less(System.Math.Abs(state.Position.Z), 0.2f);
        }

        [Test]
        public void NativeBackend_Contacts_ReportGroundNormal()
        {
            using var world = CreateWorld(out _);
            world.AttachBody(
                world.CreateEntity(),
                AuraPhysicsBodyDefinition.CreateStatic(
                    new AuraPose(new AuraVector3(0f, -0.5f, 0f), AuraQuaternion.Identity),
                    AuraPhysicsLayer.Default,
                    AuraPhysicsLayerMask.All,
                    AuraPhysicsShapeDefinition.Box(new AuraVector3(10f, 0.5f, 10f))));

            world.AttachBody(
                world.CreateEntity(),
                AuraPhysicsBodyDefinition.CreateDynamic(
                    new AuraPose(new AuraVector3(0f, 3f, 0f), AuraQuaternion.Identity),
                    AuraPhysicsLayer.Default,
                    AuraPhysicsLayerMask.All,
                    AuraPhysicsShapeDefinition.Sphere(0.5f)));

            for (var tick = 1; tick <= 300; tick++)
                world.Step(new SimulationStep(new SimulationTick((uint)tick), 1f / 60f));

            var buffer = new AuraContact[16];
            var count = world.CopyContacts(buffer);
            Assert.Greater(count, 0, "expected at least one contact while the ball rests on the ground.");

            var vertical = false;
            for (var index = 0; index < count; index++)
            {
                if (System.Math.Abs(buffer[index].Normal.Y) > 0.8f)
                    vertical = true;
            }

            Assert.IsTrue(vertical, "a resting contact should have an approximately vertical normal.");
        }

        [Test]
        public void NativeBackend_2D_DistanceJoint_KeepsDistance()
        {
            var backend = new NativePhysicsBackend();
            using var world = new AuraSimulationWorld(backend, new AuraWorldDefinition(AuraPhysicsMode.Plane2D, default, null, 8));

            var entityA = world.CreateEntity();
            var bodyA = world.AttachBody(entityA, AuraPhysicsBodyDefinition.CreateDynamic(
                new AuraPose(new AuraVector3(-1f, 4f, 0f), AuraQuaternion.Identity),
                AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All, AuraPhysicsShapeDefinition.Box(new AuraVector3(0.5f, 0.5f, 0.5f))));
            var entityB = world.CreateEntity();
            var bodyB = world.AttachBody(entityB, AuraPhysicsBodyDefinition.CreateDynamic(
                new AuraPose(new AuraVector3(1f, 4f, 0f), AuraQuaternion.Identity),
                AuraPhysicsLayer.Default, AuraPhysicsLayerMask.All, AuraPhysicsShapeDefinition.Box(new AuraVector3(0.5f, 0.5f, 0.5f))));

            world.CreateJoint(entityA, entityB, AuraJointDefinition.CreateDistance(
                bodyA, bodyB, new AuraVector3(-1f, 4f, 0f), new AuraVector3(1f, 4f, 0f), 2f));

            for (var tick = 1; tick <= 240; tick++)
                world.Step(new SimulationStep(new SimulationTick((uint)tick), 1f / 60f));

            Assert.IsTrue(world.TryGetBodyState(bodyA, out var a));
            Assert.IsTrue(world.TryGetBodyState(bodyB, out var b));
            Assert.Less(System.Math.Abs(AuraVector3.Distance(a.Pose.Position, b.Pose.Position) - 2f), 0.5f);
        }
    }
}
