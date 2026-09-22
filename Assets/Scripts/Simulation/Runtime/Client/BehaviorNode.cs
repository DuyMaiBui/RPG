using RPG.Simulation.Contracts;

namespace RPG.Simulation.Runtime
{
    public interface BehaviorNode<TContext>
    {
        BehaviorStatus Tick(TContext context);
    }
}
