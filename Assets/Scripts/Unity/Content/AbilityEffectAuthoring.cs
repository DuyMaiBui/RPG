using System;
using RPG.Core.Actors;
using UnityEngine;

namespace RPG.Unity
{
    /// <summary>Serialized form of one <see cref="AbilityEffect"/>. Authoring stores the raw values; validation
    /// rejects nonsense such as a non-positive magnitude or a status effect without a duration.</summary>
    [Serializable]
    public struct AbilityEffectAuthoring
    {
        [SerializeField] private AbilityEffectType _type;

        [SerializeField, Min(1)] private int _magnitude;

        [SerializeField, Min(0)] private int _durationTicks;

        public AbilityEffectAuthoring(AbilityEffectType type, int magnitude, int durationTicks = 0)
        {
            _type = type;
            _magnitude = magnitude;
            _durationTicks = durationTicks;
        }

        public AbilityEffectType Type => _type;

        public int Magnitude => _magnitude;

        public int DurationTicks => _durationTicks;

        public AbilityEffect ToEffect() => new AbilityEffect(_type, _magnitude, _durationTicks);
    }
}
