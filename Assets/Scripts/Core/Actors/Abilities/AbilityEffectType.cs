namespace RPG.Core.Actors
{
    /// <summary>What one ability effect does to the target it is applied to.</summary>
    public enum AbilityEffectType : byte
    {
        /// <summary>Direct damage (<see cref="AbilityEffect.Magnitude"/> health).</summary>
        Damage = 0,

        /// <summary>Direct healing (<see cref="AbilityEffect.Magnitude"/> health).</summary>
        Heal = 1,

        /// <summary>Applies <see cref="StatusEffectType.Poison"/> for <see cref="AbilityEffect.DurationTicks"/>.</summary>
        Poison = 2,

        /// <summary>Applies <see cref="StatusEffectType.Regeneration"/> for <see cref="AbilityEffect.DurationTicks"/>.</summary>
        Regeneration = 3,

        /// <summary>Applies <see cref="StatusEffectType.Slow"/> for <see cref="AbilityEffect.DurationTicks"/>.</summary>
        Slow = 4,

        /// <summary>Applies <see cref="StatusEffectType.Stun"/> for <see cref="AbilityEffect.DurationTicks"/>.</summary>
        Stun = 5,
    }
}
