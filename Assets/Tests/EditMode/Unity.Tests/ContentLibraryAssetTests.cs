using System.Collections.Generic;
using NUnit.Framework;
using RPG.Content;
using RPG.Core.Actors;
using UnityEditor;
using UnityEngine;

namespace RPG.Unity.Tests
{
    public sealed class ContentLibraryAssetTests
    {
        private readonly List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            for (var index = 0; index < _created.Count; index++)
                Object.DestroyImmediate(_created[index]);
            _created.Clear();
        }

        [Test]
        public void Library_RejectsEmptyAbilitySlot()
        {
            var library = Library(new AbilityAsset[] { null }, new ActorArchetypeAsset[0]);

            var validation = library.Validate();

            Assert.That(validation.IsValid, Is.False);
            Assert.That(Codes(validation), Does.Contain("missing-ability-asset"));
        }

        [Test]
        public void Library_RejectsAbilityWithoutEffects()
        {
            var library = Library(new[] { Ability(1) }, new ActorArchetypeAsset[0]);

            var validation = library.Validate();

            Assert.That(Codes(validation), Does.Contain("empty-ability-effects"));
        }

        [Test]
        public void Library_RejectsNonPositiveAbilityId()
        {
            var library = Library(new[] { Ability(0, Damage(5)) }, new ActorArchetypeAsset[0]);

            var validation = library.Validate();

            Assert.That(Codes(validation), Does.Contain("invalid-ability-id"));
        }

        [Test]
        public void Library_RejectsArchetypeWithoutAbilities()
        {
            var library = Library(new[] { Ability(1, Damage(5)) }, new[] { Archetype(ActorArchetype.Bruiser) });

            var validation = library.Validate();

            Assert.That(Codes(validation), Does.Contain("empty-archetype-loadout"));
        }

        [Test]
        public void Library_RejectsArchetypeWithMissingAbilityReference()
        {
            var library = Library(
                new[] { Ability(1, Damage(5)) },
                new[] { Archetype(ActorArchetype.Bruiser, new AbilityAsset[] { null }) });

            var validation = library.Validate();

            Assert.That(Codes(validation), Does.Contain("missing-ability-reference"));
        }

        [Test]
        public void Library_RejectsDuplicateAbilityIdsAcrossAssets()
        {
            var library = Library(
                new[] { Ability(1, Damage(5)), Ability(1, Damage(7)) },
                new[] { Archetype(ActorArchetype.Bruiser, Ability(1, Damage(5))) });

            var validation = library.Validate();

            Assert.That(Codes(validation), Does.Contain("duplicate-ability-id"));
        }

        [Test]
        public void Library_RejectsUnknownAbilityReference()
        {
            var library = Library(
                new[] { Ability(1, Damage(5)) },
                new[] { Archetype(ActorArchetype.Bruiser, Ability(9, Damage(5))) });

            var validation = library.Validate();

            Assert.That(Codes(validation), Does.Contain("unknown-ability-reference"));
        }

        [Test]
        public void Library_BuildsTheCatalogForValidContent()
        {
            var cleave = Ability(1, Damage(8));
            var library = Library(new[] { cleave }, new[] { Archetype(ActorArchetype.Bruiser, cleave) });

            var catalog = library.BuildCatalog();

            Assert.That(catalog.AbilityCount, Is.EqualTo(1));
            Assert.That(catalog.AbilitiesFor(ActorArchetype.Bruiser).Length, Is.EqualTo(1));
            Assert.That(catalog.AbilitiesFor(ActorArchetype.Bruiser)[0].Id, Is.EqualTo(1));
            Assert.That(catalog.AbilitiesFor(ActorArchetype.Support), Is.Empty);
        }

        [Test]
        public void Library_TryBuildFailsWithoutThrowing()
        {
            var library = Library(new AbilityAsset[] { null }, new ActorArchetypeAsset[0]);

            var built = library.TryBuild(out var catalog, out var validation);

            Assert.That(built, Is.False);
            Assert.That(catalog, Is.Null);
            Assert.That(validation.IsValid, Is.False);
        }

        [Test]
        public void ShippedLibrary_BuildsAndCoversEveryPlayableArchetype()
        {
            var guids = AssetDatabase.FindAssets("t:ContentLibraryAsset");
            Assert.That(guids, Is.Not.Empty, "No ContentLibraryAsset is authored in the project.");

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var library = AssetDatabase.LoadAssetAtPath<ContentLibraryAsset>(path);
                Assert.That(library, Is.Not.Null, path);

                var validation = library.Validate();
                Assert.That(validation.IsValid, Is.True, $"{path}: {validation.Describe()}");

                var catalog = library.BuildCatalog();
                foreach (var archetype in new[]
                         {
                             ActorArchetype.Bruiser, ActorArchetype.Skirmisher, ActorArchetype.Support,
                         })
                {
                    Assert.That(catalog.AbilitiesFor(archetype), Is.Not.Empty, $"{path} has no loadout for {archetype}");
                }
            }
        }

        private ContentLibraryAsset Library(AbilityAsset[] abilities, ActorArchetypeAsset[] archetypes)
        {
            var library = Track(ScriptableObject.CreateInstance<ContentLibraryAsset>());
            var serialized = new SerializedObject(library);
            Fill(serialized.FindProperty("_abilities"), abilities);
            Fill(serialized.FindProperty("_archetypes"), archetypes);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return library;
        }

        private AbilityAsset Ability(int id, params AbilityEffectAuthoring[] effects)
        {
            var asset = Track(ScriptableObject.CreateInstance<AbilityAsset>());
            var serialized = new SerializedObject(asset);
            serialized.FindProperty("_abilityId").intValue = id;
            serialized.FindProperty("_cooldownTicks").intValue = 10;
            serialized.FindProperty("_range").floatValue = 1f;
            serialized.FindProperty("_targetMode").enumValueIndex = (int)AbilityTargetMode.CurrentTarget;

            var effectProperty = serialized.FindProperty("_effects");
            effectProperty.arraySize = effects.Length;
            for (var index = 0; index < effects.Length; index++)
            {
                var element = effectProperty.GetArrayElementAtIndex(index);
                element.FindPropertyRelative("_type").enumValueIndex = (int)effects[index].Type;
                element.FindPropertyRelative("_magnitude").intValue = effects[index].Magnitude;
                element.FindPropertyRelative("_durationTicks").intValue = effects[index].DurationTicks;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }

        private ActorArchetypeAsset Archetype(ActorArchetype archetype, params AbilityAsset[] abilities)
        {
            var asset = Track(ScriptableObject.CreateInstance<ActorArchetypeAsset>());
            var serialized = new SerializedObject(asset);
            serialized.FindProperty("_archetype").enumValueIndex = (int)archetype;
            Fill(serialized.FindProperty("_abilities"), abilities);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }

        private static AbilityEffectAuthoring Damage(int magnitude) =>
            new AbilityEffectAuthoring(AbilityEffectType.Damage, magnitude);

        private static void Fill(SerializedProperty property, Object[] values)
        {
            property.arraySize = values.Length;
            for (var index = 0; index < values.Length; index++)
                property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
        }

        private static List<string> Codes(ContentValidationResult validation)
        {
            var codes = new List<string>(validation.Errors.Count);
            for (var index = 0; index < validation.Errors.Count; index++)
                codes.Add(validation.Errors[index].Code);
            return codes;
        }

        private T Track<T>(T created) where T : Object
        {
            _created.Add(created);
            return created;
        }
    }
}
