using AuraEngine.Core;
using AuraEngine.Physics.Native;
using AuraEngine.Unity;
using NUnit.Framework;
using UnityEngine;

namespace AuraEngine.Tests
{
    /* P0.3 joint anchor space. Scale semantics under test: joint anchors are LOCAL to each body and are mapped with
       bodyTransform.TransformPoint, so a body's localScale (and its rotation) multiplies the local anchor. The native joint
       must receive exactly that world point; the tests read it from the definition the world passes to the backend. */
    public sealed class AuraJointAnchorSpaceTests : AuraAuthoringFixtureBase
    {
        private const string ScaleBug =
            "Known past defect (joint anchor space): a bridge and a car exploded because the world anchor did not equal " +
            "bodyTransform.TransformPoint(localAnchor) on bodies with non-unit localScale.";

        [TestCase(AuraJointType.Hinge)]
        [TestCase(AuraJointType.Fixed)]
        [TestCase(AuraJointType.Distance)]
        public void Joint3D_WorldAnchorIsBodyTransformPointOfLocalAnchor_ForNonUnitScale(AuraJointType type)
        {
            var recording = new RecordingPhysicsBackend(new NativePhysicsBackend());
            Harness = AuraAuthoringHarness.Create(AuraPhysicsMode.Full3D, recording);
            var bodyA = Harness.AddBody3D(Harness.NewChild("A", null, new Vector3(0f, 5f, 0f), new Vector3(0f, 30f, 0f), new Vector3(2f, 3f, 4f)));
            var bodyB = Harness.AddBody3D(Harness.NewChild("B", null, new Vector3(3f, 5f, 1f), new Vector3(15f, 0f, 0f), new Vector3(0.5f, 1f, 2f)));
            var joint = Harness.Root.AddComponent<AuraJointAuthoring>();
            var localA = new Vector3(1f, 0.5f, -1f);
            var localB = new Vector3(-1f, 2f, 1f);
            AuraSerializedFields.SetReference(joint, "_bodyA", bodyA);
            AuraSerializedFields.SetReference(joint, "_bodyB", bodyB);
            AuraSerializedFields.SetEnum(joint, "_type", type);
            AuraSerializedFields.SetVector3(joint, "_anchorA", localA);
            AuraSerializedFields.SetVector3(joint, "_anchorB", localB);
            AuraSerializedFields.SetFloat(joint, "_distance", 3f);

            Harness.Enable(bodyA, bodyB, joint);
            Harness.CreateWorld();

            Assert.AreEqual(1, recording.JointDefinitions.Count, "Exactly one joint must reach the kernel.");
            Assert.IsTrue(joint.JointId.IsValid);
            var definition = recording.JointDefinitions[0];
            Assert.AreEqual(type, definition.Type);
            AssertNear(bodyA.transform.TransformPoint(localA), definition.AnchorA, ScaleBug + " Body A anchor.");
            AssertNear(bodyB.transform.TransformPoint(localB), definition.AnchorB, ScaleBug + " Body B anchor.");
            Assert.Greater(
                Vector3.Distance(bodyA.transform.TransformPoint(localA), bodyA.transform.position + bodyA.transform.rotation * localA),
                0.5f,
                "Test setup must use a scale that actually distinguishes TransformPoint from position + rotated local anchor.");
        }

        [TestCase(AuraJointType.Hinge)]
        [TestCase(AuraJointType.Rope)]
        [TestCase(AuraJointType.Wheel)]
        public void Joint2D_WorldAnchorIsBodyTransformPointOfLocalAnchor_ForNonUnitScale(AuraJointType type)
        {
            var recording = new RecordingPhysicsBackend(new NativePhysicsBackend());
            Harness = AuraAuthoringHarness.Create(AuraPhysicsMode.Plane2D, recording);
            var bodyA = Harness.AddBody2D(Harness.NewChild("A", null, new Vector3(0f, 5f, 0f), new Vector3(0f, 0f, 30f), new Vector3(2f, 3f, 1f)));
            var bodyB = Harness.AddBody2D(Harness.NewChild("B", null, new Vector3(3f, 5f, 0f), new Vector3(0f, 0f, -20f), new Vector3(0.5f, 1.5f, 1f)));
            var joint = Harness.Root.AddComponent<AuraJoint2DAuthoring>();
            // Anchors stay close to the body centres: Box2D joints reject anchors far from a light body (lever ratio rule),
            // and this test is about the local-to-world conversion, which the scale (2,3) and (0.5,1.5) still shifts.
            var localA = new Vector2(0.2f, 0.1f);
            var localB = new Vector2(-0.2f, 0.3f);
            AuraSerializedFields.SetReference(joint, "_bodyA", bodyA);
            AuraSerializedFields.SetReference(joint, "_bodyB", bodyB);
            AuraSerializedFields.SetEnum(joint, "_type", type);
            AuraSerializedFields.SetVector2(joint, "_anchorA", localA);
            AuraSerializedFields.SetVector2(joint, "_anchorB", localB);
            AuraSerializedFields.SetFloat(joint, "_distance", 3f);

            Harness.Enable(bodyA, bodyB, joint);
            Harness.CreateWorld();

            Assert.AreEqual(1, recording.JointDefinitions.Count, "Exactly one joint must reach the kernel.");
            Assert.IsTrue(joint.JointId.IsValid);
            var definition = recording.JointDefinitions[0];
            Assert.AreEqual(type, definition.Type);
            var expectedA = bodyA.transform.TransformPoint(new Vector3(localA.x, localA.y, 0f));
            var expectedB = bodyB.transform.TransformPoint(new Vector3(localB.x, localB.y, 0f));
            AssertNear(new Vector3(expectedA.x, expectedA.y, 0f), definition.AnchorA, ScaleBug + " Body A anchor (2D).");
            AssertNear(new Vector3(expectedB.x, expectedB.y, 0f), definition.AnchorB, ScaleBug + " Body B anchor (2D).");
        }
    }
}
