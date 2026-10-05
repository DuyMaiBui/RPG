using AuraEngine.Core;
using NUnit.Framework;

namespace AuraEngine.Tests
{
    public sealed class AuraWorldDefinitionGravityTests
    {
        [Test]
        public void UnspecifiedGravity_UsesEarthGravity()
        {
            var definition = new AuraWorldDefinition();

            Assert.AreEqual(-9.81f, definition.Gravity.Y, 1e-5f);
        }

        [Test]
        public void ExplicitZeroGravity_IsKeptInsteadOfBeingReplacedByTheDefault()
        {
            // A zero-gravity world (space scenes) used to be silently replaced by -9.81 because
            // default(AuraVector3) was treated as "not specified".
            var definition = new AuraWorldDefinition(gravity: AuraVector3.Zero);

            Assert.AreEqual(0f, definition.Gravity.X, 1e-6f);
            Assert.AreEqual(0f, definition.Gravity.Y, 1e-6f, "zero gravity was replaced by the default");
            Assert.AreEqual(0f, definition.Gravity.Z, 1e-6f);
        }

        [Test]
        public void ExplicitGravity_IsKept()
        {
            var definition = new AuraWorldDefinition(gravity: new AuraVector3(1f, -2f, 3f));

            Assert.AreEqual(new AuraVector3(1f, -2f, 3f), definition.Gravity);
        }
    }
}
