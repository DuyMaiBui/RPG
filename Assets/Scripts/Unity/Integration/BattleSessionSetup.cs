using System;
using RPG.Core.Navigation;
using RPG.Core.Physics;
using UnityEngine;

namespace RPG.Unity
{
    /// <summary>Everything a <see cref="BattleSession"/> takes from the scene: the serialized view references plus the
    /// per-battle tuning that is not authored content yet. Built once by <see cref="BattleSessionLifetimeScope"/> so
    /// the session itself never touches scene objects directly. Per-actor stats move to the stats pipeline and wave
    /// composition moves to encounter content in later phases.</summary>
    public sealed class BattleSessionSetup
    {
        public BattleSessionSetup(
            int maximumHealth,
            int attackPower,
            float moveSpeed,
            float visionRange,
            float actorRadius,
            bool enableWaves,
            int meleePerWave,
            int rangedPerWave,
            float waveUnitInterval,
            float waveInterval,
            float waveInitialDelay,
            ActorLoadoutAuthoring[] actorLoadouts,
            ColliderShapeData[] actorColliderShapes,
            NavigationObstacle[] navigationObstacles,
            ProjectileView projectilePrefab,
            Transform projectileRoot)
        {
            if (actorLoadouts == null || actorLoadouts.Length == 0)
                throw new ArgumentException("A battle session requires at least one actor loadout.", nameof(actorLoadouts));
            if (projectilePrefab == null) throw new ArgumentNullException(nameof(projectilePrefab));
            if (projectileRoot == null) throw new ArgumentNullException(nameof(projectileRoot));

            MaximumHealth = maximumHealth;
            AttackPower = attackPower;
            MoveSpeed = moveSpeed;
            VisionRange = visionRange;
            ActorRadius = actorRadius;
            EnableWaves = enableWaves;
            MeleePerWave = meleePerWave;
            RangedPerWave = rangedPerWave;
            WaveUnitInterval = waveUnitInterval;
            WaveInterval = waveInterval;
            WaveInitialDelay = waveInitialDelay;
            ActorLoadouts = (ActorLoadoutAuthoring[])actorLoadouts.Clone();
            ActorColliderShapes = actorColliderShapes;
            NavigationObstacles = navigationObstacles ?? Array.Empty<NavigationObstacle>();
            ProjectilePrefab = projectilePrefab;
            ProjectileRoot = projectileRoot;
        }

        public int MaximumHealth { get; }

        public int AttackPower { get; }

        public float MoveSpeed { get; }

        public float VisionRange { get; }

        public float ActorRadius { get; }

        public bool EnableWaves { get; }

        public int MeleePerWave { get; }

        public int RangedPerWave { get; }

        public float WaveUnitInterval { get; }

        public float WaveInterval { get; }

        public float WaveInitialDelay { get; }

        public ActorLoadoutAuthoring[] ActorLoadouts { get; }

        public ColliderShapeData[] ActorColliderShapes { get; }

        public NavigationObstacle[] NavigationObstacles { get; }

        public ProjectileView ProjectilePrefab { get; }

        public Transform ProjectileRoot { get; }
    }
}
