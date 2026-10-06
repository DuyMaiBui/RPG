using System;

namespace RPG.Core.Actors
{
    public sealed class HealthComponent : IActorComponent
    {
        public HealthComponent(int maximumHealth)
        {
            if (maximumHealth <= 0) throw new ArgumentOutOfRangeException(nameof(maximumHealth));
            MaximumHealth = maximumHealth;
            CurrentHealth = maximumHealth;
        }

        public int MaximumHealth { get; }
        public int CurrentHealth { get; private set; }
        public bool IsDead => CurrentHealth == 0;

        public int ReceiveDamage(int damage)
        {
            if (damage < 0) throw new ArgumentOutOfRangeException(nameof(damage));
            if (IsDead) return 0;

            var applied = Math.Min(damage, CurrentHealth);
            CurrentHealth -= applied;
            return applied;
        }

        public int ReceiveHealing(int healing)
        {
            if (healing < 0) throw new ArgumentOutOfRangeException(nameof(healing));
            if (IsDead) return 0;

            var applied = Math.Min(healing, MaximumHealth - CurrentHealth);
            CurrentHealth += applied;
            return applied;
        }
    }
}
