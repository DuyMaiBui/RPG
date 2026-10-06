using NUnit.Framework;
using RPG.Core.Actors;
using RPG.Simulation.Contracts;
using RPG.Simulation.Runtime;

namespace RPG.Core.Tests
{
    public sealed class AbilityCatalogTests
    {
        private static SimulationVector2 V(float x, float y) => new SimulationVector2(x, y);

        [Test]
        public void Catalog_GrantsAbilitiesPerArchetype()
        {
            Assert.That(AbilityCatalog.Abilities(ActorArchetype.Bruiser), Does.Contain(AbilityCatalog.Cleave));
            Assert.That(AbilityCatalog.Abilities(ActorArchetype.Skirmisher), Does.Contain(AbilityCatalog.VenomStrike));
            Assert.That(AbilityCatalog.Abilities(ActorArchetype.Support), Does.Contain(AbilityCatalog.Mend));
            Assert.That(AbilityCatalog.Abilities(ActorArchetype.None), Is.Empty);
        }

        [Test]
        public void Catalog_EveryGrantedAbilityHasEffects()
        {
            foreach (var archetype in new[]
                     {
                         ActorArchetype.Bruiser, ActorArchetype.Skirmisher, ActorArchetype.Support,
                     })
            {
                foreach (var ability in AbilityCatalog.Abilities(archetype))
                    Assert.That(ability.Effects, Is.Not.Empty, $"{archetype} ability {ability.Id}");
            }
        }

        [Test]
        public void Catalog_SkirmisherAbilityPoisonsTheTargetThroughTheSystem()
        {
            var state = new RpgSimulationState();
            var casterId = state.Actors.Spawn(ActorKind.Monster, FactionId.Blue, new ActorSpawnData(
                10, 1, V(0f, 0f), 0.3f, 2f, 6f, 3.5f, 1f,
                abilities: AbilityCatalog.Abilities(ActorArchetype.Skirmisher)));
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
    }
}
