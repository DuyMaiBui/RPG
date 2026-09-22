using RPG.Simulation.Contracts;

namespace RPG.Core.Actors
{
    public readonly struct ActorSnapshot
    {
        public ActorSnapshot(
            EntityId entity,
            ActorKind kind,
            FactionId faction,
            int currentHealth,
            int maximumHealth,
            SimulationVector2 position,
            float radius,
            float attackRange,
            float visionRange,
            EntityId target,
            ActorVisualState visualState,
            AutoCombatState behaviorState)
        {
            Entity = entity;
            Kind = kind;
            Faction = faction;
            CurrentHealth = currentHealth;
            MaximumHealth = maximumHealth;
            Position = position;
            Radius = radius;
            AttackRange = attackRange;
            VisionRange = visionRange;
            Target = target;
            VisualState = visualState;
            BehaviorState = behaviorState;
        }

        public EntityId Entity { get; }
        public ActorKind Kind { get; }
        public FactionId Faction { get; }
        public int CurrentHealth { get; }
        public int MaximumHealth { get; }
        public SimulationVector2 Position { get; }
        public float Radius { get; }
        public float AttackRange { get; }
        public float VisionRange { get; }
        public EntityId Target { get; }
        public ActorVisualState VisualState { get; }
        public AutoCombatState BehaviorState { get; }
    }
}
