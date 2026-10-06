using System;
using System.Collections.Generic;
using RPG.Core.Actors;
using RPG.Simulation.Contracts;

namespace RPG.Core.Navigation
{
    public sealed class MovementCohortCoordinator
    {
        private const float RebuildInterval = 0.15f;
        private const float GroupJoinRadius = 3.5f;
        private const float GroupLeaveRadius = 5f;

        private readonly List<MovementCohort> _cohorts = new();
        private readonly List<EntityId> _nearby = new();
        private bool[] _assigned = Array.Empty<bool>();
        private int[] _previousCohorts = Array.Empty<int>();
        private float _rebuildTimer;
        private int _lastSlotCount = -1;
        private int _lastNavigationRevision = -1;

        public IReadOnlyList<MovementCohort> Cohorts => _cohorts;

        public void Update(
            ActorRegistry actors,
            SpatialHash spatialHash,
            NavigationGrid navigation,
            AStarPathfinder pathfinder,
            NavigationFlowFieldCache flowFields,
            DynamicOccupancyGrid occupancy,
            SimulationVector2 redBase,
            SimulationVector2 blueBase,
            float deltaTime)
        {
            _rebuildTimer -= deltaTime;
            if (_rebuildTimer > 0f)
            {
                for (var index = 0; index < _cohorts.Count; index++)
                {
                    var cohort = _cohorts[index];
                    cohort.UpdateAnchor(actors);
                    if (cohort.IsNextSegmentBlocked(occupancy))
                        _rebuildTimer = 0f;
                    cohort.RefreshDirection(flowFields);
                }
            }

            if (_cohorts.Count == 0 ||
                _rebuildTimer <= 0f ||
                _lastSlotCount != actors.SlotCount ||
                _lastNavigationRevision != navigation.Revision)
            {
                Rebuild(actors, spatialHash, navigation, pathfinder, flowFields, occupancy, redBase, blueBase);
                _rebuildTimer = RebuildInterval;
                return;
            }

            for (var index = 0; index < _cohorts.Count; index++)
            {
                var cohort = _cohorts[index];
                cohort.UpdateAnchor(actors);
                cohort.RefreshDirection(flowFields);
            }
        }

        public bool TryGetDirection(Actor actor, out SimulationVector2 direction)
        {
            direction = SimulationVector2.Zero;
            if (!actor.Components.TryGet<MovementCohortComponent>(out var membership) ||
                !membership.IsAssigned ||
                !TryGet(membership.CohortId, out var cohort))
                return false;

            direction = cohort.GetDirection(actor);
            return direction.LengthSquared > 0.000001f;
        }

        public bool TryGet(int id, out MovementCohort cohort)
        {
            if (id < 0 || id >= _cohorts.Count)
            {
                cohort = null;
                return false;
            }

            cohort = _cohorts[id];
            return true;
        }

