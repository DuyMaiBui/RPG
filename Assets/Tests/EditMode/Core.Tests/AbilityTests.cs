using NUnit.Framework;
using RPG.Core.Actors;
using RPG.Core.Navigation;
using RPG.Simulation.Contracts;
using RPG.Simulation.Runtime;

namespace RPG.Core.Tests
{
    public sealed class AbilityTests
    {
        private static SimulationVector2 V(float x, float y) => new SimulationVector2(x, y);

        private static (RpgSimulationState State, Actor Caster, Actor Target) Setup(
            float targetX = 1f,
            AbilityDefinition[] casterAbilities = null,
            bool wallBetween = false,
            AbilityDefinition[] targetAbilities = null)
        {
            var state = new RpgSimulationState();
            if (wallBetween)
                state.Navigation.ApplyObstacle(new NavigationObstacle(1, V(targetX * 0.5f, 0f), V(0.2f, 0.5f)));

            var casterId = state.Actors.Spawn(ActorKind.Player, FactionId.Red, new ActorSpawnData(
                10, 1, V(0f, 0f), 0.3f, 2f, 6f, 1f, 1f, abilities: casterAbilities));
            var targetId = state.Actors.Spawn(ActorKind.Monster, FactionId.Blue, new ActorSpawnData(
                10, 1, V(targetX, 0f), 0.3f, 2f, 6f, 1f, 1f, abilities: targetAbilities));

            state.Actors.TryGet(casterId, out var caster);
            state.Actors.TryGet(targetId, out var target);
            caster.Components.Get<TargetComponent>().CurrentTarget = targetId;
            return (state, caster, target);
        }

        private static AbilityDefinition Strike(int damage, float range = 3f, int cooldown = 2) =>
            new AbilityDefinition(1, cooldown, range, AbilityTargetMode.CurrentTarget,
                new[] { new AbilityEffect(AbilityEffectType.Damage, damage) });

        private static SimulationContext<RpgSimulationState> Context(RpgSimulationState state) =>
            new SimulationContext<RpgSimulationState>(state, 1f / 60f);

        [Test]
        public void System_CastsATargetAbilityInRangeAndStartsTheCooldown()
        {
            var (state, caster, target) = Setup(casterAbilities: new[] { Strike(4) });
            new AbilitySystem().Tick(Context(state));

            Assert.That(target.Components.Get<HealthComponent>().CurrentHealth, Is.EqualTo(6));
            Assert.That(caster.Components.Get<AbilityComponent>().CooldownRemaining(0), Is.EqualTo(2));
        }

        [Test]
        public void System_DoesNotCastOutOfRange()
        {
            var (state, _, target) = Setup(targetX: 6f, casterAbilities: new[] { Strike(4, range: 1f) });
            new AbilitySystem().Tick(Context(state));

            Assert.That(target.Components.Get<HealthComponent>().CurrentHealth, Is.EqualTo(10));
        }

        [Test]
        public void System_DoesNotCastThroughAWall()
        {
            var (state, _, target) = Setup(targetX: 1f, casterAbilities: new[] { Strike(4) }, wallBetween: true);
            new AbilitySystem().Tick(Context(state));

            Assert.That(target.Components.Get<HealthComponent>().CurrentHealth, Is.EqualTo(10));
        }

        [Test]
        public void System_WaitsForTheCooldownBeforeCastingAgain()
        {
            var (state, _, target) = Setup(casterAbilities: new[] { Strike(4, cooldown: 2) });
            var context = Context(state);
            var system = new AbilitySystem();

            system.Tick(context);
            Assert.That(target.Components.Get<HealthComponent>().CurrentHealth, Is.EqualTo(6));

            system.Tick(context);
            Assert.That(target.Components.Get<HealthComponent>().CurrentHealth, Is.EqualTo(6));

            system.Tick(context);
            Assert.That(target.Components.Get<HealthComponent>().CurrentHealth, Is.EqualTo(2));
        }

        [Test]
        public void System_AppliesStatusEffectsToTheTarget()
        {
            var poison = new AbilityDefinition(2, 3, 3f, AbilityTargetMode.CurrentTarget,
                new[] { new AbilityEffect(AbilityEffectType.Poison, 2, 4) });
            var (state, _, target) = Setup(casterAbilities: new[] { poison });
            new AbilitySystem().Tick(Context(state));

            var effects = target.Components.Get<StatusEffectComponent>();
            Assert.That(effects.TryGet(StatusEffectType.Poison, out var effect), Is.True);
            Assert.That(effect.Magnitude, Is.EqualTo(2));
            Assert.That(effect.RemainingTicks, Is.EqualTo(4));
        }

        [Test]
        public void System_CastsASelfAbilityWithoutATarget()
        {
            var mend = new AbilityDefinition(3, 4, 0f, AbilityTargetMode.Self,
                new[] { new AbilityEffect(AbilityEffectType.Heal, 3) });
            var state = new RpgSimulationState();
            var casterId = state.Actors.Spawn(ActorKind.Player, FactionId.Red, new ActorSpawnData(
                10, 1, V(0f, 0f), 0.3f, 2f, 6f, 1f, 1f, abilities: new[] { mend }));
            state.Actors.TryGet(casterId, out var caster);
            var health = caster.Components.Get<HealthComponent>();
            health.ReceiveDamage(5);

            new AbilitySystem().Tick(Context(state));

            Assert.That(health.CurrentHealth, Is.EqualTo(8));
        }

        [Test]
        public void System_PublishesAnAbilityCastEvent()
        {
            var (state, _, _) = Setup(casterAbilities: new[] { Strike(4) });

            var context = Context(state);
            new AbilitySystem().Tick(context);

            Assert.That(context.DrainEvents(), Has.Exactly(1).TypeOf<ActorAbilityCast>());
        }

        [Test]
        public void System_LethalAbilityPublishesDeathAndRemovesTheTarget()
        {
            var (state, _, target) = Setup(casterAbilities: new[] { Strike(20) });
            var targetId = target.Id;
            var context = Context(state);

            new AbilitySystem().Tick(context);
            context.CommitDeferredActions();

            Assert.That(target.Components.Get<HealthComponent>().IsDead, Is.True);
            Assert.That(state.Actors.TryGet(targetId, out _), Is.False);
            Assert.That(context.DrainEvents(), Has.Exactly(1).TypeOf<ActorDied>());
        }
    }
}
