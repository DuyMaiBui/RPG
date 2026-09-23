using RPG.Simulation.Contracts;
using RPG.Simulation.Runtime;

namespace RPG.Core.Actors
{
    public sealed class CombatBehaviorTree
    {
        private readonly CombatBehaviorContext _context = new();
        private readonly BehaviorTree<CombatBehaviorContext> _tree;

        public CombatBehaviorTree()
        {
            _tree = new BehaviorTree<CombatBehaviorContext>(new BehaviorSelector<CombatBehaviorContext>(
                new BehaviorSequence<CombatBehaviorContext>(
                    new BehaviorCondition<CombatBehaviorContext>(context => !context.HasTarget),
                    new BehaviorAction<CombatBehaviorContext>(context => SetState(context, AutoCombatState.AcquireTarget))),
                new BehaviorSequence<CombatBehaviorContext>(
                    new BehaviorCondition<CombatBehaviorContext>(context => context.TargetInAttackRange),
                    new BehaviorAction<CombatBehaviorContext>(context => SetState(context, AutoCombatState.AttackTarget))),
                new BehaviorAction<CombatBehaviorContext>(context => SetState(context, AutoCombatState.ChaseTarget))));
        }

        public AutoCombatState Evaluate(bool hasTarget, bool targetInAttackRange)
        {
            _context.HasTarget = hasTarget;
            _context.TargetInAttackRange = targetInAttackRange;
            _tree.Tick(_context);
            return _context.Result;
        }

        private static BehaviorStatus SetState(CombatBehaviorContext context, AutoCombatState state)
        {
            context.Result = state;
            return BehaviorStatus.Succeeded;
        }
    }
}
