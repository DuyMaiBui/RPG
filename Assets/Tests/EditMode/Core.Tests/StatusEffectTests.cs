using NUnit.Framework;
using RPG.Core.Actors;
using RPG.Simulation.Contracts;
using RPG.Simulation.Runtime;

namespace RPG.Core.Tests
{
    public sealed class StatusEffectTests
    {
        private static SimulationVector2 V(float x, float y) => new SimulationVector2(x, y);

        private static Actor Spawn(RpgSimulationState state, int health = 10, float moveSpeed = 2f)
        {
            var id = state.Actors.Spawn(ActorKind.Monster, FactionId.Blue, new ActorSpawnData(
                health, 1, V(0f, 0f), 0.3f, moveSpeed, 5f, 0.5f, 1f));
            state.Actors.TryGet(id, out var actor);
            return actor;
        }

        [Test]
        public void Apply_AddsAnEffectThatAdvancesToExpiry()
        {
            var component = new StatusEffectComponent();

            Assert.That(component.Apply(StatusEffectType.Poison, 2, 3), Is.True);
            Assert.That(component.Count, Is.EqualTo(1));
            Assert.That(component.GetAt(0).RemainingTicks, Is.EqualTo(3));
            Assert.That(component.GetAt(0).Stacks, Is.EqualTo(1));

            component.Advance();
            component.Advance();
            Assert.That(component.Count, Is.EqualTo(1));
            Assert.That(component.GetAt(0).RemainingTicks, Is.EqualTo(1));

            component.Advance();
            Assert.That(component.Count, Is.EqualTo(0));
        }

        [Test]
        public void Apply_RefreshesAndStacksUpToTheTypeCap()
        {
            var component = new StatusEffectComponent();
            component.Apply(StatusEffectType.Poison, 2, 3);
            component.Apply(StatusEffectType.Poison, 5, 4);
            component.Apply(StatusEffectType.Poison, 1, 1);

            Assert.That(component.Count, Is.EqualTo(1));
            var effect = component.GetAt(0);
            Assert.That(effect.Stacks, Is.EqualTo(3));
            Assert.That(effect.Magnitude, Is.EqualTo(5));
            Assert.That(effect.RemainingTicks, Is.EqualTo(4));

            component.Apply(StatusEffectType.Poison, 1, 1);
            Assert.That(component.GetAt(0).Stacks, Is.EqualTo(3));
        }

        [Test]
        public void Apply_UsesThePerTypeStackCap()
        {
            var component = new StatusEffectComponent();
            component.Apply(StatusEffectType.Regeneration, 2, 3);
            component.Apply(StatusEffectType.Regeneration, 2, 3);

            Assert.That(component.Count, Is.EqualTo(1));
            Assert.That(component.GetAt(0).Stacks, Is.EqualTo(1));
        }

        [Test]
        public void Apply_IgnoresNonPositiveDurations()
        {
            var component = new StatusEffectComponent();

            Assert.That(component.Apply(StatusEffectType.Poison, 2, 0), Is.False);
            Assert.That(component.Count, Is.EqualTo(0));
        }

        [Test]
        public void System_PoisonsAnActorAndPublishesDamage()
        {
            var state = new RpgSimulationState();
            var actor = Spawn(state, health: 10);
            actor.Components.Get<StatusEffectComponent>().Apply(StatusEffectType.Poison, 3, 3);

            var context = new SimulationContext<RpgSimulationState>(state, 1f / 60f);
            new StatusEffectSystem().Tick(context);

            Assert.That(actor.Components.Get<HealthComponent>().CurrentHealth, Is.EqualTo(7));
            Assert.That(context.DrainEvents(), Has.Exactly(1).TypeOf<ActorDamaged>());
        }

        [Test]
        public void System_RegeneratesUpToMaximumHealth()
        {
            var state = new RpgSimulationState();
            var actor = Spawn(state, health: 10);
            var health = actor.Components.Get<HealthComponent>();
            health.ReceiveDamage(5);
            actor.Components.Get<StatusEffectComponent>().Apply(StatusEffectType.Regeneration, 3, 3);

            var context = new SimulationContext<RpgSimulationState>(state, 1f / 60f);
            new StatusEffectSystem().Tick(context);

            Assert.That(health.CurrentHealth, Is.EqualTo(8));
        }

        [Test]
        public void System_SlowModifiesSpeedWhileActiveAndResetsAfterExpiry()
        {
            var state = new RpgSimulationState();
            var actor = Spawn(state, moveSpeed: 2f);
            var movement = actor.Components.Get<MovementComponent>();
            var effects = actor.Components.Get<StatusEffectComponent>();
            effects.Apply(StatusEffectType.Slow, 50, 2);

            var context = new SimulationContext<RpgSimulationState>(state, 1f / 60f);
            var system = new StatusEffectSystem();

            system.Tick(context);
            Assert.That(effects.Count, Is.EqualTo(1));
            Assert.That(movement.SpeedMultiplier, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(movement.EffectiveSpeed, Is.EqualTo(1f).Within(0.0001f));

            system.Tick(context);
            Assert.That(effects.Count, Is.EqualTo(0));
            Assert.That(movement.SpeedMultiplier, Is.EqualTo(0.5f).Within(0.0001f));

            system.Tick(context);
            Assert.That(movement.SpeedMultiplier, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void Snapshot_CarriesTheStatusEffectMask()
        {
            var state = new RpgSimulationState();
            var actor = Spawn(state, health: 10);
            actor.Components.Get<StatusEffectComponent>().Apply(StatusEffectType.Poison, 2, 3);

            var snapshot = state.Actors.CreateSnapshot()[0];

            Assert.That(snapshot.StatusEffectMask, Is.EqualTo(1 << (int)StatusEffectType.Poison));
        }

        [Test]
        public void System_LethalPoisonPublishesDeathAndRemovesTheActor()
        {
            var state = new RpgSimulationState();
            var actor = Spawn(state, health: 2);
            var actorId = actor.Id;
            actor.Components.Get<StatusEffectComponent>().Apply(StatusEffectType.Poison, 5, 1);

            var context = new SimulationContext<RpgSimulationState>(state, 1f / 60f);
            new StatusEffectSystem().Tick(context);
            context.CommitDeferredActions();

            Assert.That(actor.Components.Get<HealthComponent>().IsDead, Is.True);
            Assert.That(state.Actors.TryGet(actorId, out _), Is.False);
            Assert.That(context.DrainEvents(), Has.Exactly(1).TypeOf<ActorDamaged>());
            Assert.That(context.DrainEvents(), Has.Exactly(1).TypeOf<ActorDied>());
        }
    }
}
