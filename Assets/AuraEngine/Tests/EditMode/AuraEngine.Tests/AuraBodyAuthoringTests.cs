using AuraEngine.Core;
using AuraEngine.Unity;
using NUnit.Framework;
using UnityEngine;

namespace AuraEngine.Tests
{
    /* P0.3: body and collider authoring built through the real component path into a native world. */
    public sealed class AuraBodyAuthoringTests : AuraAuthoringFixtureBase
    {
        [TestCase(true)]
        [TestCase(false)]
        public void Body3D_WithBoxCollider_CreatesExactlyOneBodyWithTransformPose(bool worldCreatedFirst)
        {
            Harness = AuraAuthoringHarness.Create(AuraPhysicsMode.Full3D);
            var go = Harness.NewChild("Body", null, new Vector3(1f, 2f, 3f), new Vector3(10f, 20f, 30f), Vector3.one);
            var body = Harness.AddBody3D(go);

            if (worldCreatedFirst)
            {
                Harness.CreateWorld();
                Harness.Enable(body);
            }
            else
            {
                Harness.Enable(body);
                Harness.CreateWorld();
            }

            Assert.AreEqual(1, Harness.World.BodyCount, "Exactly one body must exist for one body authoring with one collider.");
            Assert.IsFalse(body.EntityId.IsNone);
            Assert.IsTrue(Harness.World.TryGetBodyState(body.EntityId, out var state));
            AssertNear(go.transform.position, state.Pose.Position, "Body position must be the transform world position.");
            AssertSameRotation(go.transform.rotation, state.Pose.Rotation, "Body rotation must be the transform world rotation.");
        }

        [Test]
        public void Body3D_UnderRotatedScaledParent_UsesWorldPoseOfTheChild()
        {
            Harness = AuraAuthoringHarness.Create(AuraPhysicsMode.Full3D);
            Harness.CreateWorld();
            var parent = Harness.NewChild("Parent", null, new Vector3(5f, -1f, 2f), new Vector3(0f, 90f, 0f), new Vector3(2f, 2f, 2f));
            var child = Harness.NewChild("Child", parent.transform, new Vector3(1f, 0.5f, 0f), new Vector3(0f, 0f, 30f), Vector3.one);
            var body = Harness.AddBody3D(child);
            Harness.Enable(body);

            Assert.IsTrue(Harness.World.TryGetBodyState(body.EntityId, out var state));
            Assert.AreEqual(1, Harness.World.BodyCount);
            AssertNear(child.transform.position, state.Pose.Position, "A scaled, rotated parent must be baked into the world position (local (1, 0.5, 0) becomes parent.TransformPoint).");
            AssertSameRotation(child.transform.rotation, state.Pose.Rotation, "Parent rotation must compose with the local rotation.");
        }

        [Test]
        public void BoxCollider3D_SizeIgnoresTransformScale_DocumentedSemantics()
        {
            // Documented semantics: collider geometry is authored in world units and is NOT multiplied by the body's
            // lossyScale, while joint anchors ARE scaled (TransformPoint). A box of size 1 on a scale-2 body is 1 m wide.
            Harness = AuraAuthoringHarness.Create(AuraPhysicsMode.Full3D);
            Harness.CreateWorld();
            var go = Harness.NewChild("Wall", null, Vector3.zero, Vector3.zero, new Vector3(2f, 2f, 2f));
            var body = Harness.AddBody3D(go, AuraBodyType.Static);
            Harness.Enable(body);
            Harness.Step(1);

            var hit = Harness.World.Raycast(
                new AuraRay(new AuraVector3(5f, 0f, 0f), new AuraVector3(-1f, 0f, 0f)),
                20f,
                AuraPhysicsQueryFilter.All,
                out var result);

            Assert.IsTrue(hit, "The ray must hit the box.");
            Assert.AreEqual(4.5f, result.Distance, 0.05f, "Box half extent is size/2 = 0.5 regardless of transform scale 2 (4.0 would mean the collider follows lossyScale).");
        }

