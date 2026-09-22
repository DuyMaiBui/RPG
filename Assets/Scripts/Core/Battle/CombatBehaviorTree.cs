namespace RPG.Core.Actors
{
    public sealed class CombatBehaviorTree
    {
        public AutoCombatState Evaluate(bool hasTarget, bool targetInAttackRange)
        {
            if (!hasTarget) return AutoCombatState.AcquireTarget;
            return targetInAttackRange ? AutoCombatState.AttackTarget : AutoCombatState.ChaseTarget;
        }
    }
}
