using RPG.Simulation.Contracts;
using RPG.Simulation.Runtime;
using RPG.Core.Projectiles;
using RPG.Core.Formations;
using RPG.Core.Navigation;

namespace RPG.Core.Actors
{
    public sealed class AutoBattleSystem
    {
        private readonly AnyAliveEnemyTargetSelector _targetSelector = new();
        private readonly ProjectileSystem _projectiles = new();
        private readonly SpatialHash _spatialHash = new(1f);
        private readonly OrcaAvoidanceSolver _avoidance = new();
        private readonly CombatBehaviorTree _behaviorTree = new();

        public void Tick(SimulationContext<RpgSimulationState> context)
        {
            _projectiles.Tick(context);
            var actors = context.State.Actors;
            _spatialHash.Rebuild(actors);
            for (var index = 0; index < actors.SlotCount; index++)
            {
                if (!actors.TryGetAt(index, out var actor)) continue;
                var health = actor.Components.Get<HealthComponent>();
                var behavior = actor.Components.Get<AutoCombatStateComponent>();
                var cooldown = actor.Components.Get<AttackCooldownComponent>();
                cooldown.Tick(context.FixedDeltaTime);

                if (health.IsDead)
                {
                    behavior.State = AutoCombatState.Dead;
                    continue;
                }

                var target = actor.Components.Get<TargetComponent>();
                if (!IsLiveEnemy(actors, actor, target.CurrentTarget, out var targetActor))
                {
                    target.CurrentTarget = EntityId.None;
                    if (!_targetSelector.TrySelect(actor, actors, _spatialHash, out var targetId))
                    {
                        behavior.State = AutoCombatState.AcquireTarget;
                        actor.Components.Get<MovementComponent>().DesiredDirection = SimulationVector2.Zero;
                        continue;
                    }

                    target.CurrentTarget = targetId;
                    actors.TryGet(targetId, out targetActor);
                }

                var distance = Distance(actor, targetActor);
                behavior.State = _behaviorTree.Evaluate(
                    true, distance <= AttackDistance(actor, targetActor));
            }

            MoveActors(context);
            AttackActors(context);
        }

        private void MoveActors(SimulationContext<RpgSimulationState> context)
        {
            var actors = context.State.Actors;
            for (var index = 0; index < actors.SlotCount; index++)
            {
                if (!actors.TryGetAt(index, out var actor) || actor.Components.Get<HealthComponent>().IsDead)
                    continue;

                var position = actor.Components.Get<PositionComponent>();
                var movement = actor.Components.Get<MovementComponent>();
                if (actor.Components.TryGet<ManualMovementComponent>(out var manualMovement) && manualMovement.IsActive)
                {
                    movement.DesiredDirection = _avoidance.Solve(
                        actor, manualMovement.Direction, actors, _spatialHash, context.FixedDeltaTime);
                    var manualTravel = movement.Speed * context.FixedDeltaTime;
                    var manualCandidate = position.Position + movement.DesiredDirection * manualTravel;
                    position.Position = context.State.Navigation.ClampInside(
                        ResolveOverlap(actors, actor, manualCandidate), MovingBodyRadius(actor));
                    continue;
                }

                var targetComponent = actor.Components.Get<TargetComponent>();
                var hasTarget = IsLiveEnemy(actors, actor, targetComponent.CurrentTarget, out var target);
                var formationPosition = SimulationVector2.Zero;
                var hasFormation = actor.Components.TryGet<FormationSlotComponent>(out var formationSlot) &&
                                   context.State.Formations.TryGetPosition(formationSlot, out formationPosition);
                if (!hasTarget && !hasFormation)
                {
                    movement.DesiredDirection = SimulationVector2.Zero;
                    continue;
                }

                var destination = hasTarget ? target.Components.Get<PositionComponent>().Position : formationPosition;
                var distance = hasTarget ? Distance(actor, target) : Distance(position.Position, destination);
                var stopDistance = hasTarget ? AttackDistance(actor, target) : 0.05f;
                if (distance <= stopDistance || movement.Speed <= 0f)
                {
                    movement.DesiredDirection = SimulationVector2.Zero;
                    continue;
                }

                var pathFollower = actor.Components.Get<PathFollowerComponent>();
                var directDirection = (destination - position.Position).Normalized();
                var direction = directDirection;
                if (context.State.Navigation.IsDirectPathWalkable(
                        position.Position, destination, MovingBodyRadius(actor)))
                {
                    pathFollower.Clear();
                }
                else
                {
                    var pathDestinationDelta = destination - pathFollower.Destination;
                    if (!pathFollower.HasPath || pathDestinationDelta.LengthSquared > 0.25f)
                    {
                        if (context.State.Pathfinder.TryFindPath(position.Position, destination, MovingBodyRadius(actor), out var path))
                            pathFollower.SetPath(destination, path);
                        else
                            pathFollower.Clear();
                    }

                    pathFollower.AdvanceIfClose(position.Position, 0.1f);
                    if (pathFollower.HasPath)
                    {
                        var pathDirection = (pathFollower.CurrentNode - position.Position).Normalized();
                        var alignment = pathDirection.X * directDirection.X + pathDirection.Y * directDirection.Y;
                        if (alignment > 0f)
                            direction = pathDirection;
                        else
                            pathFollower.Clear();
                    }
                }
                movement.DesiredDirection = _avoidance.Solve(
                    actor, direction, actors, _spatialHash, context.FixedDeltaTime);
                var travel = System.MathF.Min(movement.Speed * context.FixedDeltaTime, distance - stopDistance);
                var candidate = position.Position + movement.DesiredDirection * travel;
                position.Position = context.State.Navigation.ClampInside(
                    ResolveOverlap(actors, actor, candidate), MovingBodyRadius(actor));
            }
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
                    Distance(attacker, target) > AttackDistance(attacker, target))
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

        private static SimulationVector2 ResolveOverlap(ActorRegistry actors, Actor movingActor, SimulationVector2 candidate)
        {
            var movingBody = movingActor.Components.Get<BodyComponent>();
            for (var pass = 0; pass < 3; pass++)
            {
                for (var index = 0; index < actors.SlotCount; index++)
                {
                    if (!actors.TryGetAt(index, out var other) || other.Id == movingActor.Id ||
                        other.Components.Get<HealthComponent>().IsDead)
                        continue;

                    var otherPosition = other.Components.Get<PositionComponent>().Position;
                    var difference = candidate - otherPosition;
                    var minimumDistance = movingBody.Radius + other.Components.Get<BodyComponent>().Radius;
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

        private static float Distance(Actor left, Actor right)
        {
            var difference = right.Components.Get<PositionComponent>().Position - left.Components.Get<PositionComponent>().Position;
            return System.MathF.Sqrt(difference.LengthSquared);
        }

        private static float Distance(SimulationVector2 left, SimulationVector2 right)
        {
            var difference = right - left;
            return System.MathF.Sqrt(difference.LengthSquared);
        }

        private static float AttackDistance(Actor attacker, Actor target) =>
            attacker.Components.Get<BodyComponent>().Radius +
            target.Components.Get<BodyComponent>().Radius +
            attacker.Components.Get<AttackRangeComponent>().Reach;

        private static float MovingBodyRadius(Actor actor) => actor.Components.Get<BodyComponent>().Radius;
    }
}
