using System.Collections.Generic;
using RPG.Simulation.Contracts;
using RPG.Simulation.Runtime;
using RPG.Core.Navigation;
using RPG.Core.Physics;
using RPG.Core.Projectiles;

namespace RPG.Core.Actors
{
    public sealed class AutoBattleSystem
    {
        private readonly AnyAliveEnemyTargetSelector _targetSelector = new();
        private readonly ProjectileSystem _projectiles = new();
        private readonly SpatialHash _spatialHash = new(1f);
        private readonly OrcaAvoidanceSolver _avoidance = new();
        private readonly CombatBehaviorTree _behaviorTree = new();
        private readonly List<EntityId> _overlapNearby = new();
        private SimulationVector2[] _resolvedDirections = new SimulationVector2[0];
        private SimulationVector2[] _candidatePositions = new SimulationVector2[0];

        public void Tick(SimulationContext<RpgSimulationState> context)
        {
            context.State.Waves.Tick(context.State, context.FixedDeltaTime);
            _projectiles.Tick(context);
            var actors = context.State.Actors;
            _spatialHash.Rebuild(actors);
            context.State.Occupancy.Rebuild(actors);
            UpdateCombatTargets(context);
            MoveActors(context);
            AttackActors(context);
        }

        private void UpdateCombatTargets(SimulationContext<RpgSimulationState> context)
        {
            var actors = context.State.Actors;
            for (var index = 0; index < actors.SlotCount; index++)
            {
                if (!actors.TryGetAt(index, out var actor)) continue;
                var health = actor.Components.Get<HealthComponent>();
                var behavior = actor.Components.Get<AutoCombatStateComponent>();
                actor.Components.Get<AttackCooldownComponent>().Tick(context.FixedDeltaTime);

                if (health.IsDead)
                {
                    behavior.State = AutoCombatState.Dead;
                    continue;
                }

                var target = actor.Components.Get<TargetComponent>();
                if (!IsLiveEnemy(actors, actor, target.CurrentTarget, out var targetActor) ||
                    !IsTargetVisible(actor, targetActor))
                {
                    target.CurrentTarget = EntityId.None;
                    if (!_targetSelector.TrySelect(actor, actors, _spatialHash, out var targetId))
                    {
                        behavior.State = AutoCombatState.ChaseTarget;
                        continue;
                    }

                    target.CurrentTarget = targetId;
                    actors.TryGet(targetId, out targetActor);
                }

                behavior.State = _behaviorTree.Evaluate(
                    true,
                    IsAttackTriggerOverlapping(actor, targetActor));
            }
        }

