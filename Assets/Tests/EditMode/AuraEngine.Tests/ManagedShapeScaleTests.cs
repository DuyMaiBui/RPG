using AuraEngine.Core;
using AuraEngine.Physics;
using AuraEngine.Simulation;
using NUnit.Framework;

namespace AuraEngine.Tests
{
    public sealed class ManagedShapeScaleTests
    {
        [Test]
        public void Scaled_Sphere_MultipliesRadius()
        {
            var geometry = AuraShapeGeometry.Sphere(0.5f).Scaled(new AuraVector3(2f, 2f, 2f));
            Assert.AreEqual(1f, geometry.Radius, 1e-5f);
        }

        [Test]
        public void Scaled_Box_MultipliesHalfExtents()
        {
            var geometry = AuraShapeGeometry.Box(new AuraVector3(0.5f, 1f, 2f)).Scaled(new AuraVector3(2f, 3f, 4f));
            Assert.AreEqual(1f, geometry.HalfExtents.X, 1e-5f);
            Assert.AreEqual(3f, geometry.HalfExtents.Y, 1e-5f);
            Assert.AreEqual(8f, geometry.HalfExtents.Z, 1e-5f);
        }

        [Test]
        public void Scaled_Mesh_ScalesVertices()
        {
            var vertices = new[]
            {
                new AuraVector3(1f, 0f, 0f),
                new AuraVector3(0f, 1f, 0f),
                new AuraVector3(0f, 0f, 1f),
                new AuraVector3(0f, 0f, 0f),
            };
            var geometry = AuraShapeGeometry.ConvexMesh(vertices).Scaled(new AuraVector3(2f, 2f, 2f));
            Assert.AreEqual(2f, geometry.MeshVertices[0].X, 1e-5f);
            Assert.AreEqual(2f, geometry.MeshVertices[1].Y, 1e-5f);
            Assert.AreEqual(2f, geometry.MeshVertices[2].Z, 1e-5f);
            Assert.AreEqual(1f, vertices[0].X, 1e-5f, "the source vertex array must not be mutated.");
        }

        [Test]
        public void Scaled_Box_RestsAtScaledHeight()
        {
            var backend = new ManagedPhysicsBackend();
            using var world = new AuraSimulationWorld(backend, new AuraWorldDefinition(AuraPhysicsMode.Full3D, default, null, 8));

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
            Assert.AreEqual(1f, state.Pose.Position.Y, 0.2f, "a box scaled to half-height 1 should rest at y = 1.");
        }
    }
}
