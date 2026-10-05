using AuraEngine.Core;
using AuraEngine.Physics.Native;
using AuraEngine.Unity;
using NUnit.Framework;
using UnityEngine;

namespace AuraEngine.Tests
{
    /* P0.3 registration order. Past defect: authoring could not be built before the bodies it references were registered.
       Components are enabled in hierarchy order (joints and chains FIRST, bodies after) and the world is created last;
       the instance must still build bodies first, then joints. */
    public sealed class AuraRegistrationOrderTests : AuraAuthoringFixtureBase
    {
        private const string OrderBug =
            "Known past defect (registration order): authoring was built before its bodies were registered, so the joint or chain was never created.";

        [Test]
        public void Joint3D_EnabledBeforeItsBodies_StillBuildsWhenTheWorldIsCreated()
        {
            Harness = AuraAuthoringHarness.Create(AuraPhysicsMode.Full3D);
            var joint = Harness.Root.AddComponent<AuraJointAuthoring>();
            var bodyA = Harness.AddBody3D(Harness.NewChild("A", null, Vector3.zero, Vector3.zero, Vector3.one));
            var bodyB = Harness.AddBody3D(Harness.NewChild("B", null, new Vector3(2f, 0f, 0f), Vector3.zero, Vector3.one));
            AuraSerializedFields.SetReference(joint, "_bodyA", bodyA);
            AuraSerializedFields.SetReference(joint, "_bodyB", bodyB);
            AuraSerializedFields.SetEnum(joint, "_type", AuraJointType.Fixed);

            Harness.Enable(joint, bodyA, bodyB);
            Harness.CreateWorld();

            Assert.AreEqual(2, Harness.World.BodyCount);
            Assert.IsTrue(joint.JointId.IsValid, OrderBug);
        }

        [Test]
        public void Joint2D_EnabledBeforeItsBodies_StillBuildsWhenTheWorldIsCreated()
        {
            Harness = AuraAuthoringHarness.Create(AuraPhysicsMode.Plane2D);
            var joint = Harness.Root.AddComponent<AuraJoint2DAuthoring>();
            var bodyA = Harness.AddBody2D(Harness.NewChild("A", null, Vector3.zero, Vector3.zero, Vector3.one));
            var bodyB = Harness.AddBody2D(Harness.NewChild("B", null, new Vector3(2f, 0f, 0f), Vector3.zero, Vector3.one));
            AuraSerializedFields.SetReference(joint, "_bodyA", bodyA);
            AuraSerializedFields.SetReference(joint, "_bodyB", bodyB);
            AuraSerializedFields.SetEnum(joint, "_type", AuraJointType.Fixed);

            Harness.Enable(joint, bodyA, bodyB);
            Harness.CreateWorld();

            Assert.IsTrue(joint.JointId.IsValid, OrderBug);
        }

        [Test]
        public void Chain3D_EnabledBeforeItsLimbs_StillBuildsOneHingePerPair()
        {
            Harness = AuraAuthoringHarness.Create(AuraPhysicsMode.Full3D);
            var chain = Harness.Root.AddComponent<AuraRagdollChainAuthoring>();
            var limbs = new AuraPhysicsBodyAuthoring[3];
            for (var index = 0; index < limbs.Length; index++)
                limbs[index] = Harness.AddBody3D(Harness.NewChild("Limb" + index, null, new Vector3(index, 0f, 0f), Vector3.zero, Vector3.one));

            AuraSerializedFields.SetReferences(chain, "_limbs", limbs);
            Harness.Enable(chain);
            Harness.Enable(limbs);
            Harness.CreateWorld();

            Assert.AreEqual(2, chain.Joints.Count, OrderBug);
            Assert.IsTrue(chain.Joints[0].IsValid);
            Assert.IsTrue(chain.Joints[1].IsValid);
        }

        [Test]
        public void Chain2D_EnabledBeforeItsLimbs_StillBuildsOneHingePerPair()
        {
            Harness = AuraAuthoringHarness.Create(AuraPhysicsMode.Plane2D);
            var chain = Harness.Root.AddComponent<AuraRagdollChain2DAuthoring>();
            var limbs = new AuraPhysicsBody2DAuthoring[3];
            for (var index = 0; index < limbs.Length; index++)
                limbs[index] = Harness.AddBody2D(Harness.NewChild("Limb" + index, null, new Vector3(index, 0f, 0f), Vector3.zero, Vector3.one));

            AuraSerializedFields.SetReferences(chain, "_limbs", limbs);
            Harness.Enable(chain);
            Harness.Enable(limbs);
            Harness.CreateWorld();

            Assert.AreEqual(2, chain.Joints.Count, OrderBug);
        }

