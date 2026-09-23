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
        public void NavigationObstacle_BlocksItsCoveredCellsAndCanBeReset()
        {
            var grid = new NavigationGrid(4, 4, 1f, SimulationVector2.Zero);
            grid.ApplyObstacle(new NavigationObstacle(
                3, new SimulationVector2(1.5f, 1.5f), new SimulationVector2(0.4f, 0.4f)));

            Assert.That(grid.IsWalkable(new GridCoordinate(1, 1)), Is.False);
            Assert.That(grid.Revision, Is.EqualTo(1));

            grid.ResetObstacles();

            Assert.That(grid.IsWalkable(new GridCoordinate(1, 1)), Is.True);
            Assert.That(grid.Revision, Is.EqualTo(2));
        }

        [Test]
        public void NavigationGrid_UsesDirectPathWhenNoObstacleBlocksIt()
        {
            var grid = new NavigationGrid(8, 8, 1f, new SimulationVector2(-4f, -4f));

            Assert.That(grid.IsDirectPathWalkable(
                new SimulationVector2(-2f, 0f),
                new SimulationVector2(2f, 0f),
                0.25f), Is.True);
        }

        [Test]
        public void NavigationGrid_RejectsDirectPathThroughBlockedCell()
        {
            var grid = new NavigationGrid(8, 8, 1f, new SimulationVector2(-4f, -4f));
            grid.ApplyObstacle(new NavigationObstacle(
                1, new SimulationVector2(0f, 0f), new SimulationVector2(0.4f, 0.4f)));

            Assert.That(grid.IsDirectPathWalkable(
                new SimulationVector2(-2f, 0f),
                new SimulationVector2(2f, 0f),
                0.25f), Is.False);
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
        public void FormationCoordinator_ReassignsSlotsByNearestPosition()
        {
            var actors = new ActorRegistry();
            var leftId = actors.Spawn(ActorKind.Player, FactionId.Red, new ActorSpawnData(
                10, 1, new SimulationVector2(-0.8f, 0f), 0.3f, 0f, 3f, 1f, 1f, formationId: 4));
            var rightId = actors.Spawn(ActorKind.Player, FactionId.Red, new ActorSpawnData(
                10, 1, new SimulationVector2(0.8f, 0f), 0.3f, 0f, 3f, 1f, 1f, formationId: 4));
            var coordinator = new FormationCoordinator();
            coordinator.RegisterLayout(4, SimulationVector2.Zero, new[]
            {
                new SimulationVector2(-1f, 0f), new SimulationVector2(1f, 0f),
            });

            coordinator.ReassignSlots(actors, 4);

            Assert.That(actors.TryGet(leftId, out var left), Is.True);
            Assert.That(actors.TryGet(rightId, out var right), Is.True);
            Assert.That(left.Components.Get<FormationSlotComponent>().LocalOffset,
                Is.EqualTo(new SimulationVector2(-1f, 0f)));
            Assert.That(right.Components.Get<FormationSlotComponent>().LocalOffset,
                Is.EqualTo(new SimulationVector2(1f, 0f)));
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

        [Test]
        public void BehaviorTree_SequenceAndSelectorKeepExpectedStatus()
        {
            var tree = new BehaviorTree<int>(new BehaviorSelector<int>(
                new BehaviorSequence<int>(
                    new BehaviorAction<int>(_ => BehaviorStatus.Failed)),
                new BehaviorAction<int>(_ => BehaviorStatus.Succeeded)));

            Assert.That(tree.Tick(0), Is.EqualTo(BehaviorStatus.Succeeded));
        }

        [Test]
        public void BehaviorTree_ConditionInverterAndParallelComposeWithoutStateLeak()
        {
            var tree = new BehaviorTree<int>(new BehaviorParallel<int>(
                new BehaviorCondition<int>(value => value > 0),
                new BehaviorInverter<int>(new BehaviorCondition<int>(value => value < 0))));

            Assert.That(tree.Tick(1), Is.EqualTo(BehaviorStatus.Succeeded));
            Assert.That(tree.Tick(-1), Is.EqualTo(BehaviorStatus.Failed));
        }

        [Test]
        public void Avoidance_SteersHeadOnActorsAwayFromCollision()
        {
            var registry = new ActorRegistry();
            var leftId = registry.Spawn(ActorKind.Player, FactionId.Red, new ActorSpawnData(
                10, 1, new SimulationVector2(-0.4f, 0f), 0.3f, 1f, 3f, 1f, 1f));
            var rightId = registry.Spawn(ActorKind.Monster, FactionId.Blue, new ActorSpawnData(
                10, 1, new SimulationVector2(0.4f, 0f), 0.3f, 1f, 3f, 1f, 1f));
            Assert.That(registry.TryGet(leftId, out var left), Is.True);
            Assert.That(registry.TryGet(rightId, out var right), Is.True);

            left.Components.Get<MovementComponent>().DesiredDirection = new SimulationVector2(1f, 0f);
            right.Components.Get<MovementComponent>().DesiredDirection = new SimulationVector2(-1f, 0f);
            var hash = new SpatialHash(1f);
            hash.Rebuild(registry);

            var direction = new OrcaAvoidanceSolver().Solve(
                left, new SimulationVector2(1f, 0f), registry, hash);

            Assert.That(direction.X, Is.LessThan(0.99f));
            Assert.That(direction.LengthSquared, Is.GreaterThan(0.9f));
        }

        [Test]
        public void Avoidance_PreservesPreferredDirectionForNearbyNonCollidingActor()
        {
            var registry = new ActorRegistry();
            var actorId = registry.Spawn(ActorKind.Player, FactionId.Red, new ActorSpawnData(
                10, 1, SimulationVector2.Zero, 0.3f, 1f, 3f, 1f, 1f));
            var nearbyId = registry.Spawn(ActorKind.Monster, FactionId.Blue, new ActorSpawnData(
                10, 1, new SimulationVector2(0f, 1.2f), 0.3f, 0f, 3f, 1f, 1f));
            Assert.That(registry.TryGet(actorId, out var actor), Is.True);
            Assert.That(registry.TryGet(nearbyId, out _), Is.True);

            var hash = new SpatialHash(1f);
            hash.Rebuild(registry);

            var direction = new OrcaAvoidanceSolver().Solve(
                actor, new SimulationVector2(1f, 0f), registry, hash);

            Assert.That(direction.X, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(direction.Y, Is.EqualTo(0f).Within(0.0001f));
        }
    }
}
