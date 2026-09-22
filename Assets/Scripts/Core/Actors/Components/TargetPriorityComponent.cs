namespace RPG.Core.Actors
{
    public sealed class TargetPriorityComponent : IActorComponent
    {
        public TargetPriorityComponent(TargetPriorityMode mode)
        {
            Mode = mode;
        }

        public TargetPriorityMode Mode { get; }
    }
}
