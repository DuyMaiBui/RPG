using System.Collections.Generic;
using RPG.Simulation.Contracts;
using RPG.Simulation.Runtime;

namespace RPG.Core.Actors
{
    /// <summary>Wires the RPG systems into the simulation host: player orders are validated in the command phase and
    /// then executed by the tick systems in a fixed order — status effects, orders, auto battle, abilities. A command
    /// that cannot be honoured is rejected with a signal instead of being applied silently.</summary>
    public sealed class RpgSimulationApplication : ISimulationApplication<RpgSimulationState>
    {
        private readonly AutoBattleSystem _autoBattle = new();
        private readonly StatusEffectSystem _statusEffects = new();
        private readonly AbilitySystem _abilities = new();
        private readonly OrderSystem _orders = new();

        void ISimulationApplication<RpgSimulationState>.BeginTick(
            SimulationContext<RpgSimulationState> context,
            SimulationTick tick) => context.State.BeginTick();

        void ISimulationApplication<RpgSimulationState>.HandleCommand(
            SimulationContext<RpgSimulationState> context,
            SessionContext session,
            in ClientCommandEnvelope command)
        {
            switch (command.Payload)
            {
                case MoveOrderCommand move:
                    HandleMove(context, session, move.Actor, move.Destination, move.Queued, engage: false);
                    break;

                case AttackMoveOrderCommand attackMove:
                    HandleMove(context, session, attackMove.Actor, attackMove.Destination, attackMove.Queued, engage: true);
                    break;

                case AttackOrderCommand attack:
                    HandleAttack(context, session, attack.Actor, attack.Target, attack.Queued);
                    break;

                case CastAbilityOrderCommand cast:
                    HandleCast(context, session, cast);
                    break;

                case HoldOrderCommand hold:
                    HandleHold(context, session, hold.Actor, hold.Queued);
                    break;

                case StopOrderCommand stop:
                    HandleStop(context, session, stop.Actor);
                    break;

                // Placeholder for the pre-order single-actor control path; replaced when the selection layer lands.
                case MoveIntentCommand moveIntent:
                    HandleMoveIntent(context, session, moveIntent);
                    break;
            }
        }

        void ISimulationApplication<RpgSimulationState>.Tick(
            SimulationContext<RpgSimulationState> context,
            SimulationTick tick)
        {
            _statusEffects.Tick(context);
            _orders.Tick(context);
            _autoBattle.Tick(context);
            _abilities.Tick(context);
        }

        void ISimulationApplication<RpgSimulationState>.HandleEvents(
            SimulationContext<RpgSimulationState> context,
            IReadOnlyList<ISimulationEvent> events)
        {
            for (var index = 0; index < events.Count; index++)
            {
                switch (events[index])
                {
                    case ActorAttackStarted attack:
                        context.State.Signals.Add(new PresentationSignal(PresentationSignalKind.AttackStarted, attack.Source, attack.Target, 0));
                        break;
                    case ActorDamaged damaged when damaged.Damage > 0:
                        context.State.Signals.Add(new PresentationSignal(PresentationSignalKind.Damaged, damaged.Target, damaged.Source, damaged.Damage));
                        break;
                    case ActorAbilityCast cast:
                        context.State.Signals.Add(new PresentationSignal(PresentationSignalKind.AbilityCast, cast.Target, cast.Source, cast.AbilityId));
                        break;
                    case ActorDied died:
                        context.State.Signals.Add(new PresentationSignal(PresentationSignalKind.Died, died.Target, died.Source, 0));
                        break;
                }
            }
        }

        ISimulationUpdate ISimulationApplication<RpgSimulationState>.CreateUpdate(
            SimulationContext<RpgSimulationState> context,
            SimulationTick tick)
            => new WorldFrameUpdate(
                context.State.Actors.CreateSnapshot(),
                context.State.Projectiles.CreateSnapshot(),
                context.State.Signals.ToArray(),
                0,
                (int)tick.Value,
                EntityId.None,
                context.State.Actors.GetBattleResult());

        private static void HandleMove(
            SimulationContext<RpgSimulationState> context,
            SessionContext session,
            EntityId actorId,
            SimulationVector2 destination,
            bool queued,
            bool engage)
        {
            if (!TryGetOwnedActor(context, session, actorId, out var actor, out var rejection))
            {
                Reject(context, actorId, rejection);
                return;
            }

            var radius = actor.Components.Get<BodyComponent>().Radius;
            if (!context.State.Navigation.IsPositionWalkable(destination, radius))
            {
                Reject(context, actorId, OrderRejection.DestinationNotWalkable);
                return;
            }

            Enqueue(context, actor, engage ? ActorOrder.AttackMove(destination) : ActorOrder.Move(destination), queued);
        }

