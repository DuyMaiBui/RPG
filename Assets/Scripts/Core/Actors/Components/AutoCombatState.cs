namespace RPG.Core.Actors
{
    public enum AutoCombatState : byte
    {
        AcquireTarget = 0,
        ChaseTarget = 1,
        AttackTarget = 2,
        Dead = 3,
    }
}