        private void Rebuild(
            ActorRegistry actors,
            SpatialHash spatialHash,
            NavigationGrid navigation,
            AStarPathfinder pathfinder,
            NavigationFlowFieldCache flowFields,
            DynamicOccupancyGrid occupancy,
            SimulationVector2 redBase,
            SimulationVector2 blueBase)
        {
            EnsureCapacity(actors.SlotCount);
            Array.Clear(_assigned, 0, actors.SlotCount);
            for (var index = 0; index < actors.SlotCount; index++)
            {
                _previousCohorts[index] = -1;
                if (!actors.TryGetAt(index, out var actor) ||
                    !actor.Components.TryGet<MovementCohortComponent>(out var membership))
                    continue;

                _previousCohorts[index] = membership.CohortId;
                membership.Clear();
            }

            var previousCount = _cohorts.Count;
            var activeCount = 0;
            for (var index = 0; index < actors.SlotCount; index++)
            {
                if (_assigned[index] ||
                    !actors.TryGetAt(index, out var root) ||
                    !CanJoin(root))
                    continue;

                if (!TryGetObjective(actors, root, navigation, redBase, blueBase, out var objectiveKey, out var destination))
                    continue;

                var cohort = activeCount < previousCount
                    ? _cohorts[activeCount]
                    : new MovementCohort(activeCount, navigation.CellCount + 1);
                if (activeCount >= previousCount)
                    _cohorts.Add(cohort);
                var faction = root.Components.Get<FactionComponent>().Faction;
                var preserveRoute = activeCount < previousCount &&
                                     cohort.ContainsMember(root.Id) &&
                                     cohort.ObjectiveKey == objectiveKey &&
                                     cohort.Faction == faction &&
                                     cohort.RouteRevision == navigation.Revision;
                cohort.Prepare(objectiveKey, faction, destination, preserveRoute);
                BuildMembers(
                    actors,
                    spatialHash,
                    navigation,
                    root,
                    _previousCohorts[root.Id.Index],
                    objectiveKey,
                    redBase,
                    blueBase,
                    cohort);
                BuildRoute(actors, navigation, pathfinder, flowFields, occupancy, redBase, blueBase, cohort);
                AssignMembership(actors, cohort);
                activeCount++;
            }

            if (_cohorts.Count > activeCount)
                _cohorts.RemoveRange(activeCount, _cohorts.Count - activeCount);

            _lastSlotCount = actors.SlotCount;
            _lastNavigationRevision = navigation.Revision;
        }

        private void BuildMembers(
            ActorRegistry actors,
            SpatialHash spatialHash,
            NavigationGrid navigation,
            Actor root,
            int rootPreviousCohort,
            int objectiveKey,
            SimulationVector2 redBase,
            SimulationVector2 blueBase,
            MovementCohort cohort)
        {
            _assigned[root.Id.Index] = true;
            cohort.AddMember(root.Id);
            var rootPosition = root.Components.Get<PositionComponent>().Position;
            spatialHash.Collect(rootPosition, GroupLeaveRadius, _nearby);
            for (var nearbyIndex = 0; nearbyIndex < _nearby.Count; nearbyIndex++)
            {
                var nearbyId = _nearby[nearbyIndex];
                if (nearbyId.Index < 0 || nearbyId.Index >= _assigned.Length || _assigned[nearbyId.Index] ||
                    !actors.TryGet(nearbyId, out var nearbyActor) || !CanJoin(nearbyActor))
                    continue;

                var nearbyPosition = nearbyActor.Components.Get<PositionComponent>().Position;
                var radius = _previousCohorts[nearbyId.Index] == rootPreviousCohort && rootPreviousCohort >= 0
                    ? GroupLeaveRadius
                    : GroupJoinRadius;
                if ((nearbyPosition - rootPosition).LengthSquared > radius * radius ||
                    nearbyActor.Components.Get<FactionComponent>().Faction != cohort.Faction ||
                    !TryGetObjective(actors, nearbyActor, navigation, redBase, blueBase, out var nearbyKey, out _) ||
                    nearbyKey != objectiveKey)
                    continue;

                _assigned[nearbyId.Index] = true;
                cohort.AddMember(nearbyId);
            }
        }

