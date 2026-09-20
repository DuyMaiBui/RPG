using System;
using RPG.Simulation.Contracts;

namespace RPG.Core.Actors
{
    public sealed class Actor
    {
        public Actor(EntityId id, ActorKind kind, int maximumHealth, int attackPower)
            : this(id, kind, kind == ActorKind.Player ? FactionId.Red : FactionId.Blue, maximumHealth, attackPower)
        {
        }

        public Actor(EntityId id, ActorKind kind, FactionId faction, int maximumHealth, int attackPower)
        {
            if (maximumHealth <= 0) throw new ArgumentOutOfRangeException(nameof(maximumHealth));
            if (attackPower < 0) throw new ArgumentOutOfRangeException(nameof(attackPower));
            Id = id;
            Kind = kind;
            Faction = faction;
            MaximumHealth = maximumHealth;
            CurrentHealth = maximumHealth;
            AttackPower = attackPower;
        }

        public EntityId Id { get; }
        public ActorKind Kind { get; }
        public FactionId Faction { get; }
        public int MaximumHealth { get; }
        public int CurrentHealth { get; private set; }
        public int AttackPower { get; }
        public bool IsDead => CurrentHealth == 0;

        public int ReceiveDamage(int damage)
        {
            if (damage < 0) throw new ArgumentOutOfRangeException(nameof(damage));
            if (IsDead) return 0;

            var applied = Math.Min(damage, CurrentHealth);
            CurrentHealth -= applied;
            return applied;
        }
    }
}
