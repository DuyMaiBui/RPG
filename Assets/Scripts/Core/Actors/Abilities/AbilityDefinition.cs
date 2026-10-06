using System;

namespace RPG.Core.Actors
{
    /// <summary>An actor ability. Auto-cast: once <see cref="CooldownTicks"/> elapses and the target is in range with
    /// line of sight, the ability fires and applies its effects.</summary>
    public sealed class AbilityDefinition
    {
        public AbilityDefinition(
            int id,
            int cooldownTicks,
            float range,
            AbilityTargetMode targetMode,
            AbilityEffect[] effects)
        {
            if (cooldownTicks < 0) throw new ArgumentOutOfRangeException(nameof(cooldownTicks));
            if (range < 0f) throw new ArgumentOutOfRangeException(nameof(range));
            if (effects == null || effects.Length == 0)
                throw new ArgumentException("An ability requires at least one effect.", nameof(effects));

            Id = id;
            CooldownTicks = cooldownTicks;
            Range = range;
            TargetMode = targetMode;
            Effects = (AbilityEffect[])effects.Clone();
        }

        public int Id { get; }

        public int CooldownTicks { get; }

        public float Range { get; }

        public AbilityTargetMode TargetMode { get; }

        public AbilityEffect[] Effects { get; }
    }
}
