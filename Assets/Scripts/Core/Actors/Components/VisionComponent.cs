using System;

namespace RPG.Core.Actors
{
    public sealed class VisionComponent : IActorComponent
    {
        public VisionComponent(float range)
        {
            if (range < 0f) throw new ArgumentOutOfRangeException(nameof(range));
            Range = range;
        }

        public float Range { get; }
    }
}
