using NUnit.Framework;
using RPG.Core.Actors;
using RPG.Core.Formations;
using RPG.Core.Navigation;
using RPG.Core.Physics;
using RPG.Core.Projectiles;
using RPG.Simulation.Contracts;
using RPG.Simulation.Runtime;

namespace RPG.Core.Tests
{
    public sealed class AutonomousCombatTests
    {
        [Test]
        public void MovementCohorts_GroupNearbyActorsAndKeepDistantActorsSeparate()
        {
            var navigation = new NavigationGrid(20, 20, 1f, new SimulationVector2(-10f, -10f));
            var actors = new ActorRegistry();
            var first = actors.Spawn(ActorKind.Monster, FactionId.Red, new ActorSpawnData(
                10, 1, new SimulationVector2(-4f, 0f), 0.3f, 1f, 2f, 0.5f, 1f));
            var second = actors.Spawn(ActorKind.Monster, FactionId.Red, new ActorSpawnData(
                10, 1, new SimulationVector2(-4f, 0.8f), 0.3f, 1f, 2f, 0.5f, 1f));
            var distant = actors.Spawn(ActorKind.Monster, FactionId.Red, new ActorSpawnData(
                10, 1, new SimulationVector2(-4f, 6f), 0.3f, 1f, 2f, 0.5f, 1f));
            var spatialHash = new SpatialHash(1f);
            var occupancy = new DynamicOccupancyGrid(navigation);
            spatialHash.Rebuild(actors);
            occupancy.Rebuild(actors);
            var coordinator = new MovementCohortCoordinator();

            coordinator.Update(
                actors,
                spatialHash,
                navigation,
                new AStarPathfinder(navigation),
                new NavigationFlowFieldCache(navigation),
                occupancy,
                new SimulationVector2(-8f, 0f),
                new SimulationVector2(8f, 0f),
                0.2f);

            Assert.That(actors.TryGet(first, out var firstActor), Is.True);
            Assert.That(actors.TryGet(second, out var secondActor), Is.True);
            Assert.That(actors.TryGet(distant, out var distantActor), Is.True);
            var firstMembership = firstActor.Components.Get<MovementCohortComponent>();
            var secondMembership = secondActor.Components.Get<MovementCohortComponent>();
            var distantMembership = distantActor.Components.Get<MovementCohortComponent>();
            Assert.That(firstMembership.CohortId, Is.EqualTo(secondMembership.CohortId));
            Assert.That(firstMembership.CohortId, Is.Not.EqualTo(distantMembership.CohortId));
            Assert.That(coordinator.TryGetDirection(firstActor, out _), Is.True);
            Assert.That(coordinator.TryGetDirection(secondActor, out _), Is.True);
        }

        [Test]
        public void MovementCohort_UsesSharedObstacleRouteForMembers()
        {
            var navigation = new NavigationGrid(16, 10, 1f, new SimulationVector2(-8f, -5f));
            navigation.ApplyObstacle(new NavigationObstacle(
                1,
                new SimulationVector2(0f, 0f),
                CollisionShape.Box(new SimulationVector2(0.4f, 1.5f))));
            var actors = new ActorRegistry();
            var first = actors.Spawn(ActorKind.Monster, FactionId.Red, new ActorSpawnData(
                10, 0, new SimulationVector2(-4f, -0.35f), 0.3f, 1f, 2f, 0.5f, 1f));
            var second = actors.Spawn(ActorKind.Monster, FactionId.Red, new ActorSpawnData(
                10, 0, new SimulationVector2(-4f, 0.35f), 0.3f, 1f, 2f, 0.5f, 1f));
            var spatialHash = new SpatialHash(1f);
            var occupancy = new DynamicOccupancyGrid(navigation);
            spatialHash.Rebuild(actors);
            occupancy.Rebuild(actors);
            var coordinator = new MovementCohortCoordinator();

            coordinator.Update(
                actors,
                spatialHash,
                navigation,
                new AStarPathfinder(navigation),
                new NavigationFlowFieldCache(navigation),
                occupancy,
                new SimulationVector2(-6f, 0f),
                new SimulationVector2(6f, 0f),
                0.2f);

            var firstActor = actors.TryGet(first, out var firstValue) ? firstValue : null;
            var secondActor = actors.TryGet(second, out var secondValue) ? secondValue : null;
            Assert.That(firstActor, Is.Not.Null);
            Assert.That(secondActor, Is.Not.Null);
            var firstMembership = firstActor.Components.Get<MovementCohortComponent>();
            var secondMembership = secondActor.Components.Get<MovementCohortComponent>();
            Assert.That(firstMembership.CohortId, Is.EqualTo(secondMembership.CohortId));
            Assert.That(coordinator.TryGet(firstMembership.CohortId, out var cohort), Is.True);
            Assert.That(cohort.RouteLength, Is.GreaterThan(2));
            Assert.That(cohort.NextWaypoint, Is.LessThan(cohort.RouteLength));
        }

