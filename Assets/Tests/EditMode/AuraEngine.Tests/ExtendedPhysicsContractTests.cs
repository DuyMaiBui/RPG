using AuraEngine.Core;
using AuraEngine.Physics;
using NUnit.Framework;

namespace AuraEngine.Tests
{
    public sealed class ExtendedPhysicsContractTests
    {
        [Test]
        public void AllWorldImplementationsExposeWaterContract()
        {
            using (IPhysicsWorld nullWorld = new NullPhysicsWorld(new AuraWorldDefinition()))
            using (IPhysicsWorld fakeWorld = new FakePhysicsWorld(new AuraWorldDefinition(), new System.Collections.Generic.List<AuraPhysicsEvent>()))
            {
                Assert.AreSame(nullWorld, nullWorld.Water);
                Assert.AreSame(fakeWorld, fakeWorld.Water);
                Assert.AreEqual(AuraWaterId.Invalid, nullWorld.Water.CreateWater(default));
                Assert.AreEqual(AuraWaterId.Invalid, fakeWorld.Water.CreateWater(default));
            }
        }

        [Test]
        public void ExtendedCapabilitiesHaveStableDistinctBits()
        {
            Assert.AreNotEqual(AuraPhysicsCapabilities.Ragdolls, AuraPhysicsCapabilities.Water);
            Assert.AreEqual(0, ((int)AuraPhysicsCapabilities.Ragdolls & (int)AuraPhysicsCapabilities.Water));
        }
    }
}
