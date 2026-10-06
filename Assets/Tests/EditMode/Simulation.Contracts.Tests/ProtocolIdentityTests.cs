using NUnit.Framework;
using RPG.Simulation.Contracts;

namespace RPG.Simulation.Contracts.Tests
{
    /// <summary>Guards the equality contract of the protocol identity types. Their <c>IEquatable</c> members are
    /// implemented explicitly, so <c>Equals(object)</c> and the operators must not call <c>Equals(other)</c>: that
    /// binds back to <c>Equals(object)</c> and overflows the stack. This is a regression guard, not a style check.
    /// </summary>
    public sealed class ProtocolIdentityTests
    {
        [Test]
        public void PlayerId_EqualityNeverRecurses()
        {
            var id = new PlayerId("player-1");

            Assert.That(id.Equals((object)new PlayerId("player-1")), Is.True);
            Assert.That(id.Equals((object)new PlayerId("player-2")), Is.False);
            Assert.That(id.Equals("player-1"), Is.False);
            Assert.That(id == new PlayerId("player-1"), Is.True);
            Assert.That(id != new PlayerId("player-2"), Is.True);
        }

        [Test]
        public void PlayerId_HashesAndComparesByValue()
        {
            var first = new PlayerId("player-1");
            var second = new PlayerId("player-1");

            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
            Assert.That(first, Is.EqualTo(second));
            Assert.That(first, Is.Not.EqualTo(new PlayerId("player-1 ")));
            Assert.That(first.ToString(), Is.EqualTo("player-1"));
        }

        [Test]
        public void EntityId_EqualityNeverRecurses()
        {
            var id = new EntityId(3, 1);

            Assert.That(id.Equals((object)new EntityId(3, 1)), Is.True);
            Assert.That(id.Equals((object)new EntityId(3, 2)), Is.False);
            Assert.That(id.Equals((object)new EntityId(4, 1)), Is.False);
            Assert.That(id == new EntityId(3, 1), Is.True);
            Assert.That(id != EntityId.None, Is.True);
        }

        [Test]
        public void EntityId_NoneIsNotAValidIndex()
        {
            Assert.That(EntityId.None.IsNone, Is.True);
            Assert.That(new EntityId(0, 0).IsNone, Is.False);
            Assert.That(EntityId.None.Index, Is.LessThan(0));
        }
    }
}
