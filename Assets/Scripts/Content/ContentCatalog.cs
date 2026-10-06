using System;
using System.Collections.Generic;
using RPG.Core.Actors;

namespace RPG.Content
{
    /// <summary>Immutable authored content: abilities by id plus the ordered ability loadout per archetype. The
    /// returned arrays are shared and must be treated as read-only, matching <c>ActorSpawnData</c>.</summary>
    public sealed class ContentCatalog
    {
        private static readonly AbilityDefinition[] NoAbilities = Array.Empty<AbilityDefinition>();

        private readonly Dictionary<int, AbilityDefinition> _abilities;
        private readonly Dictionary<ActorArchetype, AbilityDefinition[]> _loadouts;

        public ContentCatalog(
            IReadOnlyDictionary<int, AbilityDefinition> abilities,
            IReadOnlyDictionary<ActorArchetype, AbilityDefinition[]> loadouts)
        {
            if (abilities == null) throw new ArgumentNullException(nameof(abilities));
            if (loadouts == null) throw new ArgumentNullException(nameof(loadouts));

            _abilities = new Dictionary<int, AbilityDefinition>(abilities.Count);
            foreach (var pair in abilities)
                _abilities.Add(pair.Key, pair.Value);

            _loadouts = new Dictionary<ActorArchetype, AbilityDefinition[]>(loadouts.Count);
            foreach (var pair in loadouts)
                _loadouts.Add(pair.Key, pair.Value);
        }

        public int AbilityCount => _abilities.Count;

        public bool TryGetAbility(int abilityId, out AbilityDefinition ability) =>
            _abilities.TryGetValue(abilityId, out ability);

        /// <summary>Abilities granted to an archetype in cast-priority order (lowest index casts first). An archetype
        /// without an authored loadout returns an empty array.</summary>
        public AbilityDefinition[] AbilitiesFor(ActorArchetype archetype) =>
            _loadouts.TryGetValue(archetype, out var abilities) ? abilities : NoAbilities;
    }
}
