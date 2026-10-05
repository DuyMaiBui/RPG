using AuraEngine.Core;
using AuraEngine.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AuraEngine.Tests
{
    /* P0.3 character authoring (3D and 2D) and the Characters capability. */
    public sealed class AuraCharacterAuthoringTests : AuraAuthoringFixtureBase
    {
        [Test]
        public void Character3D_OnABackendWithCharacters_IsCreatedAtTheTransformAndReleasedOnDisable()
        {
            Harness = AuraAuthoringHarness.Create(AuraPhysicsMode.Full3D);
            Harness.CreateWorld();
            var character = Harness.NewChild("Hero", null, new Vector3(1f, 2f, 3f), Vector3.zero, Vector3.one).AddComponent<AuraCharacterAuthoring>();

            Harness.Enable(character);

            Assert.IsTrue(character.CharacterId.IsValid, "The native 3D world reports the Characters capability, so the character must be created.");
            Assert.IsTrue(Harness.Instance.TryGetCharacterState(character.CharacterId, out var state));
            AssertNear(new Vector3(1f, 2f, 3f), state.Position, "Character starts at the transform position.", 0.05f);

            AuraAuthoringLifecycle.Disable(character);
            Assert.IsFalse(character.CharacterId.IsValid, "No character handle may remain after disable.");
        }

        [Test]
        public void Character2D_OnABackendWithCharacters_IsCreatedAndReleasedOnDisable()
        {
            Harness = AuraAuthoringHarness.Create(AuraPhysicsMode.Plane2D);
            Harness.CreateWorld();
            var character = Harness.NewChild("Hero2D", null, new Vector3(1f, 2f, 0f), Vector3.zero, Vector3.one).AddComponent<AuraCharacter2DAuthoring>();

            Harness.Enable(character);

            Assert.IsTrue(character.CharacterId.IsValid, "The native 2D world reports the Characters capability, so the character must be created.");
            Assert.IsTrue(Harness.Instance.TryGetCharacterState(character.CharacterId, out var state));
            AssertNear(new Vector3(1f, 2f, 0f), state.Position, "2D character starts at the transform position.", 0.05f);

            AuraAuthoringLifecycle.Disable(character);
            Assert.IsFalse(character.CharacterId.IsValid);

            Harness.Enable(character);
            Assert.IsTrue(character.CharacterId.IsValid, "Re-enabling must create the character again.");
        }

        [Test]
        public void Character3D_OnABackendWithoutCharacters_LogsAClearErrorAndCreatesNothing()
        {
            // The Fake backend does not report AuraPhysicsCapabilities.Characters. BuildInto is called directly because
            // AuraSimulationInstance.Register / CreateWorld skip unsupported authoring without logging.
            Harness = AuraAuthoringHarness.Create(AuraPhysicsMode.Full3D, new FakePhysicsBackend());
            Harness.CreateWorld();
            var character = Harness.NewChild("Hero", null, Vector3.zero, Vector3.zero, Vector3.one).AddComponent<AuraCharacterAuthoring>();

            LogAssert.Expect(LogType.Error, "AuraCharacterAuthoring on 'Hero' needs a backend that supports characters.");
            character.BuildInto(Harness.Instance);

            Assert.IsFalse(character.CharacterId.IsValid);
        }

        [Test]
        public void Character2D_OnABackendWithoutCharacters_LogsAClearErrorAndCreatesNothing()
        {
            Harness = AuraAuthoringHarness.Create(AuraPhysicsMode.Plane2D, new FakePhysicsBackend());
            Harness.CreateWorld();
            var character = Harness.NewChild("Hero2D", null, Vector3.zero, Vector3.zero, Vector3.one).AddComponent<AuraCharacter2DAuthoring>();

            LogAssert.Expect(LogType.Error, "AuraCharacter2DAuthoring on 'Hero2D' needs a backend that supports characters (Plane2D).");
            character.BuildInto(Harness.Instance);

            Assert.IsFalse(character.CharacterId.IsValid);
        }
    }
}