        [Test]
        public void MovementCohort_GroupsMeleeAndRangedMembersTogether()
        {
            var navigation = new NavigationGrid(20, 20, 1f, new SimulationVector2(-10f, -10f));
            var actors = new ActorRegistry();
            var melee = actors.Spawn(ActorKind.Monster, FactionId.Red, new ActorSpawnData(
                10, 1, new SimulationVector2(-4f, 0f), 0.3f, 1f, 2f, 0.5f, 1f));
            var ranged = actors.Spawn(ActorKind.Monster, FactionId.Red, new ActorSpawnData(
                10, 1, new SimulationVector2(-4f, 0.8f), 0.3f, 1f, 2f, 3.5f, 1f,
                attackType: AttackType.Projectile, projectileSpeed: 5f));
            var spatialHash = new SpatialHash(1f);
            var occupancy = new DynamicOccupancyGrid(navigation);
            spatialHash.Rebuild(actors);
            occupancy.Rebuild(actors);
            var coordinator = new MovementCohortCoordinator();

            coordinator.Update(
                actors,
                spatialHash,
                navigation,
                new AStarPathfinder(navigation),
                new NavigationFlowFieldCache(navigation),
                occupancy,
                new SimulationVector2(-8f, 0f),
                new SimulationVector2(8f, 0f),
                0.2f);

            Assert.That(actors.TryGet(melee, out var meleeActor), Is.True);
            Assert.That(actors.TryGet(ranged, out var rangedActor), Is.True);
            var meleeMembership = meleeActor.Components.Get<MovementCohortComponent>();
            var rangedMembership = rangedActor.Components.Get<MovementCohortComponent>();
            Assert.That(meleeMembership.CohortId, Is.EqualTo(rangedMembership.CohortId));
            Assert.That(meleeMembership.IsAssigned, Is.True);
            Assert.That(rangedMembership.IsAssigned, Is.True);
        }

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
        public void FlowField_NoObstacle_PointsTowardTarget()
        {
            var grid = new NavigationGrid(8, 8, 1f, SimulationVector2.Zero);
            var cache = new NavigationFlowFieldCache(grid);

            Assert.That(cache.TryGetDirection(
                new SimulationVector2(0.5f, 0.5f),
                new SimulationVector2(6.5f, 0.5f),
                0.25f,
                out var direction), Is.True);
            Assert.That(direction.X, Is.GreaterThan(0.9f));
            Assert.That(System.MathF.Abs(direction.Y), Is.LessThan(0.1f));
        }

        [Test]
        public void FlowField_StaticWallRoutesThroughOpenSide()
        {
            var blocked = new bool[25];
            blocked[2 + 2 * 5] = true;
            blocked[2 + 1 * 5] = true;
            blocked[2 + 3 * 5] = true;
            var grid = new NavigationGrid(5, 5, 1f, SimulationVector2.Zero, blocked);
            var cache = new NavigationFlowFieldCache(grid);

            Assert.That(cache.TryGetDirection(
                new SimulationVector2(0.5f, 2.5f),
                new SimulationVector2(4.5f, 2.5f),
                0.25f,
                out var direction), Is.True);
            Assert.That(System.MathF.Abs(direction.Y), Is.GreaterThan(0.1f));
        }

        [Test]
        public void FlowField_DoesNotCutBlockedDiagonalCorner()
        {
            var blocked = new bool[9];
            blocked[1] = true;
            blocked[3] = true;
            var grid = new NavigationGrid(3, 3, 1f, SimulationVector2.Zero, blocked);
            var cache = new NavigationFlowFieldCache(grid);

            Assert.That(cache.TryGetDirection(
                new SimulationVector2(0.5f, 0.5f),
                new SimulationVector2(2.5f, 2.5f),
                0.25f,
                out _), Is.False);
        }

        [Test]
        public void DynamicOccupancy_EstimatesWaitFromLocalCrowd()
        {
            var grid = new NavigationGrid(4, 4, 1f, SimulationVector2.Zero);
            var actors = new ActorRegistry();
            actors.Spawn(ActorKind.Monster, FactionId.Red, new ActorSpawnData(
                10, 1, new SimulationVector2(1.5f, 1.5f), 0.3f, 2f, 5f, 0.5f, 1f));
            actors.Spawn(ActorKind.Monster, FactionId.Red, new ActorSpawnData(
                10, 1, new SimulationVector2(1.5f, 1.5f), 0.3f, 2f, 5f, 0.5f, 1f));
            var occupancy = new DynamicOccupancyGrid(grid);
            occupancy.Rebuild(actors);

            Assert.That(occupancy.GetEstimatedWait(new SimulationVector2(1.5f, 1.5f), 2f), Is.EqualTo(0.5f));
        }