        private void MoveActors(SimulationContext<RpgSimulationState> context)
        {
            var actors = context.State.Actors;
            context.State.MovementCohorts.Update(
                actors,
                _spatialHash,
                context.State.Navigation,
                context.State.Pathfinder,
                context.State.FlowFields,
                context.State.Occupancy,
                context.State.RedBasePosition,
                context.State.BlueBasePosition,
                context.FixedDeltaTime);
            EnsureMovementBuffers(actors.SlotCount);

            for (var index = 0; index < actors.SlotCount; index++)
            {
                _resolvedDirections[index] = SimulationVector2.Zero;
                if (!actors.TryGetAt(index, out var actor) || actor.Components.Get<HealthComponent>().IsDead)
                    continue;

                var position = actor.Components.Get<PositionComponent>();
                var movement = actor.Components.Get<MovementComponent>();
                _candidatePositions[index] = position.Position;
                if (actor.Components.TryGet<ManualMovementComponent>(out var manualMovement) && manualMovement.IsActive)
                {
                    var manualDirection = _avoidance.Solve(
                        actor,
                        manualMovement.Direction,
                        actors,
                        _spatialHash,
                        context.FixedDeltaTime);
                    _resolvedDirections[index] = manualDirection;
                    _candidatePositions[index] = position.Position +
                                                 manualDirection * (movement.Speed * context.FixedDeltaTime);
                    continue;
                }

                var targetComponent = actor.Components.Get<TargetComponent>();
                var hasTarget = IsLiveEnemy(actors, actor, targetComponent.CurrentTarget, out var target) &&
                                IsTargetVisible(actor, target);
                var destination = hasTarget
                    ? target.Components.Get<PositionComponent>().Position
                    : context.State.GetEnemyBasePosition(actor.Components.Get<FactionComponent>().Faction);
                var distance = hasTarget ? Distance(actor, target) : Distance(position.Position, destination);
                var stopDistance = hasTarget ? AttackDistance(actor, target) : context.State.BaseReach;
                if (movement.Speed <= 0f || distance <= stopDistance)
                    continue;

                if (!context.State.MovementCohorts.TryGetDirection(actor, out var preferredDirection))
                    continue;

                var resolvedDirection = _avoidance.Solve(
                    actor,
                    preferredDirection,
                    actors,
                    _spatialHash,
                    context.FixedDeltaTime);
                var travel = System.MathF.Min(
                    movement.Speed * context.FixedDeltaTime,
                    System.MathF.Max(0f, distance - stopDistance));
                _resolvedDirections[index] = resolvedDirection;
                _candidatePositions[index] = position.Position + resolvedDirection * travel;
            }

            for (var index = 0; index < actors.SlotCount; index++)
            {
                if (!actors.TryGetAt(index, out var actor) || actor.Components.Get<HealthComponent>().IsDead)
                    continue;

                var position = actor.Components.Get<PositionComponent>();
                var movement = actor.Components.Get<MovementComponent>();
                movement.DesiredDirection = _resolvedDirections[index];
                var radius = MovingBodyRadius(actor);
                var resolvedCandidate = ResolveOverlap(
                    actors,
                    actor,
                    _candidatePositions[index],
                    _spatialHash,
                    _overlapNearby);
                position.Position = context.State.Navigation.ClampInside(
                    context.State.Navigation.ResolveMovement(position.Position, resolvedCandidate, radius), radius);
            }
        }

        private void EnsureMovementBuffers(int count)
        {
            if (_resolvedDirections.Length >= count)
                return;

            _resolvedDirections = new SimulationVector2[count];
            _candidatePositions = new SimulationVector2[count];
        }

        private static void AttackActors(SimulationContext<RpgSimulationState> context)
        {
            var actors = context.State.Actors;
            for (var index = 0; index < actors.SlotCount; index++)
            {
                if (!actors.TryGetAt(index, out var attacker) || attacker.Components.Get<HealthComponent>().IsDead)
                    continue;

                var targetId = attacker.Components.Get<TargetComponent>().CurrentTarget;
                if (!IsLiveEnemy(actors, attacker, targetId, out var target) ||
                    !IsAttackTriggerOverlapping(attacker, target))
                    continue;

                var cooldown = attacker.Components.Get<AttackCooldownComponent>();
                if (!cooldown.IsReady) continue;

                cooldown.Consume();
                context.Publish(new ActorAttackStarted(attacker.Id, target.Id));
                if (attacker.Components.TryGet<ProjectileWeaponComponent>(out var weapon))
                {
                    context.State.Projectiles.Spawn(
                        attacker.Id,
                        target.Id,
                        attacker.Components.Get<PositionComponent>().Position,
                        attacker.Components.Get<AttackComponent>().AttackPower,
                        weapon.Speed,
                        weapon.Radius,
                        weapon.Lifetime);
                    continue;
                }

                var damage = target.Components.Get<HealthComponent>().ReceiveDamage(
                    attacker.Components.Get<AttackComponent>().AttackPower);
                if (damage <= 0) continue;

                context.Publish(new ActorDamaged(attacker.Id, target.Id, damage));
                if (!target.Components.Get<HealthComponent>().IsDead) continue;

                target.Components.Get<AutoCombatStateComponent>().State = AutoCombatState.Dead;
                context.Publish(new ActorDied(attacker.Id, target.Id));
                context.Defer(state => state.Actors.Destroy(target.Id));
            }
        }