        private void BuildRoute(
            ActorRegistry actors,
            NavigationGrid navigation,
            AStarPathfinder pathfinder,
            NavigationFlowFieldCache flowFields,
            DynamicOccupancyGrid occupancy,
            SimulationVector2 redBase,
            SimulationVector2 blueBase,
            MovementCohort cohort)
        {
            var destination = SimulationVector2.Zero;
            var radius = 0f;
            var objectiveCount = 0;
            for (var index = 0; index < cohort.MemberCount; index++)
            {
                if (!actors.TryGet(cohort.Members[index], out var actor)) continue;
                radius = SimulationMath.Max(radius, actor.Components.Get<ColliderComponent>().Compound.BoundingRadius);
                if (TryGetObjective(actors, actor, navigation, redBase, blueBase, out _, out var memberDestination))
                {
                    destination += memberDestination;
                    objectiveCount++;
                }
            }

            if (cohort.MemberCount == 0) return;
            cohort.UpdateAnchor(actors);
            var anchor = cohort.Anchor;
            if (objectiveCount > 0)
                destination /= objectiveCount;
            cohort.SetAnchor(anchor);
            cohort.SetDestination(destination);

            if (cohort.RouteRevision == navigation.Revision &&
                (cohort.HasRoute || cohort.UsesFlowField))
            {
                cohort.RefreshDirection(flowFields);
                return;
            }

            if (navigation.IsDirectPathWalkable(anchor, destination, radius) &&
                !occupancy.IsStationaryPathBlocked(anchor, destination))
            {
                cohort.SetDirectRoute(anchor, destination, navigation.Revision, radius);
                cohort.RefreshDirection(flowFields);
                return;
            }

            if (pathfinder.TryFindPath(anchor, destination, radius, occupancy, cohort.Route, out var pathLength))
            {
                cohort.SetRoute(destination, pathLength, navigation.Revision, radius);
                cohort.RefreshDirection(flowFields);
                return;
            }

            if (navigation.IsDirectPathWalkable(anchor, destination, radius))
            {
                cohort.SetDirectRoute(anchor, destination, navigation.Revision, radius);
                cohort.RefreshDirection(flowFields);
                return;
            }

            cohort.SetFlowFieldRoute(destination, navigation.Revision, radius);
            cohort.RefreshDirection(flowFields);
        }

        /// <summary>Joins the cohort members. There is no rank or slot assignment any more: steering every actor onto
        /// its own slot made the group march in ranks, and the crowd flows from the shared route direction plus the soft
        /// separation pass instead.</summary>
        private static void AssignMembership(ActorRegistry actors, MovementCohort cohort)
        {
            for (var index = 0; index < cohort.MemberCount; index++)
            {
                if (actors.TryGet(cohort.Members[index], out var actor) &&
                    actor.Components.TryGet<MovementCohortComponent>(out var membership))
                    membership.Assign(cohort.Id);
            }
        }

        private static bool CanJoin(Actor actor)
        {
            return !actor.Components.Get<HealthComponent>().IsDead &&
                   actor.Components.Get<MovementComponent>().Speed > 0f &&
                   (!actor.Components.TryGet<ManualMovementComponent>(out var manual) || !manual.IsActive);
        }

        private static bool TryGetObjective(
            ActorRegistry actors,
            Actor actor,
            NavigationGrid navigation,
            SimulationVector2 redBase,
            SimulationVector2 blueBase,
            out int objectiveKey,
            out SimulationVector2 destination)
        {
            var faction = actor.Components.Get<FactionComponent>().Faction;
            var target = actor.Components.Get<TargetComponent>().CurrentTarget;
            if (actors.TryGet(target, out var targetActor) &&
                !targetActor.Components.Get<HealthComponent>().IsDead &&
                targetActor.Components.Get<FactionComponent>().Faction != faction &&
                IsVisible(actor, targetActor) &&
                navigation.TryGetCoordinate(targetActor.Components.Get<PositionComponent>().Position, out var targetCell))
            {
                objectiveKey = 100000 + targetCell.Y * navigation.Width + targetCell.X;
                destination = targetActor.Components.Get<PositionComponent>().Position;
                return true;
            }

            objectiveKey = faction == FactionId.Red ? -1 : -2;
            destination = faction == FactionId.Red ? blueBase : redBase;
            return true;
        }

        private static bool IsVisible(Actor source, Actor target)
        {
            var difference = target.Components.Get<PositionComponent>().Position -
                             source.Components.Get<PositionComponent>().Position;
            var range = source.Components.Get<VisionComponent>().Range +
                        source.Components.Get<BodyComponent>().Radius +
                        target.Components.Get<BodyComponent>().Radius;
            return difference.LengthSquared <= range * range;
        }

        private void EnsureCapacity(int count)
        {
            if (_assigned.Length >= count) return;
            _assigned = new bool[count];
            _previousCohorts = new int[count];
        }
    }
}
