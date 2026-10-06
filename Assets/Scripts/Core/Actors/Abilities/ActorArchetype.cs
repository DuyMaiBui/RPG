namespace RPG.Core.Actors
{
    /// <summary>Role presets that map a unit to a default ability loadout.</summary>
    public enum ActorArchetype : byte
    {
        /// <summary>No explicit archetype: a loadout derives one from its attack type (melee becomes a Bruiser,
        /// projectile becomes a Skirmisher).</summary>
        None = 0,

        /// <summary>Front-line melee with a heavy strike.</summary>
        Bruiser = 1,

        /// <summary>Ranged attacker that poisons its target.</summary>
        Skirmisher = 2,

        /// <summary>Support that heals itself and hampers enemies.</summary>
        Support = 3,
    }
}
