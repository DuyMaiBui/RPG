using System;
using System.Collections.Generic;
using RPG.Core.Actors;
using RPG.Core.Navigation;
using RPG.Core.Physics;
using RPG.Content;
using RPG.Simulation.Contracts;

namespace RPG.Core.Tests
{
    /// <summary>Authored data of the Moba demo battle, transcribed from
    /// <c>Assets/Scenes/MobaBattleDemo.unity</c>, the Moba battle rules asset, the Moba content library and the
    /// actor view prefab. It lets a headless test run the real scenario - real map geometry, real unit stats, real
    /// abilities - without loading a Unity scene. <see cref="NavigationFingerprint"/> is the walkability hash the
    /// runtime navigation grid produces from the same authoring, so a wrong transcription fails the scenario test.
    /// </summary>
    internal static class MobaScenarioData
    {
        public const int TickRate = 30;
        public const int NavigationWidth = 60;
        public const int NavigationHeight = 40;
        public const float NavigationCellSize = 1f;
        public const float BaseOffset = 26f;
        public const int ActorsPerFaction = 0;

        public const int MaximumHealth = 30;
        public const int AttackPower = 5;
        public const float MoveSpeed = 0.8f;
        public const float VisionRange = 45f;
        public const float ActorRadius = 0.35f;
        public const float SessionAttackCooldown = 0.8f;

        /// <summary>Melee loadout health multiplier from the demo scenes: the melee rank is the one that has to cross
        /// the range where ranged abilities already hit it.</summary>
        public const float MeleeHealthMultiplier = 2f;

        public const int MeleeCleaveDamage = 4;

        public const bool EnableWaves = true;
        public const int MeleePerWave = 3;
        public const int RangedPerWave = 3;
        public const float WaveUnitInterval = 0.5f;
        public const float WaveInterval = 10f;
        public const float WaveInitialDelay = 0f;
        public const float WaveAttackCooldown = 1f;

        public const float MeleeAttackRange = 0.25f;
        public const float RangedAttackRange = 3.5f;
        public const float ProjectileSpeed = 5f;
        public const float ProjectileRadius = 0.05f;
        public const float ProjectileLifetime = 5f;

        public const uint NavigationFingerprint = 869631345u;
        public const int NavigationBlockedSamples = 6353;

