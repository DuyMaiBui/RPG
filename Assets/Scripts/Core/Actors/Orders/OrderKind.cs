namespace RPG.Core.Actors
{
    /// <summary>What a queued order asks an actor to do. <see cref="Stop"/> is deliberately not a kind: stopping is a
    /// command that clears the queue rather than a state an actor sits in.</summary>
    public enum OrderKind : byte
    {
        /// <summary>Move to the destination and ignore enemies on the way.</summary>
        Move = 0,

        /// <summary>Move to the destination, engaging enemies met on the way.</summary>
        AttackMove = 1,

        /// <summary>Attack one target, chasing it as far as the actor's leash allows.</summary>
        AttackTarget = 2,

        /// <summary>Cast one owned ability at one target, moving into range first and waiting for the cooldown.</summary>
        CastAbility = 3,

        /// <summary>Hold this position: attack what comes into reach, never chase.</summary>
        Hold = 4,
    }
}
