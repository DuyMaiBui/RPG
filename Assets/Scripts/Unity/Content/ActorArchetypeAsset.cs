using RPG.Core.Actors;
using UnityEngine;

namespace RPG.Unity
{
    /// <summary>Authored ability loadout for one <see cref="ActorArchetype"/>. Order is cast priority: the ability at
    /// index 0 is considered first.</summary>
    [CreateAssetMenu(menuName = "RPG/Content/Actor Archetype", fileName = "Archetype")]
    public sealed class ActorArchetypeAsset : ScriptableObject
    {
        [SerializeField] private ActorArchetype _archetype = ActorArchetype.Bruiser;

        [SerializeField] private AbilityAsset[] _abilities = new AbilityAsset[0];

        public ActorArchetype Archetype => _archetype;

        public AbilityAsset[] Abilities => _abilities;
    }
}
