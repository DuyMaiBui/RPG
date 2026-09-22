using System;

namespace RPG.Core.Actors
{
    public sealed class AttackComponent : IActorComponent
    {
        public AttackComponent(int attackPower)
        {
            if (attackPower < 0) throw new ArgumentOutOfRangeException(nameof(attackPower));
            AttackPower = attackPower;
        }

        public int AttackPower { get; }
    }
}
