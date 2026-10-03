using AuraEngine.Core;
using AuraEngine.Physics;
using NUnit.Framework;

namespace AuraEngine.Tests
{
    public sealed class HairCapabilitySmokeTests
    {
        [Test]
        public void HeadlessHairCapability_IsExplicitlyUnsupported()
        {
            using (IPhysicsWorld world = new NullPhysicsWorld(new AuraWorldDefinition()))
            {
                Assert.IsFalse((world.Capabilities & AuraPhysicsCapabilities.Hair) != 0);
                Assert.AreEqual(AuraHairId.Invalid, world.Hair.CreateHair(new AuraHairDefinition(
                    AuraPose.Identity,
                    new[] { AuraVector3.Zero },
                    2,
                    0.1f)));
                Assert.AreEqual(AuraResult.UnsupportedShape, world.Hair.DestroyHair(AuraHairId.Invalid));
                Assert.IsFalse(world.Hair.TryGetState(AuraHairId.Invalid, out _));
            }
        }
    }
}
