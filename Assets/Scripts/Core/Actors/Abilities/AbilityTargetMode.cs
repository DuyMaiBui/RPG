namespace RPG.Core.Actors
{
    /// <summary>Who an ability affects when it is cast.</summary>
    public enum AbilityTargetMode : byte
    {
        /// <summary>The caster's current combat target.</summary>
        CurrentTarget = 0,

        /// <summary>The caster itself.</summary>
        Self = 1,
    }
}
