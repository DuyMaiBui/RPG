namespace RPG.Core.Actors
{
    /// <summary>One effect an ability applies to its target. Magnitude is the amount for damage/healing or the status
    /// effect strength; DurationTicks is used by the status effects and ignored by direct damage/healing.</summary>
    public readonly struct AbilityEffect
    {
        public AbilityEffect(AbilityEffectType type, int magnitude, int durationTicks = 0)
        {
            Type = type;
            Magnitude = magnitude;
            DurationTicks = durationTicks;
        }

        public AbilityEffectType Type { get; }

        public int Magnitude { get; }

        public int DurationTicks { get; }
    }
}
