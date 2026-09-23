using System;
using RPG.Simulation.Contracts;

namespace RPG.Simulation.Runtime
{
    public sealed class BehaviorCondition<TContext> : BehaviorNode<TContext>
    {
        private readonly Func<TContext, bool> _condition;

        public BehaviorCondition(Func<TContext, bool> condition)
        {
            _condition = condition ?? throw new ArgumentNullException(nameof(condition));
        }

        BehaviorStatus BehaviorNode<TContext>.Tick(TContext context) =>
            _condition(context) ? BehaviorStatus.Succeeded : BehaviorStatus.Failed;
    }
}
