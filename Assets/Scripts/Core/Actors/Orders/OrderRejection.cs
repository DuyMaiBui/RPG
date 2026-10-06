namespace RPG.Core.Actors
{
    /// <summary>Why an order was refused. Only permanent problems are rejections: an order that is merely blocked by
    /// range, line of sight or a cooldown is accepted and the actor walks into position or waits instead of losing it.
    /// </summary>
    public enum OrderRejection : byte
    {
        None = 0,

        /// <summary>The issuing session's player does not own the ordered actor.</summary>
        PlayerDoesNotOwnActor = 1,

        /// <summary>The ordered actor does not exist (never spawned, or destroyed).</summary>
        ActorMissing = 2,

        /// <summary>The ordered actor is dead.</summary>
        ActorDead = 3,

        /// <summary>The actor does not have the ordered ability.</summary>
        UnknownAbility = 4,

        /// <summary>The order names a target that does not exist or is already dead.</summary>
        TargetMissing = 5,

        /// <summary>The destination is outside the navigation grid or blocked for the actor's body.</summary>
        DestinationNotWalkable = 6,

        /// <summary>The actor's order queue is full.</summary>
        QueueFull = 7,
    }
}
