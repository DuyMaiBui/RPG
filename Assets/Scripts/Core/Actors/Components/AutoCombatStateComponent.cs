namespace RPG.Core.Actors
{
    public sealed class AutoCombatStateComponent : IActorComponent
    {
        public AutoCombatState State { get; set; } = AutoCombatState.AcquireTarget;
    }
}
