using System;

namespace RPG.Core.Actors
{
    public sealed class BodyComponent : IActorComponent
    {
        public BodyComponent(float radius)
        {
            if (radius <= 0f) throw new ArgumentOutOfRangeException(nameof(radius));
            Radius = radius;
        }

        public float Radius { get; }
    }
}
