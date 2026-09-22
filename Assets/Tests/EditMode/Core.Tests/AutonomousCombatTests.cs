using NUnit.Framework;
using RPG.Core.Actors;
using RPG.Core.Formations;
using RPG.Core.Navigation;
using RPG.Core.Projectiles;
using RPG.Simulation.Contracts;
using RPG.Simulation.Runtime;

namespace RPG.Core.Tests
{
    public sealed class AutonomousCombatTests
    {
        [Test]
        public void Pathfinder_RoutesAroundBlockedCell()
        {
            var blocked = new bool[9];
            blocked[4] = true;
            var grid = new NavigationGrid(3, 3, 1f, new SimulationVector2(0f, 0f), blocked);
            var pathfinder = new AStarPathfinder(grid);

            Assert.That(pathfinder.TryFindPath(
                new SimulationVector2(0.5f, 1.5f),
                new SimulationVector2(2.5f, 1.5f),
                0.25f,
                out var path), Is.True);
            Assert.That(path, Is.Not.Empty);
            Assert.That(path, Has.None.EqualTo(new SimulationVector2(1.5f, 1.5f)));
        }

        [Test]
        public void TargetSelector_UsesVisionAndConfiguredPriority()
        {
            var registry = new ActorRegistry();
            var attackerId = registry.Spawn(ActorKind.Player, FactionId.Red, new ActorSpawnData(
                10, 1, new SimulationVector2(0f, 0f), 0.3f, 0f, 3f, 1f, 1f,
                TargetPriorityMode.LowestHealth));
            registry.Spawn(ActorKind.Monster, FactionId.Blue, new ActorSpawnData(
                10, 1, new SimulationVector2(1f, 0f), 0.3f, 0f, 3f, 1f, 1f));
            var weakId = registry.Spawn(ActorKind.Monster, FactionId.Blue, new ActorSpawnData(
                1, 1, new SimulationVector2(2f, 0f), 0.3f, 0f, 3f, 1f, 1f));

            Assert.That(registry.TryGet(attackerId, out var attacker), Is.True);
            Assert.That(new AnyAliveEnemyTargetSelector().TrySelect(attacker, registry, out var selected), Is.True);
            Assert.That(selected, Is.EqualTo(weakId));
        }

        [Test]
        public void FormationCoordinator_ResolvesAuthoringOffset()
        {
            var coordinator = new FormationCoordinator();
            coordinator.Register(7, new SimulationVector2(3f, 4f));
            var slot = new FormationSlotComponent(7, new SimulationVector2(-1f, 2f));

            Assert.That(coordinator.TryGetPosition(slot, out var position), Is.True);
            Assert.That(position, Is.EqualTo(new SimulationVector2(2f, 6f)));
        }

        [Test]
        public void PredictionBuffer_ReplaysInputsAfterAcknowledgement()
        {
            var buffer = new PredictionBuffer(4);
            buffer.Record(new PredictedMoveInput(new ClientSequence(1), new SimulationTick(1), new SimulationVector2(1f, 0f)));
            buffer.Record(new PredictedMoveInput(new ClientSequence(2), new SimulationTick(2), new SimulationVector2(1f, 0f)));

            var position = buffer.Reconcile(new SimulationVector2(5f, 0f), new ClientSequence(1), 2f, 0.5f);

            Assert.That(position, Is.EqualTo(new SimulationVector2(6f, 0f)));
            Assert.That(buffer.Count, Is.EqualTo(1));
        }

        [Test]
        public void ProjectileRegistry_ReusesReleasedProjectileObject()
        {
            var registry = new ProjectileRegistry();
            var first = registry.Spawn(EntityId.None, EntityId.None, SimulationVector2.Zero, 1, 1f, 0.1f, 1f);
            registry.Destroy(first.Id);

            var replacement = registry.Spawn(EntityId.None, EntityId.None, SimulationVector2.Zero, 2, 2f, 0.1f, 1f);

            Assert.That(replacement, Is.SameAs(first));
            Assert.That(replacement.Damage, Is.EqualTo(2));
        }
    }
}
