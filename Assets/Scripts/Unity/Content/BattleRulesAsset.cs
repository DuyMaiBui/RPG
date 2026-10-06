using UnityEngine;

namespace RPG.Unity
{
    /// <summary>Authored battle-wide rules. Per-actor stats are authored on the loadout and wave composition is
    /// authored per encounter, so neither lives here.</summary>
    [CreateAssetMenu(menuName = "RPG/Content/Battle Rules", fileName = "BattleRules")]
    public sealed class BattleRulesAsset : ScriptableObject
    {
        [SerializeField, Min(1)] private int _tickRate = 30;

        [SerializeField, Min(1)] private int _navigationWidth = 20;

        [SerializeField, Min(1)] private int _navigationHeight = 10;

        [SerializeField, Min(0.1f)] private float _navigationCellSize = 1f;

        [SerializeField, Min(1)] private int _actorsPerFaction = 3;

        [SerializeField, Min(1f)] private float _baseOffset = 26f;

        public int TickRate => _tickRate;

        public int NavigationWidth => _navigationWidth;

        public int NavigationHeight => _navigationHeight;

        public float NavigationCellSize => _navigationCellSize;

        public int ActorsPerFaction => _actorsPerFaction;

        public float BaseOffset => _baseOffset;

        /// <summary>Base offset clamped so both bases stay inside the navigation grid with one cell of margin.</summary>
        public float ResolveBaseOffset()
        {
            var maximum = _navigationWidth * _navigationCellSize * 0.5f - 1f;
            return Mathf.Min(Mathf.Max(1f, _baseOffset), maximum);
        }
    }
}
