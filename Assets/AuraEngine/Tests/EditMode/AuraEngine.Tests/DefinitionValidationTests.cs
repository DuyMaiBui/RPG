using AuraEngine.Core;
using AuraEngine.Serialization;
using AuraEngine.Simulation;
using NUnit.Framework;

namespace AuraEngine.Tests
{
    public sealed class DefinitionValidationTests
    {
        [Test]
        public void ShapeGeometry_RejectsInvalidValues()
        {
            Assert.AreNotEqual(AuraResult.Success, AuraShapeGeometry.Box(new AuraVector3(-1f, 1f, 1f)).Validate(AuraShapeType.Box));
            Assert.AreNotEqual(AuraResult.Success, AuraShapeGeometry.Sphere(0f).Validate(AuraShapeType.Sphere));
            Assert.AreNotEqual(AuraResult.Success, AuraShapeGeometry.Capsule(1f, 0f).Validate(AuraShapeType.Capsule));
            Assert.AreNotEqual(AuraResult.Success, AuraShapeGeometry.ConvexMesh(System.Array.Empty<AuraVector3>()).Validate(AuraShapeType.ConvexMesh));
            Assert.AreEqual(AuraResult.Success, AuraShapeGeometry.Sphere(1f).Validate(AuraShapeType.Sphere));
        }

        [Test]
        public void BodyDefinition_DynamicRequiresPositiveMass()
        {
            var definition = AuraPhysicsBodyDefinition.CreateDynamic(
                AuraPose.Identity,
                AuraPhysicsLayer.Default,
                AuraPhysicsLayerMask.All,
                AuraPhysicsShapeDefinition.Sphere(1f));

            Assert.IsTrue(definition.IsValid);
            Assert.AreEqual(AuraResult.Success, definition.Validate());
        }

        [Test]
        public void BodyDefinition_WithoutShapes_IsInvalid()
        {
            var definition = AuraPhysicsBodyDefinition.CreateStatic(
                AuraPose.Identity,
                AuraPhysicsLayer.Default,
                AuraPhysicsLayerMask.All);

            Assert.AreNotEqual(AuraResult.Success, definition.Validate());
        }

        [Test]
        public void LayerMask_FromLayersEmpty_IsNone()
        {
            Assert.AreEqual(AuraPhysicsLayerMask.None, AuraPhysicsLayerMask.FromLayers());
            Assert.AreEqual(AuraPhysicsLayerMask.None, AuraPhysicsLayerMask.FromLayers(null));
        }

        [Test]
        public void SnapshotCapture_OrdersStatesByEntityId()
        {
            var backend = new FakePhysicsBackend();
            using var world = new AuraSimulationWorld(backend, new AuraWorldDefinition(initialBodyCapacity: 4));
            var e0 = world.CreateEntity();
            var e1 = world.CreateEntity();
            var e2 = world.CreateEntity();

            world.AttachBody(e2, Body(2f));
            world.AttachBody(e1, Body(1f));
            world.AttachBody(e0, Body(0f));

            var snapshot = AuraSimulationSnapshot.Capture(world);

            Assert.AreEqual(3, snapshot.BodyStates.Length);
            Assert.AreEqual(e0, snapshot.BodyStates[0].Entity);
            Assert.AreEqual(e1, snapshot.BodyStates[1].Entity);
            Assert.AreEqual(e2, snapshot.BodyStates[2].Entity);
        }

        [Test]
        public void StateHash_IsIndependentOfBodyInsertionOrder()
        {
            var backendA = new FakePhysicsBackend();
            using var worldA = new AuraSimulationWorld(backendA, new AuraWorldDefinition(initialBodyCapacity: 4));
            var a0 = worldA.CreateEntity();
            var a1 = worldA.CreateEntity();
            var a2 = worldA.CreateEntity();
            worldA.AttachBody(a0, Body(0f));
            worldA.AttachBody(a1, Body(1f));
            worldA.AttachBody(a2, Body(2f));

            var backendB = new FakePhysicsBackend();
            using var worldB = new AuraSimulationWorld(backendB, new AuraWorldDefinition(initialBodyCapacity: 4));
            var b0 = worldB.CreateEntity();
            var b1 = worldB.CreateEntity();
            var b2 = worldB.CreateEntity();
            worldB.AttachBody(b2, Body(2f));
            worldB.AttachBody(b1, Body(1f));
            worldB.AttachBody(b0, Body(0f));

            var step = new SimulationStep(new SimulationTick(1), 1f / 60f);
            worldA.Step(step);
            worldB.Step(step);

            Assert.AreEqual(worldA.ComputeStateHash(), worldB.ComputeStateHash());
        }

        private static AuraPhysicsBodyDefinition Body(float x) =>
            AuraPhysicsBodyDefinition.CreateStatic(
                new AuraPose(new AuraVector3(x, 0f, 0f), AuraQuaternion.Identity),
                AuraPhysicsLayer.Default,
                AuraPhysicsLayerMask.All,
                AuraPhysicsShapeDefinition.Sphere(1f));
    }
}
