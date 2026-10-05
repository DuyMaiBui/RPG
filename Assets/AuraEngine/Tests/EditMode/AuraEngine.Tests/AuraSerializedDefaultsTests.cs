using AuraEngine.Core;
using AuraEngine.Unity;
using NUnit.Framework;
using UnityEngine;

namespace AuraEngine.Tests
{
    /* P0.3 serialization: values written through SerializedObject (the path scenes and prefabs use) must reach the
       definition the authoring builds, and untouched fields must keep their documented defaults. */
    public sealed class AuraSerializedDefaultsTests
    {
        private GameObject _go;

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
                Object.DestroyImmediate(_go);
        }

        [Test]
        public void Body3D_UntouchedFields_BuildTheDocumentedDefaults()
        {
            var body = NewBody3D();

            Assert.IsTrue(body.TryBuildDefinition(out var definition, out var result), $"Default authoring must be valid ({result}).");

            Assert.AreEqual(AuraBodyType.Dynamic, definition.Type);
            Assert.AreEqual(1f, definition.Mass);
            Assert.AreEqual(1f, definition.GravityScale);
            Assert.AreEqual(0f, definition.LinearDamping);
            Assert.AreEqual(AuraBodyFreezeFlags.None, definition.Freeze);
            Assert.IsTrue(definition.AllowSleeping);
            Assert.AreEqual(1, definition.Shapes.Length);
            Assert.AreEqual(AuraShapeType.Box, definition.Shapes[0].Type);
            Assert.IsFalse(definition.Shapes[0].IsTrigger);
            Assert.AreEqual(0.5f, definition.Shapes[0].Geometry.HalfExtents.X, 1e-5f, "Default box size is 1 m, so the half extent is 0.5.");
        }

        [Test]
        public void Body3D_SerializedValues_ReachTheBodyAndShapeDefinition()
        {
            var body = NewBody3D();
            var collider = _go.GetComponent<AuraBoxColliderAuthoring>();
            AuraSerializedFields.SetEnum(body, "_type", AuraBodyType.Kinematic);
            AuraSerializedFields.SetFloat(body, "_mass", 5f);
            AuraSerializedFields.SetFloat(body, "_gravityScale", 0.25f);
            AuraSerializedFields.SetFloat(body, "_linearDamping", 0.4f);
            AuraSerializedFields.SetBool(body, "_freezePosX", true);
            AuraSerializedFields.SetBool(body, "_freezeRotZ", true);
            AuraSerializedFields.SetBool(body, "_allowSleeping", false);
            AuraSerializedFields.SetVector3(collider, "_size", new Vector3(2f, 4f, 6f));
            AuraSerializedFields.SetVector3(collider, "_center", new Vector3(0f, 1f, 0f));
            AuraSerializedFields.SetBool(collider, "_isTrigger", true);

            Assert.IsTrue(body.TryBuildDefinition(out var definition, out var result), $"Authoring must be valid ({result}).");

            Assert.AreEqual(AuraBodyType.Kinematic, definition.Type);
            Assert.AreEqual(5f, definition.Mass);
            Assert.AreEqual(0.25f, definition.GravityScale);
            Assert.AreEqual(0.4f, definition.LinearDamping, 1e-6f);
            Assert.AreEqual(AuraBodyFreezeFlags.PositionX | AuraBodyFreezeFlags.RotationZ, definition.Freeze);
            Assert.IsFalse(definition.AllowSleeping);
            Assert.IsTrue(definition.Shapes[0].IsTrigger);
            Assert.AreEqual(3f, definition.Shapes[0].Geometry.HalfExtents.Z, 1e-5f);
            Assert.AreEqual(1f, definition.Shapes[0].LocalPose.Position.Y, 1e-5f);
        }

        [Test]
        public void Body2D_SerializedValues_ReachTheBodyDefinitionAndKeepThePlanarFreeze()
        {
            _go = new GameObject("Body2D");
            _go.AddComponent<AuraBoxCollider2DAuthoring>();
            var body = _go.AddComponent<AuraPhysicsBody2DAuthoring>();
            AuraSerializedFields.SetFloat(body, "_mass", 3f);
            AuraSerializedFields.SetBool(body, "_freezeRotation", true);

            Assert.AreEqual(AuraResult.Success, body.TryBuildDefinition(out var definition));

            Assert.AreEqual(3f, definition.Mass);
            Assert.AreEqual(
                AuraBodyFreezeFlags.PositionZ | AuraBodyFreezeFlags.RotationX | AuraBodyFreezeFlags.RotationY | AuraBodyFreezeFlags.RotationZ,
                definition.Freeze,
                "2D bodies always freeze Z and X/Y rotation; _freezeRotation adds Z rotation.");
        }

        [Test]
        public void Body3D_WithoutAnyCollider_DoesNotBuildADefinition()
        {
            _go = new GameObject("Empty");
            var body = _go.AddComponent<AuraPhysicsBodyAuthoring>();

            Assert.IsFalse(body.TryBuildDefinition(out _, out _), "A body needs at least one collider authoring in its hierarchy.");
        }

        private AuraPhysicsBodyAuthoring NewBody3D()
        {
            _go = new GameObject("Body");
            _go.AddComponent<AuraBoxColliderAuthoring>();
            return _go.AddComponent<AuraPhysicsBodyAuthoring>();
        }
    }
}
