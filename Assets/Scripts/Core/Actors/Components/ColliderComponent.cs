using RPG.Core.Physics;

namespace RPG.Core.Actors
{
    public sealed class ColliderComponent : IActorComponent
    {
        public ColliderComponent(ColliderCompound compound)
        {
            Compound = compound;
        }

        public ColliderCompound Compound { get; }
    }
}
