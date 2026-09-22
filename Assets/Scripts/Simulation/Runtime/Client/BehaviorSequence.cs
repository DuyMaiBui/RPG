using System;
using RPG.Simulation.Contracts;

namespace RPG.Simulation.Runtime
{
    public sealed class BehaviorSequence<TContext> : BehaviorNode<TContext>
    {
        private readonly BehaviorNode<TContext>[] _children;
        private int _index;

        public BehaviorSequence(params BehaviorNode<TContext>[] children)
        {
            _children = children ?? throw new ArgumentNullException(nameof(children));
        }

        BehaviorStatus BehaviorNode<TContext>.Tick(TContext context)
        {
            while (_index < _children.Length)
            {
                var status = _children[_index].Tick(context);
                if (status == BehaviorStatus.Running) return status;
                if (status == BehaviorStatus.Failed)
                {
                    _index = 0;
                    return status;
                }

                _index++;
            }

            _index = 0;
            return BehaviorStatus.Succeeded;
        }
    }
}
