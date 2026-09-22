namespace RPG.Core.Actors
{
    public sealed class FactionComponent : IActorComponent
    {
        public FactionComponent(FactionId faction)
        {
            Faction = faction;
        }

        public FactionId Faction { get; }
    }
}
