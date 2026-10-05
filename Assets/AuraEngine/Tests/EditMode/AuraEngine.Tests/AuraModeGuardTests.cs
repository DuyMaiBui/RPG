using System.Text.RegularExpressions;
using AuraEngine.Core;
using AuraEngine.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AuraEngine.Tests
{
    /* P0.3 2D vs 3D mode guards: authoring for the wrong physics mode must log an error and create nothing. */
    public sealed class AuraModeGuardTests : AuraAuthoringFixtureBase
    {
        [Test]
        public void Body2D_InFull3DInstance_LogsAnErrorAndCreatesNothing()
        {
            Harness = AuraAuthoringHarness.Create(AuraPhysicsMode.Full3D);
            Harness.CreateWorld();
            var body = Harness.AddBody2D(Harness.NewChild("Body2D", null, Vector3.zero, Vector3.zero, Vector3.one));

            LogAssert.Expect(LogType.Error, "AuraPhysicsBody2DAuthoring on 'Body2D' requires Plane2D mode.");
            Harness.Enable(body);

            Assert.AreEqual(0, Harness.World.BodyCount);
            Assert.IsTrue(body.EntityId.IsNone);
        }

        [Test]
        public void Joint2D_InFull3DInstance_LogsAnErrorAndCreatesNothing()
        {
            Harness = AuraAuthoringHarness.Create(AuraPhysicsMode.Full3D);
            Harness.CreateWorld();
            var joint = Harness.NewChild("Joint2D", null, Vector3.zero, Vector3.zero, Vector3.one).AddComponent<AuraJoint2DAuthoring>();

            LogAssert.Expect(LogType.Error, "AuraJoint2DAuthoring on 'Joint2D' requires Plane2D mode.");
            Harness.Enable(joint);

            Assert.IsFalse(joint.JointId.IsValid);
        }

        [Test]
        public void Chain2D_InFull3DInstance_LogsAnErrorAndCreatesNothing()
        {
            Harness = AuraAuthoringHarness.Create(AuraPhysicsMode.Full3D);
            Harness.CreateWorld();
            var chain = Harness.NewChild("Chain2D", null, Vector3.zero, Vector3.zero, Vector3.one).AddComponent<AuraRagdollChain2DAuthoring>();

            LogAssert.Expect(LogType.Error, "AuraRagdollChain2DAuthoring on 'Chain2D' requires Plane2D joint support.");
            Harness.Enable(chain);

            Assert.AreEqual(0, chain.Joints.Count);
        }

        [Test]
        public void Character2D_InFull3DInstance_LogsAnErrorAndCreatesNothing()
        {
            Harness = AuraAuthoringHarness.Create(AuraPhysicsMode.Full3D);
            Harness.CreateWorld();
            var character = Harness.NewChild("Character2D", null, Vector3.zero, Vector3.zero, Vector3.one).AddComponent<AuraCharacter2DAuthoring>();

            LogAssert.Expect(LogType.Error, new Regex("AuraCharacter2DAuthoring on 'Character2D'"));
            Harness.Enable(character);

            Assert.IsFalse(character.CharacterId.IsValid);
        }

        [Test]
        public void Body3D_InPlane2DInstance_LogsAnErrorAndCreatesNothing()
        {
            Harness = AuraAuthoringHarness.Create(AuraPhysicsMode.Plane2D);
            Harness.CreateWorld();
            var body = Harness.AddBody3D(Harness.NewChild("Body3D", null, Vector3.zero, Vector3.zero, Vector3.one));

            LogAssert.Expect(LogType.Error, new Regex("AuraPhysicsBodyAuthoring on 'Body3D'"));
            Harness.Enable(body);

            Assert.AreEqual(0, Harness.World.BodyCount);
            Assert.IsTrue(body.EntityId.IsNone);
        }

        [Test]
        public void Character3D_InPlane2DInstance_LogsAnErrorAndCreatesNothing()
        {
            Harness = AuraAuthoringHarness.Create(AuraPhysicsMode.Plane2D);
            Harness.CreateWorld();
            var character = Harness.NewChild("Character3D", null, Vector3.zero, Vector3.zero, Vector3.one).AddComponent<AuraCharacterAuthoring>();

            LogAssert.Expect(LogType.Error, new Regex("AuraCharacterAuthoring on 'Character3D'"));
            Harness.Enable(character);

            Assert.IsFalse(character.CharacterId.IsValid);
        }
    }
}
