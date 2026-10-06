using RPG.Simulation.Contracts;

namespace RPG.Core.Actors
{
    /// <summary>One immutable order. Which fields matter depends on <see cref="Kind"/>; the unused ones keep their
    /// default so an order stays a small copyable value that can sit in a queue array.</summary>
    public readonly struct ActorOrder
    {
        private ActorOrder(OrderKind kind, SimulationVector2 destination, EntityId target, int abilityId)
        {
            Kind = kind;
            Destination = destination;
            Target = target;
            AbilityId = abilityId;
        }

        public OrderKind Kind { get; }

        public SimulationVector2 Destination { get; }

        public EntityId Target { get; }

        public int AbilityId { get; }

        /// <summary>True when the order drives movement toward <see cref="Destination"/>.</summary>
        public bool HasDestination => Kind == OrderKind.Move || Kind == OrderKind.AttackMove;

        /// <summary>True when the order locks the actor onto <see cref="Target"/> instead of auto-selecting.</summary>
        public bool HasLockedTarget => Kind == OrderKind.AttackTarget || Kind == OrderKind.CastAbility;

        public static ActorOrder Move(SimulationVector2 destination) =>
            new ActorOrder(OrderKind.Move, destination, EntityId.None, 0);

        public static ActorOrder AttackMove(SimulationVector2 destination) =>
            new ActorOrder(OrderKind.AttackMove, destination, EntityId.None, 0);

        public static ActorOrder AttackTarget(EntityId target) =>
            new ActorOrder(OrderKind.AttackTarget, SimulationVector2.Zero, target, 0);

        public static ActorOrder CastAbility(int abilityId, EntityId target) =>
            new ActorOrder(OrderKind.CastAbility, SimulationVector2.Zero, target, abilityId);

        public static ActorOrder Hold() =>
            new ActorOrder(OrderKind.Hold, SimulationVector2.Zero, EntityId.None, 0);

        public override string ToString() => Kind switch
        {
            OrderKind.Move => $"Move({Destination.X}, {Destination.Y})",
            OrderKind.AttackMove => $"AttackMove({Destination.X}, {Destination.Y})",
            OrderKind.AttackTarget => $"AttackTarget({Target.Index})",
            OrderKind.CastAbility => $"CastAbility({AbilityId} -> {Target.Index})",
            _ => Kind.ToString(),
        };
    }
}
