using RPG.Core.Actors;
using UnityEngine;

namespace RPG.Unity
{
    /// <summary>Authored ability. The asset is the source of truth; <see cref="ToDefinition"/> converts it to the
    /// engine-free definition the simulation consumes. The id is persisted in saves, so it must never be reused for a
    /// different ability.</summary>
    [CreateAssetMenu(menuName = "RPG/Content/Ability", fileName = "Ability")]
    public sealed class AbilityAsset : ScriptableObject
    {
        [SerializeField, Min(1)] private int _abilityId = 1;

        [SerializeField, Min(0)] private int _cooldownTicks = 30;

        [SerializeField, Min(0f)] private float _range = 1f;

        [SerializeField] private AbilityTargetMode _targetMode = AbilityTargetMode.CurrentTarget;

        [SerializeField] private AbilityEffectAuthoring[] _effects = new AbilityEffectAuthoring[0];

        public int AbilityId => _abilityId;

        public int CooldownTicks => _cooldownTicks;

        public float Range => _range;

        public AbilityTargetMode TargetMode => _targetMode;

        public AbilityEffectAuthoring[] Effects => _effects;

        /// <summary>Converts this asset to the engine-free definition. Throws when the authoring is malformed; run
        /// <see cref="ContentLibraryAsset.Validate"/> first to report the problem without an exception.</summary>
        public AbilityDefinition ToDefinition()
        {
            var effects = new AbilityEffect[_effects.Length];
            for (var index = 0; index < _effects.Length; index++)
                effects[index] = _effects[index].ToEffect();

            return new AbilityDefinition(_abilityId, _cooldownTicks, _range, _targetMode, effects);
        }
    }
}