        public static NavigationObstacle[] CreateObstacles() =>
            new[]
            {
            new NavigationObstacle(0, new SimulationVector2(-20.0f, -7.0f), CollisionShape.Polygon(new[] { new SimulationVector2(-2.0f, -1.0f), new SimulationVector2(-1.0f, -1.8f), new SimulationVector2(0.8f, -1.6f), new SimulationVector2(2.0f, -0.5f), new SimulationVector2(1.5f, 1.2f), new SimulationVector2(0.0f, 1.8f), new SimulationVector2(-1.6f, 1.2f) }, 3.560472f)),
            new NavigationObstacle(1, new SimulationVector2(-5.0f, 0.0f), CollisionShape.Polygon(new[] { new SimulationVector2(-2.0f, -1.0f), new SimulationVector2(-1.0f, -1.8f), new SimulationVector2(0.8f, -1.6f), new SimulationVector2(2.0f, -0.5f), new SimulationVector2(1.5f, 1.2f), new SimulationVector2(0.0f, 1.8f), new SimulationVector2(-1.6f, 1.2f) }, 2.670354f)),
            new NavigationObstacle(2, new SimulationVector2(20.0f, 7.0f), CollisionShape.Polygon(new[] { new SimulationVector2(-1.6f, -1.8f), new SimulationVector2(1.0f, -1.5f), new SimulationVector2(2.0f, 0.0f), new SimulationVector2(1.2f, 1.7f), new SimulationVector2(-0.8f, 1.5f), new SimulationVector2(-2.0f, 0.3f) }, 2.076942f)),
            new NavigationObstacle(3, new SimulationVector2(-10.0f, 6.0f), CollisionShape.Polygon(new[] { new SimulationVector2(-2.0f, -1.5f), new SimulationVector2(1.8f, -1.2f), new SimulationVector2(1.5f, 1.2f), new SimulationVector2(0.0f, 2.0f), new SimulationVector2(-1.8f, 1.0f) }, 1.48353f)),
            new NavigationObstacle(4, new SimulationVector2(5.0f, 0.0f), CollisionShape.Polygon(new[] { new SimulationVector2(-1.6f, -1.8f), new SimulationVector2(1.0f, -1.5f), new SimulationVector2(2.0f, 0.0f), new SimulationVector2(1.2f, 1.7f), new SimulationVector2(-0.8f, 1.5f), new SimulationVector2(-2.0f, 0.3f) }, 2.96706f)),
            new NavigationObstacle(5, new SimulationVector2(-10.0f, -6.0f), CollisionShape.Polygon(new[] { new SimulationVector2(-1.6f, -1.8f), new SimulationVector2(1.0f, -1.5f), new SimulationVector2(2.0f, 0.0f), new SimulationVector2(1.2f, 1.7f), new SimulationVector2(-0.8f, 1.5f), new SimulationVector2(-2.0f, 0.3f) }, 3.857178f)),
            new NavigationObstacle(6, new SimulationVector2(-8.0f, 14.0f), CollisionShape.Polygon(new[] { new SimulationVector2(-1.6f, -1.8f), new SimulationVector2(1.0f, -1.5f), new SimulationVector2(2.0f, 0.0f), new SimulationVector2(1.2f, 1.7f), new SimulationVector2(-0.8f, 1.5f), new SimulationVector2(-2.0f, 0.3f) }, 0.296706f)),
            new NavigationObstacle(7, new SimulationVector2(-15.0f, 0.0f), CollisionShape.Polygon(new[] { new SimulationVector2(-2.0f, -1.5f), new SimulationVector2(1.8f, -1.2f), new SimulationVector2(1.5f, 1.2f), new SimulationVector2(0.0f, 2.0f), new SimulationVector2(-1.8f, 1.0f) }, 2.373648f)),
            new NavigationObstacle(8, new SimulationVector2(15.0f, 0.0f), CollisionShape.Polygon(new[] { new SimulationVector2(-2.0f, -1.5f), new SimulationVector2(1.8f, -1.2f), new SimulationVector2(1.5f, 1.2f), new SimulationVector2(0.0f, 2.0f), new SimulationVector2(-1.8f, 1.0f) }, 3.263766f)),
            new NavigationObstacle(9, new SimulationVector2(15.0f, -14.0f), CollisionShape.Polygon(new[] { new SimulationVector2(-1.6f, -1.8f), new SimulationVector2(1.0f, -1.5f), new SimulationVector2(2.0f, 0.0f), new SimulationVector2(1.2f, 1.7f), new SimulationVector2(-0.8f, 1.5f), new SimulationVector2(-2.0f, 0.3f) }, 5.637414f)),
            new NavigationObstacle(10, new SimulationVector2(3.0f, -14.0f), CollisionShape.Polygon(new[] { new SimulationVector2(-2.0f, -1.0f), new SimulationVector2(-1.0f, -1.8f), new SimulationVector2(0.8f, -1.6f), new SimulationVector2(2.0f, -0.5f), new SimulationVector2(1.5f, 1.2f), new SimulationVector2(0.0f, 1.8f), new SimulationVector2(-1.6f, 1.2f) }, 5.340707f)),
            new NavigationObstacle(11, new SimulationVector2(-18.0f, 14.0f), CollisionShape.Polygon(new[] { new SimulationVector2(-2.0f, -1.0f), new SimulationVector2(-1.0f, -1.8f), new SimulationVector2(0.8f, -1.6f), new SimulationVector2(2.0f, -0.5f), new SimulationVector2(1.5f, 1.2f), new SimulationVector2(0.0f, 1.8f), new SimulationVector2(-1.6f, 1.2f) }, 0.0f)),
            new NavigationObstacle(12, new SimulationVector2(10.0f, -6.0f), CollisionShape.Polygon(new[] { new SimulationVector2(-2.0f, -1.5f), new SimulationVector2(1.8f, -1.2f), new SimulationVector2(1.5f, 1.2f), new SimulationVector2(0.0f, 2.0f), new SimulationVector2(-1.8f, 1.0f) }, 4.153883f)),
            new NavigationObstacle(13, new SimulationVector2(-18.0f, -14.0f), CollisionShape.Polygon(new[] { new SimulationVector2(-1.6f, -1.8f), new SimulationVector2(1.0f, -1.5f), new SimulationVector2(2.0f, 0.0f), new SimulationVector2(1.2f, 1.7f), new SimulationVector2(-0.8f, 1.5f), new SimulationVector2(-2.0f, 0.3f) }, 4.747295f)),
            new NavigationObstacle(14, new SimulationVector2(10.0f, 6.0f), CollisionShape.Polygon(new[] { new SimulationVector2(-2.0f, -1.0f), new SimulationVector2(-1.0f, -1.8f), new SimulationVector2(0.8f, -1.6f), new SimulationVector2(2.0f, -0.5f), new SimulationVector2(1.5f, 1.2f), new SimulationVector2(0.0f, 1.8f), new SimulationVector2(-1.6f, 1.2f) }, 1.780236f)),
            new NavigationObstacle(15, new SimulationVector2(3.0f, 14.0f), CollisionShape.Polygon(new[] { new SimulationVector2(-2.0f, -1.5f), new SimulationVector2(1.8f, -1.2f), new SimulationVector2(1.5f, 1.2f), new SimulationVector2(0.0f, 2.0f), new SimulationVector2(-1.8f, 1.0f) }, 0.593412f)),
            new NavigationObstacle(16, new SimulationVector2(-8.0f, -14.0f), CollisionShape.Polygon(new[] { new SimulationVector2(-2.0f, -1.5f), new SimulationVector2(1.8f, -1.2f), new SimulationVector2(1.5f, 1.2f), new SimulationVector2(0.0f, 2.0f), new SimulationVector2(-1.8f, 1.0f) }, 5.044002f)),
            new NavigationObstacle(17, new SimulationVector2(15.0f, 14.0f), CollisionShape.Polygon(new[] { new SimulationVector2(-2.0f, -1.0f), new SimulationVector2(-1.0f, -1.8f), new SimulationVector2(0.8f, -1.6f), new SimulationVector2(2.0f, -0.5f), new SimulationVector2(1.5f, 1.2f), new SimulationVector2(0.0f, 1.8f), new SimulationVector2(-1.6f, 1.2f) }, 0.890118f)),
            new NavigationObstacle(18, new SimulationVector2(-20.0f, 7.0f), CollisionShape.Polygon(new[] { new SimulationVector2(-1.6f, -1.8f), new SimulationVector2(1.0f, -1.5f), new SimulationVector2(2.0f, 0.0f), new SimulationVector2(1.2f, 1.7f), new SimulationVector2(-0.8f, 1.5f), new SimulationVector2(-2.0f, 0.3f) }, 1.186824f)),
            new NavigationObstacle(19, new SimulationVector2(20.0f, -7.0f), CollisionShape.Polygon(new[] { new SimulationVector2(-2.0f, -1.0f), new SimulationVector2(-1.0f, -1.8f), new SimulationVector2(0.8f, -1.6f), new SimulationVector2(2.0f, -0.5f), new SimulationVector2(1.5f, 1.2f), new SimulationVector2(0.0f, 1.8f), new SimulationVector2(-1.6f, 1.2f) }, 4.45059f)),
            };

