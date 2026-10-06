using System.Collections.Generic;
using RPG.Content;
using RPG.Core.Actors;
using UnityEngine;

namespace RPG.Unity
{
    /// <summary>Root authored content asset. Abilities and archetype loadouts are authored as separate assets and
    /// referenced here; <see cref="TryBuild"/> converts them into the engine-free catalog the simulation consumes and
    /// reports every authoring problem at once. Runtime code never falls back to code-defined content.</summary>
    [CreateAssetMenu(menuName = "RPG/Content/Content Library", fileName = "ContentLibrary")]
    public sealed class ContentLibraryAsset : ScriptableObject
    {
        [SerializeField] private AbilityAsset[] _abilities = new AbilityAsset[0];

        [SerializeField] private ActorArchetypeAsset[] _archetypes = new ActorArchetypeAsset[0];

        public AbilityAsset[] Abilities => _abilities;

        public ActorArchetypeAsset[] Archetypes => _archetypes;

        /// <summary>Builds the catalog, throwing when any authoring problem exists.</summary>
        public ContentCatalog BuildCatalog()
        {
            if (!TryBuild(out var catalog, out var validation))
                throw new System.InvalidOperationException(validation.Describe());

            return catalog;
        }

        public ContentValidationResult Validate()
        {
            TryBuild(out _, out var validation);
            return validation;
        }

        public bool TryBuild(out ContentCatalog catalog, out ContentValidationResult validation)
        {
            var builder = new ContentCatalogBuilder();
            var errors = new List<ContentValidationError>();

            CollectAbilities(builder, errors);
            CollectArchetypes(builder, errors);

            var builderValidation = builder.Validate();
            for (var index = 0; index < builderValidation.Errors.Count; index++)
                errors.Add(builderValidation.Errors[index]);

            validation = new ContentValidationResult(errors);
            if (!validation.IsValid)
            {
                catalog = null;
                return false;
            }

            catalog = builder.Build();
            return true;
        }

        private void CollectAbilities(ContentCatalogBuilder builder, List<ContentValidationError> errors)
        {
            for (var index = 0; index < _abilities.Length; index++)
            {
                var asset = _abilities[index];
                if (asset == null)
                {
                    errors.Add(new ContentValidationError(
                        "missing-ability-asset",
                        $"Ability slot {index} is empty.",
                        name));
                    continue;
                }

                if (asset.AbilityId <= 0)
                {
                    errors.Add(new ContentValidationError(
                        "invalid-ability-id",
                        $"Ability id {asset.AbilityId} must be positive.",
                        asset.name));
                    continue;
                }

                if (asset.CooldownTicks < 0 || asset.Range < 0f)
                {
                    errors.Add(new ContentValidationError(
                        "invalid-ability-tuning",
                        $"Ability {asset.AbilityId} has cooldown {asset.CooldownTicks} and range {asset.Range}; both must be non-negative.",
                        asset.name));
                    continue;
                }

                if (asset.Effects == null || asset.Effects.Length == 0)
                {
                    errors.Add(new ContentValidationError(
                        "empty-ability-effects",
                        $"Ability {asset.AbilityId} has no effects.",
                        asset.name));
                    continue;
                }

                builder.AddAbility(asset.ToDefinition(), asset.name);
            }
        }

        private void CollectArchetypes(ContentCatalogBuilder builder, List<ContentValidationError> errors)
        {
            for (var index = 0; index < _archetypes.Length; index++)
            {
                var asset = _archetypes[index];
                if (asset == null)
                {
                    errors.Add(new ContentValidationError(
                        "missing-archetype-asset",
                        $"Archetype slot {index} is empty.",
                        name));
                    continue;
                }

                var references = asset.Abilities;
                if (references == null || references.Length == 0)
                {
                    errors.Add(new ContentValidationError(
                        "empty-archetype-loadout",
                        $"Archetype {asset.Archetype} has an empty ability loadout.",
                        asset.name));
                    continue;
                }

                var abilityIds = new int[references.Length];
                var complete = true;
                for (var slot = 0; slot < references.Length; slot++)
                {
                    if (references[slot] == null)
                    {
                        errors.Add(new ContentValidationError(
                            "missing-ability-reference",
                            $"Archetype {asset.Archetype} ability slot {slot} is empty.",
                            asset.name));
                        complete = false;
                        continue;
                    }

                    abilityIds[slot] = references[slot].AbilityId;
                }

                if (complete)
                    builder.AddArchetypeLoadout(asset.Archetype, abilityIds, asset.name);
            }
        }
    }
}