        [Test]
        public void Body3D_DisablingRemovesTheBodyAndEnablingCreatesItAgain()
        {
            Harness = AuraAuthoringHarness.Create(AuraPhysicsMode.Full3D);
            Harness.CreateWorld();
            var body = Harness.AddBody3D(Harness.NewChild("Body", null, Vector3.up, Vector3.zero, Vector3.one));
            Harness.Enable(body);
            var first = body.EntityId;
            Assert.AreEqual(1, Harness.World.BodyCount);

            AuraAuthoringLifecycle.Disable(body);

            Assert.IsTrue(body.EntityId.IsNone, "No entity handle may remain after disable.");
            Assert.AreEqual(0, Harness.World.BodyCount, "The kernel body must be destroyed on disable.");
            Assert.IsFalse(Harness.World.TryGetBody(first, out _), "The old entity must not resolve to a body.");

            Harness.Enable(body);

            Assert.IsFalse(body.EntityId.IsNone);
            Assert.AreEqual(1, Harness.World.BodyCount, "Re-enabling must create exactly one body, not a duplicate.");
            Harness.Enable(body);
            Assert.AreEqual(1, Harness.World.BodyCount, "Enabling twice must stay idempotent.");
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Body2D_WithBoxCollider_CreatesExactlyOneBodyWithPlanarPose(bool worldCreatedFirst)
        {
            Harness = AuraAuthoringHarness.Create(AuraPhysicsMode.Plane2D);
            var go = Harness.NewChild("Body2D", null, new Vector3(1f, 2f, 5f), new Vector3(0f, 0f, 30f), Vector3.one);
            var body = Harness.AddBody2D(go);

            if (worldCreatedFirst)
            {
                Harness.CreateWorld();
                Harness.Enable(body);
            }
            else
            {
                Harness.Enable(body);
                Harness.CreateWorld();
            }

            Assert.AreEqual(1, Harness.World.BodyCount);
            Assert.IsTrue(Harness.World.TryGetBodyState(body.EntityId, out var state));
            AssertNear(new Vector3(1f, 2f, 0f), state.Pose.Position, "2D body position is the transform XY with Z flattened to 0.");
            AssertSameRotation(Quaternion.Euler(0f, 0f, 30f), state.Pose.Rotation, "2D body rotation is the transform Z angle.");
        }

        [Test]
        public void Body2D_UnderRotatedScaledParent_UsesWorldPositionAndZAngle()
        {
            Harness = AuraAuthoringHarness.Create(AuraPhysicsMode.Plane2D);
            Harness.CreateWorld();
            var parent = Harness.NewChild("Parent", null, new Vector3(4f, 1f, 0f), new Vector3(0f, 0f, 40f), new Vector3(2f, 3f, 1f));
            var child = Harness.NewChild("Child", parent.transform, new Vector3(1f, 1f, 0f), new Vector3(0f, 0f, 20f), Vector3.one);
            var body = Harness.AddBody2D(child);
            Harness.Enable(body);

            Assert.IsTrue(Harness.World.TryGetBodyState(body.EntityId, out var state));
            var world = child.transform.position;
            AssertNear(new Vector3(world.x, world.y, 0f), state.Pose.Position, "Non-uniform parent scale must be baked into the 2D world position.");
            AssertSameRotation(Quaternion.Euler(0f, 0f, 60f), state.Pose.Rotation, "Parent 40 + local 20 degrees.");
        }

        [Test]
        public void Body2D_DisablingRemovesTheBodyAndEnablingCreatesItAgain()
        {
            Harness = AuraAuthoringHarness.Create(AuraPhysicsMode.Plane2D);
            Harness.CreateWorld();
            var body = Harness.AddBody2D(Harness.NewChild("Body2D", null, Vector3.zero, Vector3.zero, Vector3.one));
            Harness.Enable(body);
            Assert.AreEqual(1, Harness.World.BodyCount);

            AuraAuthoringLifecycle.Disable(body);
            Assert.IsTrue(body.EntityId.IsNone);
            Assert.AreEqual(0, Harness.World.BodyCount);

            Harness.Enable(body);
            Assert.IsFalse(body.EntityId.IsNone);
            Assert.AreEqual(1, Harness.World.BodyCount);
        }
    }
}