        [Test]
        public void DynamicOccupancy_DoesNotChangeStaticWalkability()
        {
            var grid = new NavigationGrid(4, 4, 1f, SimulationVector2.Zero);
            var actors = new ActorRegistry();
            actors.Spawn(ActorKind.Monster, FactionId.Red, new ActorSpawnData(
                10, 1, new SimulationVector2(1.5f, 1.5f), 0.3f, 0f, 5f, 0.5f, 1f));
            var occupancy = new DynamicOccupancyGrid(grid);
            occupancy.Rebuild(actors);

            Assert.That(grid.IsWalkable(new GridCoordinate(1, 1)), Is.True);
            Assert.That(occupancy.GetCount(new SimulationVector2(1.5f, 1.5f)), Is.EqualTo(1));
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
        public void CircleObstacle_BlocksOnlyCellsOverlappingItsShape()
        {
            var grid = new NavigationGrid(5, 5, 1f, SimulationVector2.Zero);
            grid.ApplyObstacle(new NavigationObstacle(
                4,
                new SimulationVector2(2.5f, 2.5f),
                CollisionShape.Circle(0.4f)));

            Assert.That(grid.IsWalkable(new GridCoordinate(2, 2)), Is.False);
            Assert.That(grid.IsWalkable(new GridCoordinate(1, 2)), Is.True);
        }

        [Test]
        public void CollisionShapeQueries_ResolveTriggerOverlapWithoutUnityPhysics()
        {
            var circle = CollisionShape.Circle(0.5f);
            var box = CollisionShape.Box(new SimulationVector2(0.5f, 0.25f));

            Assert.That(CollisionShapeQueries.Overlaps(
                circle, SimulationVector2.Zero, box, new SimulationVector2(0.75f, 0f)), Is.True);
            Assert.That(CollisionShapeQueries.Overlaps(
                circle, SimulationVector2.Zero, box, new SimulationVector2(2f, 0f)), Is.False);
        }

        [Test]
        public void CollisionShapeQueries_RotatedBoxUsesOrientedBounds()
        {
            var box = CollisionShape.Box(new SimulationVector2(1f, 0.2f), System.MathF.PI * 0.25f);
            var circle = CollisionShape.Circle(0.1f);

            Assert.That(CollisionShapeQueries.Overlaps(
                box, SimulationVector2.Zero, circle, new SimulationVector2(0.6f, 0.8f)), Is.True);
        }

        [Test]
        public void PolygonObstacle_RasterizesOnlyOverlappingCells()
        {
            var grid = new NavigationGrid(5, 5, 1f, SimulationVector2.Zero);
            grid.ApplyObstacle(new NavigationObstacle(
                5,
                new SimulationVector2(2.5f, 2.5f),
                CollisionShape.Polygon(new[]
                {
                    new SimulationVector2(-0.5f, -0.5f),
                    new SimulationVector2(0.5f, -0.5f),
                    new SimulationVector2(0f, 0.5f),
                })));

            Assert.That(grid.IsWalkable(new GridCoordinate(2, 2)), Is.False);
            Assert.That(grid.IsWalkable(new GridCoordinate(1, 2)), Is.True);
        }

        [Test]
        public void ColliderCompound_QueriesOffsetShapesAndFiltersByMode()
        {
            var compound = new ColliderCompound(new[]
            {
                new ColliderShapeData(
                    CollisionShape.Circle(0.25f),
                    new SimulationVector2(1f, 0f),
                    0f,
                    ColliderMode.Solid,
                    new ColliderFilter(0, -1)),
                new ColliderShapeData(
                    CollisionShape.Box(new SimulationVector2(0.25f, 0.1f)),
                    new SimulationVector2(-1f, 0f),
                    0f,
                    ColliderMode.Trigger,
                    new ColliderFilter(1, -1)),
            });

            Assert.That(compound.Overlaps(
                SimulationVector2.Zero,
                0f,
                CollisionShape.Circle(0.2f),
                new SimulationVector2(1.2f, 0f),
                ColliderMode.Solid,
                new ColliderFilter(0, -1)), Is.True);
            Assert.That(compound.Overlaps(
                SimulationVector2.Zero,
                0f,
                CollisionShape.Circle(0.2f),
                new SimulationVector2(-1f, 0f),
                ColliderMode.Solid,
                new ColliderFilter(0, -1)), Is.False);
        }

        [Test]
        public void ColliderCompound_AttackQueryCanUseNonDefaultLayerFilters()
        {
            var attacker = new ColliderCompound(new[]
            {
                new ColliderShapeData(
                    CollisionShape.Circle(0.35f),
                    SimulationVector2.Zero,
                    0f,
                    ColliderMode.Solid,
                    new ColliderFilter(1, 1 << 2)),
            });
            var target = new ColliderCompound(new[]
            {
                new ColliderShapeData(
                    CollisionShape.Circle(0.35f),
                    SimulationVector2.Zero,
                    0f,
                    ColliderMode.Solid,
                    new ColliderFilter(2, 1 << 1)),
            });

            Assert.That(target.Overlaps(
                new SimulationVector2(0.8f, 0f),
                0f,
                CollisionShape.Circle(attacker.BoundingRadius + 0.25f),
                SimulationVector2.Zero,
                ColliderMode.Solid,
                attacker.GetAt(0).Filter), Is.True);
            Assert.That(target.HasInteraction(ColliderMode.Solid, attacker.GetAt(0).Filter), Is.True);
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
        public void NavigationGrid_StopsMovementBeforeSolidObstacle()
        {
            var grid = new NavigationGrid(8, 4, 1f, new SimulationVector2(-4f, -2f));
            grid.ApplyObstacle(new NavigationObstacle(
                6,
                new SimulationVector2(0f, 0f),
                CollisionShape.Box(new SimulationVector2(0.4f, 1f))));

            var resolved = grid.ResolveMovement(
                new SimulationVector2(-2f, 0f),
                new SimulationVector2(2f, 0f),
                0.25f);

            Assert.That(resolved.X, Is.LessThan(-0.6f));
            Assert.That(grid.IsPositionWalkable(resolved, 0.25f), Is.True);
            Assert.That(grid.IsPositionWalkable(new SimulationVector2(0f, 0f), 0.25f), Is.False);
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
        public void ActorSnapshot_PreservesMeleeAndProjectileAttackTypes()
        {
            var registry = new ActorRegistry();
            registry.Spawn(ActorKind.Player, FactionId.Red, new ActorSpawnData(
                10, 1, SimulationVector2.Zero, 0.35f, 0.8f, 6f, 0.25f, 0.8f));
            registry.Spawn(ActorKind.Monster, FactionId.Blue, new ActorSpawnData(
                10, 1, new SimulationVector2(2f, 0f), 0.35f, 0.8f, 6f, 3.5f, 0.8f,
                attackType: AttackType.Projectile, projectileSpeed: 5f, projectileRadius: 0.05f, projectileLifetime: 5f));

            var snapshots = registry.CreateSnapshot();

            Assert.That(snapshots[0].AttackType, Is.EqualTo(AttackType.Melee));
            Assert.That(snapshots[0].AttackRange, Is.EqualTo(0.25f));
            Assert.That(snapshots[1].AttackType, Is.EqualTo(AttackType.Projectile));
            Assert.That(snapshots[1].AttackRange, Is.EqualTo(3.5f));
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
        public void Avoidance_KeepsPreferredDirectionForSameFactionActor()
        {
            // Own side is not an obstacle: reciprocal avoidance between allies is what makes a crowd queue and shuffle,
            // so the solver leaves allies out and the soft separation pass keeps them from stacking instead. See
            // Docs/RPG-Combat-Plan.md (C6).
            var registry = new ActorRegistry();
            var leftId = registry.Spawn(ActorKind.Player, FactionId.Red, new ActorSpawnData(
                10, 1, new SimulationVector2(-0.4f, 0f), 0.3f, 1f, 3f, 1f, 1f));
            var rightId = registry.Spawn(ActorKind.Player, FactionId.Red, new ActorSpawnData(
                10, 1, new SimulationVector2(0.4f, 0f), 0.3f, 1f, 3f, 1f, 1f));
            Assert.That(registry.TryGet(leftId, out var left), Is.True);
            Assert.That(registry.TryGet(rightId, out var right), Is.True);

            left.Components.Get<MovementComponent>().DesiredDirection = new SimulationVector2(1f, 0f);
            right.Components.Get<MovementComponent>().DesiredDirection = new SimulationVector2(-1f, 0f);
            var hash = new SpatialHash(1f);
            hash.Rebuild(registry);

            var direction = new OrcaAvoidanceSolver().Solve(
                left, new SimulationVector2(1f, 0f), registry, hash);

            Assert.That(direction.X, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(direction.Y, Is.EqualTo(0f).Within(0.0001f));
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
