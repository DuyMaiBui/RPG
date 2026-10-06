using RPG.Simulation.Contracts;
using RPG.Simulation.Runtime;

namespace RPG.Core.Actors
{
    /// <summary>Resolves queued orders once per tick, before the auto battle, so the battle system sees the order's
    /// intent instead of choosing for the actor. It advances an order only when it is finished: a move arrived, a
    /// locked target died. An order that is merely blocked stays current, so the actor walks into position or waits
    /// for a cooldown rather than losing the order. Casting itself belongs to <see cref="AbilitySystem"/>, which owns
    /// cooldowns and the one-cast-per-tick rule.</summary>
    public sealed class OrderSystem
    {
        /// <summary>How close an actor must get to a move destination before the order counts as arrived. About one
        /// body radius: close enough that the unit reads as standing on the ordered point, and large enough that a
        /// destination other units are crowding still completes.</summary>
        public const float ArrivalRadius = 0.35f;

        public void Tick(SimulationContext<RpgSimulationState> context)
        {
            var actors = context.State.Actors;
            for (var index = 0; index < actors.SlotCount; index++)
            {
                if (!actors.TryGetAt(index, out var actor))
                    continue;
                if (!actor.Components.TryGet<OrderQueueComponent>(out var orders) || !orders.HasOrder)
                    continue;

                if (actor.Components.Get<HealthComponent>().IsDead)
                {
                    orders.Clear();
                    continue;
                }

                Resolve(context, actor, orders);
            }
        }

        /// <summary>True when the actor's current order wants it to move toward the destination.</summary>
        public static bool TryGetMoveDestination(Actor actor, out ActorOrder order, out float stopDistance)
        {
            order = default;
            stopDistance = 0f;
            if (!actor.Components.TryGet<OrderQueueComponent>(out var orders) || !orders.HasOrder)
                return false;
            if (!orders.Current.HasDestination)
                return false;

            order = orders.Current;
            stopDistance = ArrivalRadius;
            return true;
        }

        /// <summary>True when the actor's current order forbids movement (holding position).</summary>
        public static bool IsHolding(Actor actor) =>
            actor.Components.TryGet<OrderQueueComponent>(out var orders) &&
            orders.HasOrder &&
            orders.Current.Kind == OrderKind.Hold;

        /// <summary>True when the actor's current order locks it onto a target instead of auto-selecting one.</summary>
        public static bool TryGetLockedTarget(Actor actor, out EntityId target)
        {
            target = EntityId.None;
            if (!actor.Components.TryGet<OrderQueueComponent>(out var orders) || !orders.HasOrder)
                return false;
            if (!orders.Current.HasLockedTarget)
                return false;

            target = orders.Current.Target;
            return true;
        }

        /// <summary>True when the actor's current order tells it to ignore enemies entirely.</summary>
        public static bool IsMoveWithoutEngaging(Actor actor) =>
            actor.Components.TryGet<OrderQueueComponent>(out var orders) &&
            orders.HasOrder &&
            orders.Current.Kind == OrderKind.Move;

        /// <summary>Slack between attack reach and where a chase stops, so a separation push does not leave the
        /// attacker standing just outside its own range.</summary>
        private const float ChaseStopMargin = 0.15f;

        /// <summary>How close the actor must get before its current order is satisfied: a cast order stops at the
        /// ability's own reach, so the caster arrives able to cast; a direct attack stops inside attack reach. The
        /// cast measurement matches <see cref="AbilitySystem"/> exactly, including body radii.</summary>
        public static float ResolveChaseStopDistance(Actor actor, Actor target)
        {
            if (actor.Components.TryGet<OrderQueueComponent>(out var orders) &&
                orders.HasOrder &&
                orders.Current.Kind == OrderKind.CastAbility &&
                actor.Components.TryGet<AbilityComponent>(out var abilities))
            {
                var abilityIndex = abilities.IndexOf(orders.Current.AbilityId);
                if (abilityIndex >= 0)
                {
                    var ability = abilities.GetAt(abilityIndex);
                    return ability.Range +
                           actor.Components.Get<BodyComponent>().Radius +
                           target.Components.Get<BodyComponent>().Radius;
                }
            }

            // Stop inside attack reach rather than exactly on it. Stopping on the boundary leaves the attacker a
            // separation push away from being out of range, and then it neither moves (it has arrived) nor attacks
            // (it is 0.05 too far): the measured Moba battle produced 27 basic attacks in 300 s that way.
            return SimulationMath.Max(
                ChaseStopMargin * 0.5f,
                AutoBattleSystem.AttackDistance(actor, target) - ChaseStopMargin);
        }

        private static void Resolve(SimulationContext<RpgSimulationState> context, Actor actor, OrderQueueComponent orders)
        {
            var order = orders.Current;
            switch (order.Kind)
            {
                case OrderKind.Move:
                case OrderKind.AttackMove:
                    if (HasArrived(actor, order))
                        orders.Advance();
                    break;

                case OrderKind.AttackTarget:
                case OrderKind.CastAbility:
                    if (!IsLiveVisibleTarget(context, actor, order.Target))
                    {
                        orders.Advance();
                        break;
                    }

                    actor.Components.Get<TargetComponent>().CurrentTarget = order.Target;
                    break;

                case OrderKind.Hold:
                    break;
            }
        }

        private static bool HasArrived(Actor actor, ActorOrder order)
        {
            var position = actor.Components.Get<PositionComponent>().Position;
            var difference = order.Destination - position;
            return difference.LengthSquared <= ArrivalRadius * ArrivalRadius;
        }

        private static bool IsLiveVisibleTarget(SimulationContext<RpgSimulationState> context, Actor actor, EntityId targetId)
        {
            if (targetId.IsNone)
                return false;
            if (!context.State.Actors.TryGet(targetId, out var target))
                return false;
            if (target.Components.Get<HealthComponent>().IsDead)
                return false;

            var position = actor.Components.Get<PositionComponent>().Position;
            var targetPosition = target.Components.Get<PositionComponent>().Position;
            return context.State.Navigation.HasLineOfSight(position, targetPosition, 0f);
        }
    }
}
