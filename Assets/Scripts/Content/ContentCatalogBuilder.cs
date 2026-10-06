using System;
using System.Collections.Generic;
using RPG.Core.Actors;

namespace RPG.Content
{
    /// <summary>Collects authored definitions and produces a validated <see cref="ContentCatalog"/>. Problems are
    /// accumulated instead of thrown so one authoring pass reports every error at once, and iteration is ordered by
    /// id and archetype so the report is deterministic.</summary>
    public sealed class ContentCatalogBuilder
    {
        private readonly SortedDictionary<int, AbilityDefinition> _abilities = new();
        private readonly SortedDictionary<ActorArchetype, int[]> _loadouts = new();
        private readonly SortedDictionary<int, string> _abilitySources = new();
        private readonly SortedDictionary<ActorArchetype, string> _loadoutSources = new();
        private readonly List<ContentValidationError> _errors = new();

        public void AddAbility(AbilityDefinition ability, string source = null)
        {
            if (ability == null) throw new ArgumentNullException(nameof(ability));

            if (_abilities.ContainsKey(ability.Id))
            {
                _errors.Add(new ContentValidationError(
                    "duplicate-ability-id",
                    $"Ability id {ability.Id} is defined more than once.",
                    source));
                return;
            }

            _abilities.Add(ability.Id, ability);
            _abilitySources.Add(ability.Id, source);
        }

        public void AddArchetypeLoadout(ActorArchetype archetype, int[] abilityIds, string source = null)
        {
            if (_loadouts.ContainsKey(archetype))
            {
                _errors.Add(new ContentValidationError(
                    "duplicate-archetype-loadout",
                    $"Archetype {archetype} has more than one loadout.",
                    source));
                return;
            }

            if (abilityIds == null || abilityIds.Length == 0)
            {
                _errors.Add(new ContentValidationError(
                    "empty-archetype-loadout",
                    $"Archetype {archetype} has an empty ability loadout.",
                    source));
                return;
            }

            _loadouts.Add(archetype, (int[])abilityIds.Clone());
            _loadoutSources.Add(archetype, source);
        }

        public ContentValidationResult Validate()
        {
            var errors = new List<ContentValidationError>(_errors);

            foreach (var pair in _abilities)
                ValidateAbility(pair.Value, _abilitySources[pair.Key], errors);

            foreach (var pair in _loadouts)
            {
                var source = _loadoutSources[pair.Key];
                for (var index = 0; index < pair.Value.Length; index++)
                {
                    if (_abilities.ContainsKey(pair.Value[index])) continue;

                    errors.Add(new ContentValidationError(
                        "unknown-ability-reference",
                        $"Archetype {pair.Key} references ability id {pair.Value[index]} at index {index}, which is not defined.",
                        source));
                }
            }

            return new ContentValidationResult(errors);
        }

        /// <summary>Builds the catalog. Throws when validation reports any error; use <see cref="Validate"/> to report
        /// problems without throwing.</summary>
        public ContentCatalog Build()
        {
            var validation = Validate();
            if (!validation.IsValid)
                throw new InvalidOperationException(validation.Describe());

            var loadouts = new Dictionary<ActorArchetype, AbilityDefinition[]>(_loadouts.Count);
            foreach (var pair in _loadouts)
            {
                var abilities = new AbilityDefinition[pair.Value.Length];
                for (var index = 0; index < pair.Value.Length; index++)
                    abilities[index] = _abilities[pair.Value[index]];

                loadouts.Add(pair.Key, abilities);
            }

            return new ContentCatalog(_abilities, loadouts);
        }

        private static void ValidateAbility(
            AbilityDefinition ability,
            string source,
            List<ContentValidationError> errors)
        {
            for (var index = 0; index < ability.Effects.Length; index++)
            {
                var effect = ability.Effects[index];
                var where = $"Ability {ability.Id} effect {index} ({effect.Type})";

                if (effect.Magnitude <= 0)
                {
                    errors.Add(new ContentValidationError(
                        "non-positive-magnitude",
                        $"{where} has magnitude {effect.Magnitude}; every effect needs a positive magnitude.",
                        source));
                }

                switch (effect.Type)
                {
                    case AbilityEffectType.Damage:
                    case AbilityEffectType.Heal:
                        if (effect.DurationTicks != 0)
                        {
                            errors.Add(new ContentValidationError(
                                "unexpected-duration",
                                $"{where} has duration {effect.DurationTicks}; direct damage and healing ignore duration and must leave it at zero.",
                                source));
                        }

                        break;
                    default:
                        if (effect.DurationTicks <= 0)
                        {
                            errors.Add(new ContentValidationError(
                                "missing-duration",
                                $"{where} has duration {effect.DurationTicks}; a status effect needs a positive duration.",
                                source));
                        }

                        break;
                }
            }
        }
    }
}
