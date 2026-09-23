using System;
using RPG.Simulation.Contracts;

namespace RPG.Simulation.Runtime
{
    public sealed class BehaviorParallel<TContext> : BehaviorNode<TContext>
    {
        private readonly BehaviorNode<TContext>[] _children;

        public BehaviorParallel(params BehaviorNode<TContext>[] children)
        {
            _children = children ?? throw new ArgumentNullException(nameof(children));
        }

        BehaviorStatus BehaviorNode<TContext>.Tick(TContext context)
        {
            var running = false;
            for (var index = 0; index < _children.Length; index++)
            {
                var status = _children[index].Tick(context);
                if (status == BehaviorStatus.Failed) return BehaviorStatus.Failed;
                if (status == BehaviorStatus.Running) running = true;
            }

            return running ? BehaviorStatus.Running : BehaviorStatus.Succeeded;
        }
    }
}
