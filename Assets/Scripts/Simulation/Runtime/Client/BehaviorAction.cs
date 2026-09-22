using System;
using RPG.Simulation.Contracts;

namespace RPG.Simulation.Runtime
{
    public sealed class BehaviorAction<TContext> : BehaviorNode<TContext>
    {
        private readonly Func<TContext, BehaviorStatus> _action;

        public BehaviorAction(Func<TContext, BehaviorStatus> action)
        {
            _action = action ?? throw new ArgumentNullException(nameof(action));
        }

        BehaviorStatus BehaviorNode<TContext>.Tick(TContext context) => _action(context);
    }
}
