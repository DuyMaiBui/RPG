using System;
using RPG.Simulation.Contracts;

namespace RPG.Simulation.Runtime
{
    public sealed class BehaviorInverter<TContext> : BehaviorNode<TContext>
    {
        private readonly BehaviorNode<TContext> _child;

        public BehaviorInverter(BehaviorNode<TContext> child)
        {
            _child = child ?? throw new ArgumentNullException(nameof(child));
        }

        BehaviorStatus BehaviorNode<TContext>.Tick(TContext context)
        {
            var status = _child.Tick(context);
            return status == BehaviorStatus.Succeeded
                ? BehaviorStatus.Failed
                : status == BehaviorStatus.Failed
                    ? BehaviorStatus.Succeeded
                    : BehaviorStatus.Running;
        }
    }
}
