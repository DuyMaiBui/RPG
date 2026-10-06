namespace RPG.Core.Actors
{
    /// <summary>Read-only helpers for querying an actor's active status effects.</summary>
    public static class ActorStatus
    {
        public static bool Has(Actor actor, StatusEffectType type) =>
            actor.Components.TryGet<StatusEffectComponent>(out var effects) && effects.Has(type);

        /// <summary>True while the actor is stunned, so it can neither move, attack nor cast.</summary>
        public static bool IsDisabled(Actor actor) =>
            actor.Components.TryGet<StatusEffectComponent>(out var effects) &&
            effects.Has(StatusEffectType.Stun);
    }
}