        /// <summary>Actor collider from the actor view prefab: a body circle plus a small box offset above it.</summary>
        public static ColliderShapeData[] CreateActorColliderShapes() =>
            new[]
            {
                new ColliderShapeData(CollisionShape.Circle(0.35f), new SimulationVector2(0f, 0f), 0f,
                    ColliderMode.Solid, new ColliderFilter(0, -1)),
                new ColliderShapeData(CollisionShape.Box(new SimulationVector2(0.16f, 0.1f)),
                    new SimulationVector2(0f, 0.22f), 0f, ColliderMode.Solid, new ColliderFilter(0, -1)),
            };

        /// <summary>Content library of the Moba scene: Cleave and ConcussiveBlow for the bruiser, VenomStrike for
        /// the skirmisher.</summary>
        public static ContentCatalog CreateCatalog()
        {
            var cleave = new AbilityDefinition(1, 4, 1.2f, AbilityTargetMode.CurrentTarget,
                new[] { new AbilityEffect(AbilityEffectType.Damage, MeleeCleaveDamage) });
            var concussiveBlow = new AbilityDefinition(5, 9, 1.2f, AbilityTargetMode.CurrentTarget,
                new[]
                {
                    new AbilityEffect(AbilityEffectType.Damage, 4),
                    new AbilityEffect(AbilityEffectType.Stun, 1, 4),
                });
            var venomStrike = new AbilityDefinition(2, 6, 4f, AbilityTargetMode.CurrentTarget,
                new[]
                {
                    new AbilityEffect(AbilityEffectType.Damage, 3),
                    new AbilityEffect(AbilityEffectType.Poison, 2, 8),
                });

            var abilities = new Dictionary<int, AbilityDefinition>
            {
                { 1, cleave },
                { 5, concussiveBlow },
                { 2, venomStrike },
            };
            var loadouts = new Dictionary<ActorArchetype, AbilityDefinition[]>
            {
                { ActorArchetype.Bruiser, new[] { cleave, concussiveBlow } },
                { ActorArchetype.Skirmisher, new[] { venomStrike } },
            };
            return new ContentCatalog(abilities, loadouts);
        }

