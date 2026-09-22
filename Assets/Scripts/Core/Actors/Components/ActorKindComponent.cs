namespace RPG.Core.Actors
{
    public sealed class ActorKindComponent : IActorComponent
    {
        public ActorKindComponent(ActorKind kind)
        {
            Kind = kind;
        }

        public ActorKind Kind { get; }
    }
}
