using System;

namespace RPG.Core.Actors
{
    /// <summary>Default ability set and archetype loadouts. Definitions are immutable and shared between actors, so the
    /// ability component can hold them directly.</summary>
    public static class AbilityCatalog
    {
        public static AbilityDefinition Cleave { get; } = new AbilityDefinition(
            1,
            4,
            1.2f,
            AbilityTargetMode.CurrentTarget,
            new[] { new AbilityEffect(AbilityEffectType.Damage, 8) });

        public static AbilityDefinition VenomStrike { get; } = new AbilityDefinition(
            2,
            6,
            4f,
            AbilityTargetMode.CurrentTarget,
            new[]
            {
                new AbilityEffect(AbilityEffectType.Damage, 3),
                new AbilityEffect(AbilityEffectType.Poison, 2, 8),
            });

        public static AbilityDefinition Mend { get; } = new AbilityDefinition(
            3,
            8,
            0f,
            AbilityTargetMode.Self,
            new[] { new AbilityEffect(AbilityEffectType.Heal, 6) });

        public static AbilityDefinition Hamstring { get; } = new AbilityDefinition(
            4,
            7,
            3f,
            AbilityTargetMode.CurrentTarget,
            new[] { new AbilityEffect(AbilityEffectType.Slow, 30, 6) });

        private static readonly AbilityDefinition[] BruiserAbilities = { Cleave };
        private static readonly AbilityDefinition[] SkirmisherAbilities = { VenomStrike };
        private static readonly AbilityDefinition[] SupportAbilities = { Mend, Hamstring };
        private static readonly AbilityDefinition[] NoAbilities = Array.Empty<AbilityDefinition>();

        /// <summary>Abilities granted to an archetype, in cast-priority order (lowest index casts first).</summary>
        public static AbilityDefinition[] Abilities(ActorArchetype archetype) => archetype switch
        {
            ActorArchetype.Bruiser => BruiserAbilities,
            ActorArchetype.Skirmisher => SkirmisherAbilities,
            ActorArchetype.Support => SupportAbilities,
            _ => NoAbilities,
        };
    }
}
