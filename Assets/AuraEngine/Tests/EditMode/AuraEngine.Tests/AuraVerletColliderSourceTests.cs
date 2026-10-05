using AuraEngine.Core;
using AuraEngine.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AuraEngine.Tests
{
    /* P0.3 AuraVerletColliderSource: bindings from every supported collider authoring kind, trigger exclusion and the
       warning for shapes Verlet collision cannot represent. Needs no physics world. */
    public sealed class AuraVerletColliderSourceTests
    {
        private GameObject _root;

        [SetUp]
        public void SetUp() => _root = new GameObject("VerletRoot");

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
                Object.DestroyImmediate(_root);
        }

        [Test]
        public void Refresh_BuildsABindingFromEachSupportedAuthoringKind()
        {
            var box = Add<AuraBoxColliderAuthoring>("Box", new Vector3(1f, 2f, 3f));
            AuraSerializedFields.SetVector3(box, "_size", new Vector3(2f, 4f, 6f));
            var sphere = Add<AuraSphereColliderAuthoring>("Sphere", new Vector3(0f, 5f, 0f));
            AuraSerializedFields.SetFloat(sphere, "_radius", 0.75f);
            var capsule = Add<AuraCapsuleColliderAuthoring>("Capsule", new Vector3(0f, 0f, 7f));
            AuraSerializedFields.SetFloat(capsule, "_radius", 0.5f);
            AuraSerializedFields.SetFloat(capsule, "_height", 2f);
            var plane = Add<AuraPlaneColliderAuthoring>("Plane", new Vector3(0f, -1f, 0f));
            var box2D = Add<AuraBoxCollider2DAuthoring>("Box2D", new Vector3(4f, 4f, 0f));
            AuraSerializedFields.SetVector2(box2D, "_size", new Vector2(2f, 6f));
            var circle2D = Add<AuraCircleCollider2DAuthoring>("Circle2D", new Vector3(5f, 0f, 0f));
            AuraSerializedFields.SetFloat(circle2D, "_radius", 0.25f);
            var capsule2D = Add<AuraCapsuleCollider2DAuthoring>("Capsule2D", new Vector3(6f, 0f, 0f));

            var source = _root.AddComponent<AuraVerletColliderSource>();
            AuraSerializedFields.SetReferences(source, "_colliders3D", new Object[] { box, sphere, capsule, plane });
            AuraSerializedFields.SetReferences(source, "_colliders2D", new Object[] { box2D, circle2D, capsule2D });

            var colliders = source.Refresh();

            Assert.AreEqual(7, colliders.Count, "One binding per non-trigger, supported collider authoring.");
            Assert.AreEqual(AuraVerletColliderKind.Box, colliders[0].Kind);
            AssertNear(new Vector3(1f, 2f, 3f), colliders[0].Position);
            AssertNear(new Vector3(1f, 2f, 3f), colliders[0].HalfExtents);
            Assert.IsFalse(colliders[0].Planar);
            Assert.AreEqual(AuraVerletColliderKind.Sphere, colliders[1].Kind);
            Assert.AreEqual(0.75f, colliders[1].Radius, 1e-4f);
            Assert.AreEqual(AuraVerletColliderKind.Capsule, colliders[2].Kind);
            Assert.AreEqual(0.5f, colliders[2].Radius, 1e-4f);
            Assert.AreEqual(0.5f, colliders[2].HalfSegment, 1e-4f, "Capsule core half segment is height / 2 - radius.");
            Assert.AreEqual(AuraVerletColliderKind.Plane, colliders[3].Kind);
            AssertNear(Vector3.up, colliders[3].Normal);
            Assert.AreEqual(AuraVerletColliderKind.Box, colliders[4].Kind);
            Assert.IsTrue(colliders[4].Planar, "2D authoring produces planar colliders.");
            AssertNear(new Vector3(1f, 3f, 0.5f), colliders[4].HalfExtents);
            Assert.AreEqual(AuraVerletColliderKind.Sphere, colliders[5].Kind);
            Assert.IsTrue(colliders[5].Planar);
            Assert.AreEqual(0.25f, colliders[5].Radius, 1e-4f);
            Assert.AreEqual(AuraVerletColliderKind.Capsule, colliders[6].Kind);
            Assert.IsTrue(colliders[6].Planar);
        }

        [Test]
        public void Refresh_ExcludesTriggerColliders()
        {
            var solid = Add<AuraBoxColliderAuthoring>("Solid", Vector3.zero);
            var trigger = Add<AuraSphereColliderAuthoring>("Trigger", Vector3.one);
            AuraSerializedFields.SetBool(trigger, "_isTrigger", true);
            var trigger2D = Add<AuraBoxCollider2DAuthoring>("Trigger2D", Vector3.one);
            AuraSerializedFields.SetBool(trigger2D, "_isTrigger", true);
            var source = _root.AddComponent<AuraVerletColliderSource>();
            AuraSerializedFields.SetReferences(source, "_colliders3D", new Object[] { solid, trigger });
            AuraSerializedFields.SetReferences(source, "_colliders2D", new Object[] { trigger2D });

            var colliders = source.Refresh();

            Assert.AreEqual(1, colliders.Count, "Trigger colliders must not push cloth or hair particles.");
            Assert.AreEqual(AuraVerletColliderKind.Box, colliders[0].Kind);
        }

        [Test]
        public void Refresh_SkipsShapesVerletCannotRepresent_WithAWarning()
        {
            var cylinder = Add<AuraCylinderColliderAuthoring>("Cylinder", Vector3.zero);
            var source = _root.AddComponent<AuraVerletColliderSource>();
            AuraSerializedFields.SetReferences(source, "_colliders3D", new Object[] { cylinder });

            LogAssert.Expect(
                LogType.Warning,
                "[AuraVerletColliderSource] Shape type Cylinder on 'Cylinder' is not supported by Verlet collision (sphere, capsule, box, plane only).");
            var colliders = source.Refresh();

            Assert.AreEqual(0, colliders.Count);
        }

        [Test]
        public void Refresh_MissingColliderReference_LogsAnAuthoringErrorAndKeepsTheOthers()
        {
            var box = Add<AuraBoxColliderAuthoring>("Box", Vector3.zero);
            var source = _root.AddComponent<AuraVerletColliderSource>();
            AuraSerializedFields.SetReferences(source, "_colliders3D", new Object[] { null, box });

            LogAssert.Expect(
                LogType.Error,
                "[AuraVerletColliderSource] 3D collider reference at index 0 is missing on 'VerletRoot'. Author the reference in the scene/prefab.");
            var colliders = source.Refresh();

            Assert.AreEqual(1, colliders.Count);
        }

        private T Add<T>(string name, Vector3 position) where T : Component
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform, false);
            go.transform.localPosition = position;
            return go.AddComponent<T>();
        }

        private static void AssertNear(Vector3 expected, AuraVector3 actual)
        {
            Assert.Less(Vector3.Distance(expected, actual.ToUnity()), 1e-3f, $"Expected {expected}, got {actual.ToUnity()}.");
        }
    }
}
