using System;
using AuraEngine.Core;
using AuraEngine.Physics;
using AuraEngine.Simulation;
using NUnit.Framework;

namespace AuraEngine.Tests
{
    public sealed class ManagedMeshShapeTests
    {
        private static AuraSimulationWorld CreateWorld(out ManagedPhysicsBackend backend)
        {
            backend = new ManagedPhysicsBackend();
            return new AuraSimulationWorld(backend, new AuraWorldDefinition(initialBodyCapacity: 8));
        }

        private static void Step(AuraSimulationWorld world, int count)
        {
            for (var index = 1; index <= count; index++)
                world.Step(new SimulationStep(new SimulationTick((uint)index), 1f / 60f));
        }

        private static AuraPhysicsShapeDefinition FlatMesh(float y, float half)
        {
            var vertices = new[]
            {
                new AuraVector3(-half, y, -half),
                new AuraVector3(half, y, -half),
                new AuraVector3(half, y, half),
                new AuraVector3(-half, y, half),
            };
            var indices = new[] { 0, 1, 2, 0, 2, 3 };
            return new AuraPhysicsShapeDefinition(
                AuraShapeType.TriangleMesh,
                AuraPose.Identity,
                false,
                AuraPhysicsMaterialDefinition.Default,
                AuraPhysicsLayer.Default,
                AuraShapeGeometry.TriangleMesh(vertices, indices));
        }

        private static AuraPhysicsShapeDefinition FlatHeightField(float y, float half, int resolution)
        {
            var samples = new float[resolution * resolution];
            for (var index = 0; index < samples.Length; index++)
                samples[index] = y;

            var step = (half * 2f) / (resolution - 1);
            return AuraPhysicsShapeDefinition.HeightField(samples, resolution, new AuraVector3(step, 1f, step));
        }

        private static AuraPhysicsShapeDefinition Sphere(float radius) =>
            AuraPhysicsShapeDefinition.Sphere(radius);

        [Test]
        public void TriangleMesh_StaticFloorStopsFallingSphere()
        {
            using var world = CreateWorld(out _);
            var floor = world.AttachBody(
                world.CreateEntity(),
                AuraPhysicsBodyDefinition.CreateStatic(
                    new AuraPose(new AuraVector3(0f, 0f, 0f), AuraQuaternion.Identity),
                    AuraPhysicsLayer.Default,
                    AuraPhysicsLayerMask.All,
                    FlatMesh(0f, 5f)));

            Assert.IsTrue(floor.IsValid, "the managed backend should accept a static triangle mesh.");

            var sphere = world.AttachBody(
                world.CreateEntity(),
                AuraPhysicsBodyDefinition.CreateDynamic(
                    new AuraPose(new AuraVector3(0f, 4f, 0f), AuraQuaternion.Identity),
                    AuraPhysicsLayer.Default,
                    AuraPhysicsLayerMask.All,
                    Sphere(0.5f)));

            Step(world, 300);

            Assert.IsTrue(world.TryGetBodyState(sphere, out var state));
            Assert.AreEqual(0.5f, state.Pose.Position.Y, 0.2f, $"sphere should rest on the mesh, y={state.Pose.Position.Y}.");
        }

        [Test]
        public void HeightField_StaticFloorStopsFallingSphere()
        {
            using var world = CreateWorld(out _);
            var field = world.AttachBody(
                world.CreateEntity(),
                AuraPhysicsBodyDefinition.CreateStatic(
                    new AuraPose(new AuraVector3(0f, 0f, 0f), AuraQuaternion.Identity),
                    AuraPhysicsLayer.Default,
                    AuraPhysicsLayerMask.All,
                    FlatHeightField(0f, 5f, 5)));

            Assert.IsTrue(field.IsValid, "the managed backend should accept a static height field.");

            var sphere = world.AttachBody(
                world.CreateEntity(),
                AuraPhysicsBodyDefinition.CreateDynamic(
                    new AuraPose(new AuraVector3(2f, 4f, 2f), AuraQuaternion.Identity),
                    AuraPhysicsLayer.Default,
                    AuraPhysicsLayerMask.All,
                    Sphere(0.5f)));

            Step(world, 300);

            Assert.IsTrue(world.TryGetBodyState(sphere, out var state));
            Assert.AreEqual(0.5f, state.Pose.Position.Y, 0.2f, $"sphere should rest on the height field, y={state.Pose.Position.Y}.");
        }

        [Test]
        public void TriangleMesh_RaycastHitsFloor()
        {
            using var world = CreateWorld(out _);
            world.AttachBody(
                world.CreateEntity(),
                AuraPhysicsBodyDefinition.CreateStatic(
                    new AuraPose(new AuraVector3(0f, 0f, 0f), AuraQuaternion.Identity),
                    AuraPhysicsLayer.Default,
                    AuraPhysicsLayerMask.All,
                    FlatMesh(0f, 5f)));

            var hit = world.Raycast(
                new AuraRay(new AuraVector3(0f, 5f, 0f), new AuraVector3(0f, -1f, 0f)),
                100f,
                AuraPhysicsQueryFilter.All,
                out var result);

            Assert.IsTrue(hit, "raycast should hit the triangle-mesh floor.");
            Assert.AreEqual(5f, result.Distance, 0.1f, $"hit distance={result.Distance}.");
        }

        [Test]
        public void HeightField_RaycastHitsFloor()
        {
            using var world = CreateWorld(out _);
            world.AttachBody(
                world.CreateEntity(),
                AuraPhysicsBodyDefinition.CreateStatic(
                    new AuraPose(new AuraVector3(0f, 0f, 0f), AuraQuaternion.Identity),
                    AuraPhysicsLayer.Default,
                    AuraPhysicsLayerMask.All,
                    FlatHeightField(0f, 4f, 5)));

            var hit = world.Raycast(
                new AuraRay(new AuraVector3(2f, 5f, 2f), new AuraVector3(0f, -1f, 0f)),
                100f,
                AuraPhysicsQueryFilter.All,
                out var result);

            Assert.IsTrue(hit, "raycast should hit the height-field floor.");
            Assert.AreEqual(5f, result.Distance, 0.2f, $"hit distance={result.Distance}.");
        }
    }
}
