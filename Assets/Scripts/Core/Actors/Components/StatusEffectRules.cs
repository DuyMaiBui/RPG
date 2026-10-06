namespace RPG.Core.Actors
{
    /// <summary>Per-type status effect policy: the stack cap and which actor stat the effect drives.</summary>
    public static class StatusEffectRules
    {
        public static int MaxStacks(StatusEffectType type) => type switch
        {
            StatusEffectType.Poison => 3,
            StatusEffectType.Regeneration => 1,
            StatusEffectType.Slow => 2,
            _ => 1,
        };

        /// <summary>True when the effect deals or restores health every tick.</summary>
        public static bool IsPeriodic(StatusEffectType type) =>
            type == StatusEffectType.Poison || type == StatusEffectType.Regeneration;

        /// <summary>True when the effect changes the actor's movement speed.</summary>
        public static bool IsMovementModifier(StatusEffectType type) => type == StatusEffectType.Slow;
    }
}