        public static int MeleeMaximumHealth =>
            Math.Max(1, (int)Math.Round(MaximumHealth * (double)MeleeHealthMultiplier));

        public static ActorSpawnData CreateMeleeSpawn(
            ContentCatalog catalog,
            SimulationVector2 position,
            float attackCooldown) =>
            new ActorSpawnData(
                MeleeMaximumHealth,
                AttackPower,
                position,
                ActorRadius,
                MoveSpeed,
                VisionRange,
                MeleeAttackRange,
                attackCooldown,
                attackType: AttackType.Melee,
                colliderShapes: CreateActorColliderShapes(),
                abilities: catalog.AbilitiesFor(ActorArchetype.Bruiser));

        public static ActorSpawnData CreateRangedSpawn(
            ContentCatalog catalog,
            SimulationVector2 position,
            float attackCooldown) =>
            new ActorSpawnData(
                MaximumHealth,
                AttackPower,
                position,
                ActorRadius,
                MoveSpeed,
                VisionRange,
                RangedAttackRange,
                attackCooldown,
                attackType: AttackType.Projectile,
                projectileSpeed: ProjectileSpeed,
                projectileRadius: ProjectileRadius,
                projectileLifetime: ProjectileLifetime,
                colliderShapes: CreateActorColliderShapes(),
                abilities: catalog.AbilitiesFor(ActorArchetype.Skirmisher));

        /// <summary>Builds the battle the way <c>BattleSession</c> does: obstacles, bases, waves and the initial
        /// per-faction actors. <paramref name="withObstacles"/> is only ever false for the A/B measurement that
        /// isolates terrain pinning from crowd behaviour.</summary>
        public static RpgSimulationState CreateState(bool withObstacles = true)
        {
            var state = new RpgSimulationState(NavigationWidth, NavigationHeight, NavigationCellSize);
            if (withObstacles)
            {
                var obstacles = CreateObstacles();
                for (var index = 0; index < obstacles.Length; index++)
                    state.Navigation.ApplyObstacle(obstacles[index]);
            }

            var redBase = new SimulationVector2(-BaseOffset, 0f);
            var blueBase = new SimulationVector2(BaseOffset, 0f);
            state.ConfigureBases(redBase, blueBase, ActorRadius * 2f);

            if (!EnableWaves) return state;
            var catalog = CreateCatalog();
            state.Waves.Configure(
                MeleePerWave,
                RangedPerWave,
                WaveUnitInterval,
                WaveInterval,
                WaveInitialDelay,
                CreateMeleeSpawn(catalog, SimulationVector2.Zero, WaveAttackCooldown),
                CreateRangedSpawn(catalog, SimulationVector2.Zero, WaveAttackCooldown),
                CreateMeleeSpawn(catalog, SimulationVector2.Zero, WaveAttackCooldown),
                CreateRangedSpawn(catalog, SimulationVector2.Zero, WaveAttackCooldown),
                new SimulationVector2(redBase.X + ActorRadius * 3f, redBase.Y),
                new SimulationVector2(blueBase.X - ActorRadius * 3f, blueBase.Y));
            return state;
        }

        public static EntityId Spawn(
            RpgSimulationState state,
            ContentCatalog catalog,
            FactionId faction,
            AttackType attackType,
            SimulationVector2 position,
            float attackCooldown)
        {
            var data = attackType == AttackType.Melee
                ? CreateMeleeSpawn(catalog, position, attackCooldown)
                : CreateRangedSpawn(catalog, position, attackCooldown);
            return state.Actors.Spawn(
                faction == FactionId.Red ? ActorKind.Player : ActorKind.Monster, faction, data);
        }
    }
}
