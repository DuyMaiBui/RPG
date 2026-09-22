using System;
using RPG.Simulation.Contracts;

namespace RPG.Simulation.Runtime
{
    public sealed class BehaviorTree<TContext>
    {
        private readonly BehaviorNode<TContext> _root;

        public BehaviorTree(BehaviorNode<TContext> root)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
        }

        public BehaviorStatus Tick(TContext context) => _root.Tick(context);
    }
}