        [Test]
        public void GearJoint_BuildsItsReferencedHingesFirst_EvenWhenRegisteredBeforeThem()
        {
            var recording = new RecordingPhysicsBackend(new NativePhysicsBackend());
            Harness = AuraAuthoringHarness.Create(AuraPhysicsMode.Full3D, recording, zeroGravity: true);
            var fixedBody = Harness.AddBody3D(Harness.NewChild("Frame", null, Vector3.zero, Vector3.zero, Vector3.one), AuraBodyType.Static);
            var gearA = Harness.AddBody3D(Harness.NewChild("GearA", null, new Vector3(-1f, 0f, 0f), Vector3.zero, Vector3.one));
            var gearB = Harness.AddBody3D(Harness.NewChild("GearB", null, new Vector3(1f, 0f, 0f), Vector3.zero, Vector3.one));
            var hingeA = AddHinge("HingeA", fixedBody, gearA, new Vector3(-1f, 0f, 0f));
            var hingeB = AddHinge("HingeB", fixedBody, gearB, new Vector3(1f, 0f, 0f));
            var gear = Harness.Root.AddComponent<AuraGearJointAuthoring>();
            AuraSerializedFields.SetReference(gear, "_bodyA", gearA);
            AuraSerializedFields.SetReference(gear, "_bodyB", gearB);
            AuraSerializedFields.SetReference(gear, "_hingeA", hingeA);
            AuraSerializedFields.SetReference(gear, "_hingeB", hingeB);

            Harness.Enable(fixedBody, gearA, gearB, gear, hingeA, hingeB);
            Harness.CreateWorld();

            Assert.IsTrue(hingeA.JointId.IsValid, "The gear must build the hinge it references.");
            Assert.IsTrue(hingeB.JointId.IsValid, "The gear must build the hinge it references.");
            Assert.IsTrue(gear.JointId.IsValid, OrderBug);
            Assert.AreEqual(3, recording.JointDefinitions.Count, "Each joint must be created exactly once (building is idempotent).");
            Assert.AreEqual(AuraJointType.Hinge, recording.JointDefinitions[0].Type);
            Assert.AreEqual(AuraJointType.Hinge, recording.JointDefinitions[1].Type);
            Assert.AreEqual(AuraJointType.Gear, recording.JointDefinitions[2].Type, "The gear joint must be created after both hinges it couples.");
        }

        [Test]
        public void RackAndPinion_BuildsItsReferencedHingeAndSliderFirst_EvenWhenRegisteredBeforeThem()
        {
            var recording = new RecordingPhysicsBackend(new NativePhysicsBackend());
            Harness = AuraAuthoringHarness.Create(AuraPhysicsMode.Full3D, recording, zeroGravity: true);
            var frame = Harness.AddBody3D(Harness.NewChild("Frame", null, Vector3.zero, Vector3.zero, Vector3.one), AuraBodyType.Static);
            var pinion = Harness.AddBody3D(Harness.NewChild("Pinion", null, Vector3.zero, Vector3.zero, Vector3.one));
            var rack = Harness.AddBody3D(Harness.NewChild("Rack", null, new Vector3(0f, 1f, 0f), Vector3.zero, Vector3.one));
            var hinge = AddHinge("PinionHinge", frame, pinion, Vector3.zero);
            var slider = AddJoint("RackSlider", AuraJointType.Slider, frame, rack, new Vector3(0f, 1f, 0f), Vector3.right);
            var coupling = Harness.Root.AddComponent<AuraRackAndPinionJointAuthoring>();
            AuraSerializedFields.SetReference(coupling, "_bodyA", pinion);
            AuraSerializedFields.SetReference(coupling, "_bodyB", rack);
            AuraSerializedFields.SetReference(coupling, "_pinionHinge", hinge);
            AuraSerializedFields.SetReference(coupling, "_rackSlider", slider);

            Harness.Enable(frame, pinion, rack, coupling, hinge, slider);
            Harness.CreateWorld();

            Assert.IsTrue(hinge.JointId.IsValid, "The coupling must build the hinge it references.");
            Assert.IsTrue(slider.JointId.IsValid, "The coupling must build the slider it references.");
            Assert.IsTrue(coupling.JointId.IsValid, OrderBug);
            Assert.AreEqual(3, recording.JointDefinitions.Count);
            Assert.AreEqual(AuraJointType.RackAndPinion, recording.JointDefinitions[2].Type);
        }

