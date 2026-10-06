using System.Collections.Generic;
using NUnit.Framework;
using RPG.Content;
using RPG.Core.Actors;

namespace RPG.Core.Tests
{
    public sealed class ContentCatalogBuilderTests
    {
        [Test]
        public void Validation_RejectsDuplicateAbilityIds()
        {
            var builder = new ContentCatalogBuilder();
            builder.AddAbility(Ability(7, new AbilityEffect(AbilityEffectType.Damage, 5)), "first.asset");
            builder.AddAbility(Ability(7, new AbilityEffect(AbilityEffectType.Damage, 9)), "second.asset");

            var validation = builder.Validate();

            Assert.That(validation.IsValid, Is.False);
            Assert.That(Codes(validation), Does.Contain("duplicate-ability-id"));
        }

        [Test]
        public void Validation_RejectsUnknownAbilityReference()
        {
            var builder = new ContentCatalogBuilder();
            builder.AddAbility(Ability(1, new AbilityEffect(AbilityEffectType.Damage, 5)));
            builder.AddArchetypeLoadout(ActorArchetype.Bruiser, new[] { 1, 42 }, "bruiser.asset");

            var validation = builder.Validate();

            Assert.That(validation.IsValid, Is.False);
            Assert.That(Codes(validation), Does.Contain("unknown-ability-reference"));
        }

        [Test]
        public void Validation_RejectsEmptyArchetypeLoadout()
        {
            var builder = new ContentCatalogBuilder();
            builder.AddArchetypeLoadout(ActorArchetype.Support, new int[0], "support.asset");

            var validation = builder.Validate();

            Assert.That(validation.IsValid, Is.False);
            Assert.That(Codes(validation), Does.Contain("empty-archetype-loadout"));
        }

        [Test]
        public void Validation_RejectsNonPositiveMagnitude()
        {
            var builder = new ContentCatalogBuilder();
            builder.AddAbility(Ability(1, new AbilityEffect(AbilityEffectType.Damage, 0)));
            builder.AddAbility(Ability(2, new AbilityEffect(AbilityEffectType.Heal, -3)));

            var validation = builder.Validate();

            Assert.That(Count(validation, "non-positive-magnitude"), Is.EqualTo(2));
        }

        [Test]
        public void Validation_RejectsDurationOnDirectEffect()
        {
            var builder = new ContentCatalogBuilder();
            builder.AddAbility(Ability(1, new AbilityEffect(AbilityEffectType.Damage, 5, 3)));

            var validation = builder.Validate();

            Assert.That(Codes(validation), Does.Contain("unexpected-duration"));
        }

        [Test]
        public void Validation_RejectsStatusEffectWithoutDuration()
        {
            var builder = new ContentCatalogBuilder();
            builder.AddAbility(Ability(1, new AbilityEffect(AbilityEffectType.Poison, 2)));

            var validation = builder.Validate();

            Assert.That(Codes(validation), Does.Contain("missing-duration"));
        }

        [Test]
        public void Validation_ReportsEveryProblemInOnePass()
        {
            var builder = new ContentCatalogBuilder();
            builder.AddAbility(Ability(1, new AbilityEffect(AbilityEffectType.Poison, 0)));
            builder.AddAbility(Ability(1, new AbilityEffect(AbilityEffectType.Damage, 5)));
            builder.AddArchetypeLoadout(ActorArchetype.Bruiser, new[] { 99 });

            var validation = builder.Validate();

            var codes = Codes(validation);
            Assert.That(codes, Does.Contain("duplicate-ability-id"));
            Assert.That(codes, Does.Contain("non-positive-magnitude"));
            Assert.That(codes, Does.Contain("missing-duration"));
            Assert.That(codes, Does.Contain("unknown-ability-reference"));
        }

