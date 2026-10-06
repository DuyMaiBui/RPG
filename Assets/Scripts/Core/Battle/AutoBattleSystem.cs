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
        // A crowd where every actor keeps pushing into the same blocked spot never dissolves. After this long without
        // net progress an actor steps sideways for a short window, which breaks the symmetry and lets a queue form.
        private const int RecoveryTickThreshold = 45;
        private const int RecoveryTicks = 20;
        private const float ProgressEpsilonSquared = 0.000001f;

        private readonly AnyAliveEnemyTargetSelector _targetSelector = new();
        private readonly ProjectileSystem _projectiles = new();
        private readonly SpatialHash _spatialHash = new(1f);
        private readonly OrcaAvoidanceSolver _avoidance = new();
        private readonly CombatBehaviorTree _behaviorTree = new();
        private readonly List<EntityId> _overlapNearby = new();
        private SimulationVector2[] _resolvedDirections = new SimulationVector2[0];
        private SimulationVector2[] _candidatePositions = new SimulationVector2[0];
        private SimulationVector2[] _lastProgressPositions = new SimulationVector2[0];
        private int[] _stuckTicks = new int[0];
        private int[] _recoveryTicks = new int[0];
        private bool[] _wantsToMove = new bool[0];

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

                // An order owns targeting while it lasts: OrderSystem validated and wrote the locked target this tick,
                // and a plain move order means "do not engage at all".
                if (OrderSystem.TryGetLockedTarget(actor, out var orderedTargetId))
                {
                    var orderedTargetComponent = actor.Components.Get<TargetComponent>();
                    orderedTargetComponent.CurrentTarget = orderedTargetId;
                    behavior.State = IsLiveEnemy(actors, actor, orderedTargetId, out var orderedTarget) &&
                                     IsTargetVisible(actor, orderedTarget, context.State.Navigation)
                        ? _behaviorTree.Evaluate(true, IsAttackTriggerOverlapping(actor, orderedTarget))
                        : AutoCombatState.ChaseTarget;
                    continue;
                }

                if (OrderSystem.IsMoveWithoutEngaging(actor))
                {
                    actor.Components.Get<TargetComponent>().CurrentTarget = EntityId.None;
                    behavior.State = AutoCombatState.ChaseTarget;
                    continue;
                }

                var target = actor.Components.Get<TargetComponent>();
                if (!IsLiveEnemy(actors, actor, target.CurrentTarget, out var targetActor) ||
                    !IsTargetVisible(actor, targetActor, context.State.Navigation))
                {
                    target.CurrentTarget = EntityId.None;
                    if (!_targetSelector.TrySelect(actor, actors, _spatialHash, context.State.Navigation, out var targetId))
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
                if (ActorStatus.IsDisabled(actor))
                    continue;
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
                                                 manualDirection * (movement.EffectiveSpeed * context.FixedDeltaTime);
                    continue;
                }

                if (OrderSystem.IsHolding(actor))
                    continue;

                var targetComponent = actor.Components.Get<TargetComponent>();
                var hasTarget = IsLiveEnemy(actors, actor, targetComponent.CurrentTarget, out var target) &&
                                IsTargetVisible(actor, target, context.State.Navigation);
                var hasMoveDestination = OrderSystem.TryGetMoveDestination(actor, out var order, out var orderStopDistance);
                var movesWithoutEngaging = OrderSystem.IsMoveWithoutEngaging(actor);

                SimulationVector2 destination;
                float stopDistance;
                var steersDirectly = false;
                if (hasMoveDestination && movesWithoutEngaging)
                {
                    destination = order.Destination;
                    stopDistance = orderStopDistance;
                    steersDirectly = true;
                }
                else if (hasTarget)
                {
                    destination = target.Components.Get<PositionComponent>().Position;
                    stopDistance = OrderSystem.ResolveChaseStopDistance(actor, target);
                }
                else if (hasMoveDestination)
                {
                    destination = order.Destination;
                    stopDistance = orderStopDistance;
                    steersDirectly = true;
                }
                else if (OrderSystem.TryGetLockedTarget(actor, out _))
                {
                    continue;
                }
                else
                {
                    destination = context.State.GetEnemyBasePosition(actor.Components.Get<FactionComponent>().Faction);
                    stopDistance = context.State.BaseReach;
                }

                var distance = Distance(position.Position, destination);
                if (movement.EffectiveSpeed <= 0f || distance <= stopDistance)
                    continue;

                _wantsToMove[index] = true;
                SimulationVector2 preferredDirection;
                if (steersDirectly)
                {
                    // An ordered move steers straight at its destination and leaves the rest to local avoidance.
                    // Cohort routing follows the battle objective, which is not where the player pointed.
                    preferredDirection = (destination - position.Position).Normalized();
                    if (preferredDirection.LengthSquared <= 0f)
                    {
                        _wantsToMove[index] = false;
                        continue;
                    }
                }
                else if (!context.State.MovementCohorts.TryGetDirection(actor, out preferredDirection))
                {
                    // The crowd route gave nothing (no route, or a flow field that could not resolve). Steering straight
                    // at the destination beats standing still: an actor that stops here would never be re-routed.
                    preferredDirection = (destination - position.Position).Normalized();
                    if (preferredDirection.LengthSquared <= 0f)
                    {
                        _wantsToMove[index] = false;
                        continue;
                    }
                }

                if (_recoveryTicks[index] > 0)
                {
                    _recoveryTicks[index]--;
                    preferredDirection = Sidestep(actor, preferredDirection);
                }

                var resolvedDirection = _avoidance.Solve(
                    actor,
                    preferredDirection,
                    actors,
                    _spatialHash,
                    context.FixedDeltaTime);
                var travel = SimulationMath.Min(
                    movement.EffectiveSpeed * context.FixedDeltaTime,
                    SimulationMath.Max(0f, distance - stopDistance));
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

            TrackProgress(actors);
        }

        /// <summary>Detects actors that want to move but make no headway, which is how a crowd jams: everyone pushes
        /// into the same blocked spot and the pushes cancel out. A stuck actor is given a short sidestep window so the
        /// jam can dissolve into a queue instead of standing forever. Deterministic: it depends only on tick counts
        /// and actor ids.</summary>
        private void TrackProgress(ActorRegistry actors)
        {
            for (var index = 0; index < actors.SlotCount; index++)
            {
                if (!actors.TryGetAt(index, out var actor) || actor.Components.Get<HealthComponent>().IsDead)
                {
                    _stuckTicks[index] = 0;
                    _recoveryTicks[index] = 0;
                    continue;
                }

                var position = actor.Components.Get<PositionComponent>().Position;
                if (!_wantsToMove[index])
                {
                    _stuckTicks[index] = 0;
                    _lastProgressPositions[index] = position;
                    continue;
                }

                if ((position - _lastProgressPositions[index]).LengthSquared > ProgressEpsilonSquared)
                {
                    _stuckTicks[index] = 0;
                    _lastProgressPositions[index] = position;
                    continue;
                }

                _stuckTicks[index]++;
                if (_stuckTicks[index] < RecoveryTickThreshold)
                    continue;

                _stuckTicks[index] = 0;
                _recoveryTicks[index] = RecoveryTicks;
            }
        }

        /// <summary>Steps sideways while still advancing. The side comes from the actor index so the result stays
        /// deterministic and two jammed actors tend to choose opposite sides.</summary>
        private static SimulationVector2 Sidestep(Actor actor, SimulationVector2 direction)
        {
            var side = new SimulationVector2(-direction.Y, direction.X);
            var sign = (actor.Id.Index & 1) == 0 ? 1f : -1f;
            return (direction + side * sign).Normalized();
        }

        private void EnsureMovementBuffers(int count)
        {
            if (_resolvedDirections.Length >= count)
            {
                for (var index = 0; index < count; index++)
                    _wantsToMove[index] = false;
                return;
            }

            _resolvedDirections = new SimulationVector2[count];
            _candidatePositions = new SimulationVector2[count];
            _lastProgressPositions = new SimulationVector2[count];
            _stuckTicks = new int[count];
            _recoveryTicks = new int[count];
            _wantsToMove = new bool[count];
        }

        private static void AttackActors(SimulationContext<RpgSimulationState> context)
        {
            var actors = context.State.Actors;
            for (var index = 0; index < actors.SlotCount; index++)
            {
                if (!actors.TryGetAt(index, out var attacker) || attacker.Components.Get<HealthComponent>().IsDead)
                    continue;
                if (ActorStatus.IsDisabled(attacker))
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

        /// <summary>Resolves overlap against nearby actors with a bounded, relaxed push. Resolving every overlap fully
        /// each tick would let separation — not the actor's movement — decide the final position, which is what makes
        /// a dense crowd stop moving: the forward step is a fraction of a push, so units get shoved back where they
        /// came from. The correction is therefore a fraction of each overlap and is capped per tick.</summary>
        private static SimulationVector2 ResolveOverlap(
            ActorRegistry actors,
            Actor movingActor,
            SimulationVector2 candidate,
            SpatialHash spatialHash,
            List<EntityId> nearby)
        {
            const float relaxation = 0.35f;
            const float maximumCorrection = 0.12f;

            var start = candidate;
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
                    var otherRadius = other.Components.Get<ColliderComponent>().Compound.BoundingRadius;
                    candidate = CollisionResolver.SeparateCircles(
                        candidate,
                        movingRadius,
                        otherPosition,
                        otherRadius,
                        movingActor.Id.Index < other.Id.Index ? -1f : 1f,
                        relaxation);
                }
            }

            var correction = candidate - start;
            return correction.LengthSquared > maximumCorrection * maximumCorrection
                ? start + correction.Normalized() * maximumCorrection
                : candidate;
        }

        private static bool IsLiveEnemy(ActorRegistry actors, Actor source, EntityId targetId, out Actor target)
        {
            if (!actors.TryGet(targetId, out target) || target.Components.Get<HealthComponent>().IsDead)
                return false;

            return target.Components.Get<FactionComponent>().Faction !=
                   source.Components.Get<FactionComponent>().Faction;
        }

        private static bool IsTargetVisible(Actor source, Actor target, NavigationGrid navigation)
        {
            var sourcePosition = source.Components.Get<PositionComponent>().Position;
            var targetPosition = target.Components.Get<PositionComponent>().Position;
            var difference = targetPosition - sourcePosition;
            var vision = source.Components.Get<VisionComponent>().Range +
                         source.Components.Get<BodyComponent>().Radius +
                         target.Components.Get<BodyComponent>().Radius;
            if (difference.LengthSquared > vision * vision)
                return false;

            // A wall between the two blocks the sight line even when the target is inside the vision range.
            return navigation == null || navigation.HasLineOfSight(sourcePosition, targetPosition, 0f);
        }

        private static float Distance(Actor left, Actor right)
        {
            var difference = right.Components.Get<PositionComponent>().Position -
                             left.Components.Get<PositionComponent>().Position;
            return SimulationMath.Sqrt(difference.LengthSquared);
        }

        private static float Distance(SimulationVector2 left, SimulationVector2 right)
        {
            var difference = right - left;
            return SimulationMath.Sqrt(difference.LengthSquared);
        }

        /// <summary>Centre-to-centre distance at which <paramref name="attacker"/> stops to attack
        /// <paramref name="target"/>: both bodies plus the attacker's reach. Order resolution uses it too, so an actor
        /// ordered onto a target stops at the same distance an automatic attack would.</summary>
        public static float AttackDistance(Actor attacker, Actor target) =>
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