        private static void HandleAttack(
            SimulationContext<RpgSimulationState> context,
            SessionContext session,
            EntityId actorId,
            EntityId targetId,
            bool queued)
        {
            if (!TryGetOwnedActor(context, session, actorId, out var actor, out var rejection))
            {
                Reject(context, actorId, rejection);
                return;
            }

            if (!IsLiveTarget(context, targetId))
            {
                Reject(context, actorId, OrderRejection.TargetMissing);
                return;
            }

            Enqueue(context, actor, ActorOrder.AttackTarget(targetId), queued);
        }

        private static void HandleCast(
            SimulationContext<RpgSimulationState> context,
            SessionContext session,
            CastAbilityOrderCommand cast)
        {
            if (!TryGetOwnedActor(context, session, cast.Actor, out var actor, out var rejection))
            {
                Reject(context, cast.Actor, rejection);
                return;
            }

            if (!actor.Components.TryGet<AbilityComponent>(out var abilities) || abilities.IndexOf(cast.AbilityId) < 0)
            {
                Reject(context, cast.Actor, OrderRejection.UnknownAbility);
                return;
            }

            if (!IsLiveTarget(context, cast.Target))
            {
                Reject(context, cast.Actor, OrderRejection.TargetMissing);
                return;
            }

            Enqueue(context, actor, ActorOrder.CastAbility(cast.AbilityId, cast.Target), cast.Queued);
        }

        private static void HandleHold(
            SimulationContext<RpgSimulationState> context,
            SessionContext session,
            EntityId actorId,
            bool queued)
        {
            if (!TryGetOwnedActor(context, session, actorId, out var actor, out var rejection))
            {
                Reject(context, actorId, rejection);
                return;
            }

            Enqueue(context, actor, ActorOrder.Hold(), queued);
        }

        private static void HandleStop(
            SimulationContext<RpgSimulationState> context,
            SessionContext session,
            EntityId actorId)
        {
            if (!TryGetOwnedActor(context, session, actorId, out var actor, out var rejection))
            {
                Reject(context, actorId, rejection);
                return;
            }

            actor.Components.Get<OrderQueueComponent>().Clear();
        }

        private static void HandleMoveIntent(
            SimulationContext<RpgSimulationState> context,
            SessionContext session,
            MoveIntentCommand move)
        {
            var owned = context.State.Players.ActorsOf(session.Player);
            for (var index = 0; index < owned.Count; index++)
            {
                if (!context.State.Actors.TryGet(owned[index], out var actor)) continue;
                if (!actor.Components.TryGet<ManualMovementComponent>(out var manualMovement)) continue;

                manualMovement.Direction = move.Direction.Normalized();
                return;
            }
        }

        private static bool TryGetOwnedActor(
            SimulationContext<RpgSimulationState> context,
            SessionContext session,
            EntityId actorId,
            out Actor actor,
            out OrderRejection rejection)
        {
            actor = null!;

            // Ownership first: a command for an actor this session does not own is refused without revealing whether
            // that actor exists.
            if (!context.State.Players.Owns(session.Player, actorId))
            {
                rejection = OrderRejection.PlayerDoesNotOwnActor;
                return false;
            }

            if (!context.State.Actors.TryGet(actorId, out actor))
            {
                rejection = OrderRejection.ActorMissing;
                return false;
            }

            if (actor.Components.Get<HealthComponent>().IsDead)
            {
                rejection = OrderRejection.ActorDead;
                return false;
            }

            rejection = OrderRejection.None;
            return true;
        }

        private static bool IsLiveTarget(SimulationContext<RpgSimulationState> context, EntityId targetId) =>
            !targetId.IsNone &&
            context.State.Actors.TryGet(targetId, out var target) &&
            !target.Components.Get<HealthComponent>().IsDead;

        private static void Enqueue(
            SimulationContext<RpgSimulationState> context,
            Actor actor,
            ActorOrder order,
            bool queued)
        {
            if (!actor.Components.Get<OrderQueueComponent>().Enqueue(order, !queued))
                Reject(context, actor.Id, OrderRejection.QueueFull);
        }

        private static void Reject(
            SimulationContext<RpgSimulationState> context,
            EntityId actorId,
            OrderRejection rejection) =>
            context.State.Signals.Add(new PresentationSignal(
                PresentationSignalKind.OrderRejected,
                actorId,
                EntityId.None,
                (int)rejection));
    }
}