        [Test]
        public void Validation_IsIdenticalAcrossRuns()
        {
            var first = BuildInvalid().Validate().Describe();
            var second = BuildInvalid().Validate().Describe();

            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void Validation_ReportsTheSourceOfTheFailingAbility()
        {
            var builder = new ContentCatalogBuilder();
            builder.AddAbility(Ability(7, new AbilityEffect(AbilityEffectType.Damage, 0)), "Content/Fireball.asset");

            var validation = builder.Validate();

            Assert.That(validation.Errors, Is.Not.Empty);
            Assert.That(validation.Errors[0].Source, Is.EqualTo("Content/Fireball.asset"));
        }

        [Test]
        public void Build_ThrowsWhenValidationFails()
        {
            var builder = new ContentCatalogBuilder();
            builder.AddAbility(Ability(1, new AbilityEffect(AbilityEffectType.Damage, 0)));

            Assert.Throws<System.InvalidOperationException>(() => builder.Build());
        }

        [Test]
        public void Catalog_ResolvesArchetypeLoadoutInCastOrder()
        {
            var builder = new ContentCatalogBuilder();
            builder.AddAbility(Ability(5, new AbilityEffect(AbilityEffectType.Damage, 9)));
            builder.AddAbility(Ability(1, new AbilityEffect(AbilityEffectType.Damage, 4)));
            builder.AddArchetypeLoadout(ActorArchetype.Bruiser, new[] { 1, 5 });

            var catalog = builder.Build();
            var abilities = catalog.AbilitiesFor(ActorArchetype.Bruiser);

            Assert.That(abilities.Length, Is.EqualTo(2));
            Assert.That(abilities[0].Id, Is.EqualTo(1));
            Assert.That(abilities[1].Id, Is.EqualTo(5));
        }

        [Test]
        public void Catalog_ExposesAbilitiesById()
        {
            var builder = new ContentCatalogBuilder();
            builder.AddAbility(Ability(3, new AbilityEffect(AbilityEffectType.Heal, 6)));

            var catalog = builder.Build();

            Assert.That(catalog.AbilityCount, Is.EqualTo(1));
            Assert.That(catalog.TryGetAbility(3, out var ability), Is.True);
            Assert.That(ability.Range, Is.EqualTo(1f));
            Assert.That(catalog.TryGetAbility(4, out _), Is.False);
        }

        [Test]
        public void Catalog_ReturnsEmptyLoadoutForUnregisteredArchetype()
        {
            var builder = new ContentCatalogBuilder();
            builder.AddAbility(Ability(1, new AbilityEffect(AbilityEffectType.Damage, 4)));
            builder.AddArchetypeLoadout(ActorArchetype.Bruiser, new[] { 1 });

            var catalog = builder.Build();

            Assert.That(catalog.AbilitiesFor(ActorArchetype.None), Is.Empty);
            Assert.That(catalog.AbilitiesFor(ActorArchetype.Support), Is.Empty);
        }

        private static ContentCatalogBuilder BuildInvalid()
        {
            var builder = new ContentCatalogBuilder();
            builder.AddAbility(Ability(5, new AbilityEffect(AbilityEffectType.Poison, 0)), "b.asset");
            builder.AddAbility(Ability(2, new AbilityEffect(AbilityEffectType.Damage, 1, 4)), "a.asset");
            builder.AddArchetypeLoadout(ActorArchetype.Support, new[] { 77, 78 }, "s.asset");
            return builder;
        }

        private static AbilityDefinition Ability(int id, params AbilityEffect[] effects) =>
            new AbilityDefinition(id, 10, 1f, AbilityTargetMode.CurrentTarget, effects);

        private static List<string> Codes(ContentValidationResult validation)
        {
            var codes = new List<string>(validation.Errors.Count);
            for (var index = 0; index < validation.Errors.Count; index++)
                codes.Add(validation.Errors[index].Code);
            return codes;
        }

        private static int Count(ContentValidationResult validation, string code)
        {
            var count = 0;
            for (var index = 0; index < validation.Errors.Count; index++)
            {
                if (validation.Errors[index].Code == code)
                    count++;
            }

            return count;
        }
    }
}