        [Test]
        public void GearJoint_DestroyingAReferencedHingeFirst_NeverThrows()
        {
            Harness = AuraAuthoringHarness.Create(AuraPhysicsMode.Full3D, zeroGravity: true);
            var frame = Harness.AddBody3D(Harness.NewChild("Frame", null, Vector3.zero, Vector3.zero, Vector3.one), AuraBodyType.Static);
            var gearA = Harness.AddBody3D(Harness.NewChild("GearA", null, new Vector3(-1f, 0f, 0f), Vector3.zero, Vector3.one));
            var gearB = Harness.AddBody3D(Harness.NewChild("GearB", null, new Vector3(1f, 0f, 0f), Vector3.zero, Vector3.one));
            var hingeA = AddHinge("HingeA", frame, gearA, new Vector3(-1f, 0f, 0f));
            var hingeB = AddHinge("HingeB", frame, gearB, new Vector3(1f, 0f, 0f));
            var gear = Harness.Root.AddComponent<AuraGearJointAuthoring>();
            AuraSerializedFields.SetReference(gear, "_bodyA", gearA);
            AuraSerializedFields.SetReference(gear, "_bodyB", gearB);
            AuraSerializedFields.SetReference(gear, "_hingeA", hingeA);
            AuraSerializedFields.SetReference(gear, "_hingeB", hingeB);
            Harness.Enable(frame, gearA, gearB, hingeA, hingeB, gear);
            Harness.CreateWorld();
            Assert.IsTrue(gear.JointId.IsValid);

            Assert.DoesNotThrow(() =>
            {
                // Unity sends OnDisable before destroying; the referenced hinge goes first, the coupling afterwards.
                AuraAuthoringLifecycle.Disable(hingeA);
                UnityEngine.Object.DestroyImmediate(hingeA.gameObject);
                Harness.Step(2);
                AuraAuthoringLifecycle.Disable(gear);
                UnityEngine.Object.DestroyImmediate(gear);
                AuraAuthoringLifecycle.Disable(hingeB);
                Harness.Step(2);
            });
        }

        [Test]
        public void Chain3D_DestroyingALimbBodyFirst_NeverThrows()
        {
            Harness = AuraAuthoringHarness.Create(AuraPhysicsMode.Full3D);
            var chain = Harness.Root.AddComponent<AuraRagdollChainAuthoring>();
            var limbs = new AuraPhysicsBodyAuthoring[3];
            for (var index = 0; index < limbs.Length; index++)
                limbs[index] = Harness.AddBody3D(Harness.NewChild("Limb" + index, null, new Vector3(index, 0f, 0f), Vector3.zero, Vector3.one));

            AuraSerializedFields.SetReferences(chain, "_limbs", limbs);
            Harness.Enable(limbs);
            Harness.Enable(chain);
            Harness.CreateWorld();
            Assert.AreEqual(2, chain.Joints.Count);

            Assert.DoesNotThrow(() =>
            {
                AuraAuthoringLifecycle.Disable(limbs[1]);
                UnityEngine.Object.DestroyImmediate(limbs[1].gameObject);
                AuraAuthoringLifecycle.Disable(chain);
                Harness.Step(2);
            });
        }

        private AuraJointAuthoring AddHinge(string name, AuraPhysicsBodyAuthoring bodyA, AuraPhysicsBodyAuthoring bodyB, Vector3 worldAnchor) =>
            AddJoint(name, AuraJointType.Hinge, bodyA, bodyB, worldAnchor, Vector3.forward);

        /* The anchor is a world point; with identity bodies at known positions the local anchors are derived from it. */
        private AuraJointAuthoring AddJoint(string name, AuraJointType type, AuraPhysicsBodyAuthoring bodyA, AuraPhysicsBodyAuthoring bodyB, Vector3 worldAnchor, Vector3 axis)
        {
            var go = Harness.NewChild(name, null, Vector3.zero, Vector3.zero, Vector3.one);
            var joint = go.AddComponent<AuraJointAuthoring>();
            AuraSerializedFields.SetReference(joint, "_bodyA", bodyA);
            AuraSerializedFields.SetReference(joint, "_bodyB", bodyB);
            AuraSerializedFields.SetEnum(joint, "_type", type);
            AuraSerializedFields.SetVector3(joint, "_anchorA", bodyA.transform.InverseTransformPoint(worldAnchor));
            AuraSerializedFields.SetVector3(joint, "_anchorB", bodyB.transform.InverseTransformPoint(worldAnchor));
            AuraSerializedFields.SetVector3(joint, "_axisA", axis);
            AuraSerializedFields.SetVector3(joint, "_axisB", axis);
            return joint;
        }
    }
}
