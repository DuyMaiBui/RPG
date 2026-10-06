namespace RPG.Core.Actors
{
    /// <summary>Kinds of timed status effect the simulation can apply to an actor.</summary>
    public enum StatusEffectType : byte
    {
        /// <summary>Periodic damage: <see cref="StatusEffect.Magnitude"/> health per tick per stack.</summary>
        Poison = 0,

        /// <summary>Periodic healing: <see cref="StatusEffect.Magnitude"/> health per tick per stack.</summary>
        Regeneration = 1,

        /// <summary>Movement modifier: <see cref="StatusEffect.Magnitude"/> percent slower per stack (30 = -30%).</summary>
        Slow = 2,

        /// <summary>Disables the actor: it cannot move, attack or cast while stunned.</summary>
        Stun = 3,
    }
}
