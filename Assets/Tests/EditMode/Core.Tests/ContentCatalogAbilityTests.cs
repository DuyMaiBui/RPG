using NUnit.Framework;
using RPG.Content;
using RPG.Core.Actors;
using RPG.Simulation.Contracts;
using RPG.Simulation.Runtime;

namespace RPG.Core.Tests
{
    public sealed class ContentCatalogAbilityTests
    {
        private static SimulationVector2 V(float x, float y) => new SimulationVector2(x, y);

        [Test]
        public void Catalog_ArchetypeAbilityPoisonsTheTargetThroughTheSystem()
        {
            var builder = new ContentCatalogBuilder();
            builder.AddAbility(new AbilityDefinition(
                2,
                6,
                4f,
                AbilityTargetMode.CurrentTarget,
                new[]
                {
                    new AbilityEffect(AbilityEffectType.Damage, 3),
                    new AbilityEffect(AbilityEffectType.Poison, 2, 8),
                }));
            builder.AddArchetypeLoadout(ActorArchetype.Skirmisher, new[] { 2 });
            var catalog = builder.Build();

            var state = new RpgSimulationState();
            var casterId = state.Actors.Spawn(ActorKind.Monster, FactionId.Blue, new ActorSpawnData(
                10, 1, V(0f, 0f), 0.3f, 2f, 6f, 3.5f, 1f,
                abilities: catalog.AbilitiesFor(ActorArchetype.Skirmisher)));
            var targetId = state.Actors.Spawn(ActorKind.Player, FactionId.Red, new ActorSpawnData(
                10, 1, V(1f, 0f), 0.3f, 2f, 6f, 1f, 1f));
            state.Actors.TryGet(casterId, out var caster);
            state.Actors.TryGet(targetId, out var target);
            caster.Components.Get<TargetComponent>().CurrentTarget = targetId;

            new AbilitySystem().Tick(new SimulationContext<RpgSimulationState>(state, 1f / 60f));

            Assert.That(target.Components.Get<HealthComponent>().CurrentHealth, Is.EqualTo(7));
            Assert.That(target.Components.Get<StatusEffectComponent>().TryGet(StatusEffectType.Poison, out var poison), Is.True);
            Assert.That(poison.Magnitude, Is.EqualTo(2));
        }

        [Test]
        public void Catalog_ArchetypeWithoutLoadoutGrantsNoAbilities()
        {
            var builder = new ContentCatalogBuilder();
            builder.AddAbility(new AbilityDefinition(
                1,
                6,
                4f,
                AbilityTargetMode.CurrentTarget,
                new[] { new AbilityEffect(AbilityEffectType.Damage, 3) }));
            builder.AddArchetypeLoadout(ActorArchetype.Bruiser, new[] { 1 });
            var catalog = builder.Build();

            var state = new RpgSimulationState();
            var actorId = state.Actors.Spawn(ActorKind.Monster, FactionId.Blue, new ActorSpawnData(
                10, 1, V(0f, 0f), 0.3f, 2f, 6f, 3.5f, 1f,
                abilities: catalog.AbilitiesFor(ActorArchetype.Support)));

            state.Actors.TryGet(actorId, out var actor);

            Assert.That(actor.Components.TryGet<AbilityComponent>(out _), Is.False);
        }
    }
}
