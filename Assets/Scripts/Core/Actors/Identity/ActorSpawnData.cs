using RPG.Simulation.Contracts;

namespace RPG.Core.Actors
{
    public readonly struct ActorSpawnData
    {
        public ActorSpawnData(
            int maximumHealth,
            int attackPower,
            SimulationVector2 position,
            float radius,
            float moveSpeed,
            float visionRange,
            float attackRange,
            float attackCooldown,
            TargetPriorityMode targetPriority = TargetPriorityMode.Nearest,
            AttackType attackType = AttackType.Melee,
            float projectileSpeed = 0f,
            float projectileRadius = 0.05f,
            float projectileLifetime = 5f,
            int formationId = -1,
            SimulationVector2 formationOffset = default)
        {
            MaximumHealth = maximumHealth;
            AttackPower = attackPower;
            Position = position;
            Radius = radius;
            MoveSpeed = moveSpeed;
            VisionRange = visionRange;
            AttackRange = attackRange;
            AttackCooldown = attackCooldown;
            TargetPriority = targetPriority;
            AttackType = attackType;
            ProjectileSpeed = projectileSpeed;
            ProjectileRadius = projectileRadius;
            ProjectileLifetime = projectileLifetime;
            FormationId = formationId;
            FormationOffset = formationOffset;
        }

        public int MaximumHealth { get; }
        public int AttackPower { get; }
        public SimulationVector2 Position { get; }
        public float Radius { get; }
        public float MoveSpeed { get; }
        public float VisionRange { get; }
        public float AttackRange { get; }
        public float AttackCooldown { get; }
        public TargetPriorityMode TargetPriority { get; }
        public AttackType AttackType { get; }
        public float ProjectileSpeed { get; }
        public float ProjectileRadius { get; }
        public float ProjectileLifetime { get; }
        public int FormationId { get; }
        public SimulationVector2 FormationOffset { get; }
    }
}
