namespace RPG.Core.Actors
{
    public sealed class CombatBehaviorContext
    {
        public bool HasTarget { get; set; }
        public bool TargetInAttackRange { get; set; }
        public AutoCombatState Result { get; set; }
    }
}
