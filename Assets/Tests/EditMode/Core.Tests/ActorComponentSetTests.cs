using NUnit.Framework;
using RPG.Core.Actors;

namespace RPG.Core.Tests
{
    public sealed class ActorComponentSetTests
    {
        [Test]
        public void Components_AreRetrievedByConcreteTypeWithoutActorFields()
        {
            var components = new ActorComponentSet();
            var health = new HealthComponent(10);
            components.Add(health);

            Assert.That(components.TryGet<HealthComponent>(out var resolved), Is.True);
            Assert.That(resolved, Is.SameAs(health));
            Assert.That(components.Contains<HealthComponent>(), Is.True);
            Assert.That(components.Remove<HealthComponent>(), Is.True);
            Assert.That(components.Contains<HealthComponent>(), Is.False);
        }

        [Test]
        public void HealthComponent_ClampsDamageAndStopsAtZero()
        {
            var health = new HealthComponent(10);

            Assert.That(health.ReceiveDamage(4), Is.EqualTo(4));
            Assert.That(health.CurrentHealth, Is.EqualTo(6));
            Assert.That(health.ReceiveDamage(10), Is.EqualTo(6));
            Assert.That(health.IsDead, Is.True);
            Assert.That(health.ReceiveDamage(1), Is.EqualTo(0));
        }
    }
}
