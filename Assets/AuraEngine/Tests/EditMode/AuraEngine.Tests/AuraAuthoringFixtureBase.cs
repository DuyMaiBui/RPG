using AuraEngine.Core;
using AuraEngine.Physics.Native;
using AuraEngine.Unity;
using NUnit.Framework;
using UnityEngine;

namespace AuraEngine.Tests
{
    /* Base for fixtures that build authoring components in a test scene: requires libaura and disposes the harness. */
    public abstract class AuraAuthoringFixtureBase
    {
        protected AuraAuthoringHarness Harness { get; set; }

        [SetUp]
        public void SetUpFixture()
        {
            Assert.IsTrue(NativePhysicsBackend.IsAvailable(), "libaura is not loadable.");
        }

        [TearDown]
        public void TearDownFixture()
        {
            Harness?.Dispose();
            Harness = null;
        }

        protected static void AssertNear(Vector3 expected, AuraVector3 actual, string message, float tolerance = 1e-3f)
        {
            Assert.Less(Vector3.Distance(expected, actual.ToUnity()), tolerance, $"{message} Expected {expected}, got {actual.ToUnity()}.");
        }

        protected static void AssertSameRotation(Quaternion expected, AuraQuaternion actual, string message, float toleranceDegrees = 0.1f)
        {
            Assert.Less(Quaternion.Angle(expected, actual.ToUnity()), toleranceDegrees, message);
        }
    }
}
