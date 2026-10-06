namespace RPG.Core.Actors
{
    /// <summary>One timed effect on an actor. Immutable: the owning <see cref="StatusEffectComponent"/> replaces the
    /// entry when its duration or stacks change, so snapshots and hashes stay deterministic.</summary>
    public readonly struct StatusEffect
    {
        public StatusEffect(StatusEffectType type, int magnitude, int remainingTicks, int stacks)
        {
            Type = type;
            Magnitude = magnitude;
            RemainingTicks = remainingTicks;
            Stacks = stacks;
        }

        public StatusEffectType Type { get; }

        public int Magnitude { get; }

        public int RemainingTicks { get; }

        public int Stacks { get; }

        public StatusEffect WithRemainingTicks(int remainingTicks) =>
            new StatusEffect(Type, Magnitude, remainingTicks, Stacks);

        public StatusEffect WithStacks(int stacks) =>
            new StatusEffect(Type, Magnitude, RemainingTicks, stacks);
    }
}
