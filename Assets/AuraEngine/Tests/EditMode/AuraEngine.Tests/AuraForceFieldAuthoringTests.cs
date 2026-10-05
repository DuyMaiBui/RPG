using AuraEngine.Core;
using AuraEngine.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AuraEngine.Tests
{
    /* P0.3 force field authoring. The field is observed by its effect: a body in zero gravity accelerates inside an
       enabled Directional zone, keeps its velocity once the zone is updated to zero or removed. */
    public sealed class AuraForceFieldAuthoringTests : AuraAuthoringFixtureBase
    {
        private AuraPhysicsBodyAuthoring _body;
        private AuraForceFieldAuthoring _field;

        [Test]
        public void ForceField_IsRegisteredOnEnable_AndAcceleratesBodiesInsideTheZone()
        {
            BuildScene();

            Harness.Enable(_body, _field);

            Assert.IsTrue(_field.FieldId.IsValid, "The native world supports force fields, so the field must be registered.");
            Harness.Step(10);
            Assert.Greater(VelocityY(), 0.5f, "A 10 m/s^2 upward zone must accelerate the body (expected about 1.67 m/s after 10 steps).");
        }

        [Test]
        public void ForceField_SetVector_UpdatesTheRegisteredField()
        {
            BuildScene();
            Harness.Enable(_body, _field);
            Harness.Step(10);
            var before = VelocityY();

            _field.SetVector(Vector3.zero);
            Harness.Step(10);

            Assert.AreEqual(before, VelocityY(), 0.01f, "After updating the vector to zero the body must stop accelerating.");
        }

        [Test]
        public void ForceField_IsRemovedOnDisable_AndNoLongerAcceleratesBodies()
        {
            BuildScene();
            Harness.Enable(_body, _field);
            Harness.Step(5);
            var before = VelocityY();

            AuraAuthoringLifecycle.Disable(_field);
            Assert.IsFalse(_field.FieldId.IsValid, "No field handle may remain after disable.");
            Harness.Step(10);

            Assert.AreEqual(before, VelocityY(), 0.01f, "A removed field must not act on bodies.");
        }

        [Test]
        public void ForceField_OnABackendWithoutForceFields_LogsAClearErrorAndCreatesNothing()
        {
            Harness = AuraAuthoringHarness.Create(AuraPhysicsMode.Full3D, new FakePhysicsBackend());
            Harness.CreateWorld();
            var field = Harness.NewChild("Field", null, Vector3.zero, Vector3.zero, Vector3.one).AddComponent<AuraForceFieldAuthoring>();

            LogAssert.Expect(LogType.Error, "AuraForceFieldAuthoring on 'Field': the physics backend has no force field support.");
            field.BuildInto(Harness.Instance);

            Assert.IsFalse(field.FieldId.IsValid);
        }

        private void BuildScene()
        {
            Harness = AuraAuthoringHarness.Create(AuraPhysicsMode.Full3D, zeroGravity: true);
            Harness.CreateWorld();
            _body = Harness.AddBody3D(Harness.NewChild("Body", null, Vector3.zero, Vector3.zero, Vector3.one));
            _field = Harness.NewChild("Field", null, Vector3.zero, Vector3.zero, Vector3.one).AddComponent<AuraForceFieldAuthoring>();
            AuraSerializedFields.SetEnum(_field, "_kind", AuraForceFieldKind.Directional);
            AuraSerializedFields.SetVector3(_field, "_vector", new Vector3(0f, 10f, 0f));
        }

        private float VelocityY()
        {
            Assert.IsTrue(Harness.World.TryGetBodyState(_body.EntityId, out var state));
            return state.LinearVelocity.Y;
        }
    }
}
