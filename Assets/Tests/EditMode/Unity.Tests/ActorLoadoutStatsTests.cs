using System;
using System.Collections.Generic;
using NUnit.Framework;
using RPG.Content;
using RPG.Core.Actors;
using RPG.Simulation.Contracts;

namespace RPG.Unity.Tests
{
    /// <summary>Authored loadout values that the scene hands to the simulation. The health multiplier is what makes a
    /// melee rank tougher than the rank that shoots from safety, so its scaling and its rejection of a bad value are
    /// both pinned here.</summary>
    public sealed class ActorLoadoutStatsTests
    {
        [Test]
        public void CreateSpawnData_ScalesHealthByTheLoadoutMultiplier()
        {
            var loadout = new ActorLoadoutAuthoring(AttackType.Melee, 0.25f, 0f, 0.05f, 0f, ActorArchetype.None, 2f);

            var data = loadout.CreateSpawnData(Catalog(), 30, 5, SimulationVector2.Zero, 0.35f, 0.8f, 45f, 1f);

            Assert.That(data.MaximumHealth, Is.EqualTo(60));
            Assert.That(data.AttackRange, Is.EqualTo(0.25f));
        }

        [Test]
        public void CreateSpawnData_KeepsTheSessionHealthWithoutAMultiplier()
        {
            var loadout = new ActorLoadoutAuthoring(AttackType.Projectile, 3.5f, 5f, 0.05f, 5f);

            var data = loadout.CreateSpawnData(Catalog(), 30, 5, SimulationVector2.Zero, 0.35f, 0.8f, 45f, 1f);

            Assert.That(data.MaximumHealth, Is.EqualTo(30));
        }

        [Test]
        public void CreateSpawnData_RejectsNonPositiveHealthMultiplier()
        {
            var loadout = new ActorLoadoutAuthoring(AttackType.Melee, 0.25f, 0f, 0.05f, 0f, ActorArchetype.None, 0f);

            Assert.Throws<InvalidOperationException>(() =>
                loadout.CreateSpawnData(Catalog(), 30, 5, SimulationVector2.Zero, 0.35f, 0.8f, 45f, 1f));
        }

        private static ContentCatalog Catalog() => new(
            new Dictionary<int, AbilityDefinition>(),
            new Dictionary<ActorArchetype, AbilityDefinition[]>
            {
                { ActorArchetype.Bruiser, new AbilityDefinition[0] },
                { ActorArchetype.Skirmisher, new AbilityDefinition[0] },
            });
    }
}