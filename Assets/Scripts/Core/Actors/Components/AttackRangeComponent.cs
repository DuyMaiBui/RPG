using System;

namespace RPG.Core.Actors
{
    public sealed class AttackRangeComponent : IActorComponent
    {
        public AttackRangeComponent(float reach)
        {
            if (reach < 0f) throw new ArgumentOutOfRangeException(nameof(reach));
            Reach = reach;
        }

        public float Reach { get; }
    }
}