        private static SimulationVector2 ResolveOverlap(
            ActorRegistry actors,
            Actor movingActor,
            SimulationVector2 candidate,
            SpatialHash spatialHash,
            List<EntityId> nearby)
        {
            var movingRadius = movingActor.Components.Get<ColliderComponent>().Compound.BoundingRadius;
            spatialHash.Collect(candidate, movingRadius + 1f, nearby);
            for (var pass = 0; pass < 3; pass++)
            {
                for (var index = 0; index < nearby.Count; index++)
                {
                    if (!actors.TryGet(nearby[index], out var other) || other.Id == movingActor.Id ||
                        other.Components.Get<HealthComponent>().IsDead)
                        continue;

                    var otherPosition = other.Components.Get<PositionComponent>().Position;
                    var difference = candidate - otherPosition;
                    var minimumDistance = movingRadius + other.Components.Get<ColliderComponent>().Compound.BoundingRadius;
                    var distanceSquared = difference.LengthSquared;
                    if (distanceSquared >= minimumDistance * minimumDistance)
                        continue;

                    var direction = distanceSquared <= 0.000001f
                        ? new SimulationVector2(movingActor.Id.Index < other.Id.Index ? -1f : 1f, 0f)
                        : difference.Normalized();
                    candidate = otherPosition + direction * minimumDistance;
                }
            }

            return candidate;
        }

        private static bool IsLiveEnemy(ActorRegistry actors, Actor source, EntityId targetId, out Actor target)
        {
            if (!actors.TryGet(targetId, out target) || target.Components.Get<HealthComponent>().IsDead)
                return false;

            return target.Components.Get<FactionComponent>().Faction !=
                   source.Components.Get<FactionComponent>().Faction;
        }

        private static bool IsTargetVisible(Actor source, Actor target)
        {
            var difference = target.Components.Get<PositionComponent>().Position -
                             source.Components.Get<PositionComponent>().Position;
            var vision = source.Components.Get<VisionComponent>().Range +
                         source.Components.Get<BodyComponent>().Radius +
                         target.Components.Get<BodyComponent>().Radius;
            return difference.LengthSquared <= vision * vision;
        }

        private static float Distance(Actor left, Actor right)
        {
            var difference = right.Components.Get<PositionComponent>().Position -
                             left.Components.Get<PositionComponent>().Position;
            return System.MathF.Sqrt(difference.LengthSquared);
        }

        private static float Distance(SimulationVector2 left, SimulationVector2 right)
        {
            var difference = right - left;
            return System.MathF.Sqrt(difference.LengthSquared);
        }

        private static float AttackDistance(Actor attacker, Actor target) =>
            attacker.Components.Get<ColliderComponent>().Compound.BoundingRadius +
            target.Components.Get<ColliderComponent>().Compound.BoundingRadius +
            attacker.Components.Get<AttackRangeComponent>().Reach;

        private static bool IsAttackTriggerOverlapping(Actor attacker, Actor target)
        {
            var attackerCollider = attacker.Components.Get<ColliderComponent>().Compound;
            for (var index = 0; index < attackerCollider.Count; index++)
            {
                var shape = attackerCollider.GetAt(index);
                if (shape.Mode == ColliderMode.Solid &&
                    target.Components.Get<ColliderComponent>().Compound.HasInteraction(ColliderMode.Solid, shape.Filter) &&
                    Distance(attacker, target) <= AttackDistance(attacker, target))
                    return true;
            }

            return false;
        }

        private static float MovingBodyRadius(Actor actor) =>
            actor.Components.Get<ColliderComponent>().Compound.BoundingRadius;
    }
}
