using System;
using System.Collections.Generic;
using RPG.Core.Actors;
using RPG.Core.Navigation;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace RPG.Unity
{
    /// <summary>Composition root for one battle. It owns the scene's serialized references, builds the session scope
    /// and lets the container create and dispose <see cref="BattleSession"/>. It runs no gameplay itself; missing or
    /// invalid authoring throws here instead of being repaired.</summary>
    public sealed class BattleSessionLifetimeScope : LifetimeScope
    {
        [SerializeField] private BattleRulesAsset battleRules;
        [SerializeField] private ContentLibraryAsset content;
        [SerializeField] private bool enableWaves;
        [SerializeField] private int meleePerWave = 3;
        [SerializeField] private int rangedPerWave = 3;
        [SerializeField] private float waveUnitInterval = 0.5f;
        [SerializeField] private float waveInterval = 10f;
        [SerializeField] private float waveInitialDelay;
        [SerializeField] private int maximumHealth = 30;
        [SerializeField] private int attackPower = 5;
        [SerializeField] private float moveSpeed = 0.8f;
        [SerializeField] private float visionRange = 6f;
        [SerializeField] private float actorRadius = 0.35f;
        [SerializeField] private ActorLoadoutAuthoring[] actorLoadouts =
        {
            new ActorLoadoutAuthoring(AttackType.Melee, 0.25f, 0f, 0.05f, 0f),
            new ActorLoadoutAuthoring(AttackType.Projectile, 3.5f, 5f, 0.05f, 5f),
        };
        [SerializeField] private ActorView actorPrefab;
        [SerializeField] private Transform actorRoot;
        [SerializeField] private FloatingCombatTextPool floatingTextPool;
        [SerializeField] private SimulationUnityBridge bridge;
        [SerializeField] private ProjectileView projectilePrefab;
        [SerializeField] private Transform projectileRoot;
        [SerializeField] private ColliderCompoundAuthoring[] navigationColliders = new ColliderCompoundAuthoring[0];

        protected override void Awake()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            Application.runInBackground = true;
            base.Awake();
        }

        protected override void Configure(IContainerBuilder builder)
        {
            if (battleRules == null || content == null)
                throw new InvalidOperationException("BattleSessionLifetimeScope requires battle rules and a content library.");
            if (bridge == null || actorPrefab == null || actorRoot == null || floatingTextPool == null ||
                projectilePrefab == null || projectileRoot == null)
                throw new InvalidOperationException("BattleSessionLifetimeScope scene references are incomplete.");

            if (!content.TryBuild(out var catalog, out var validation))
            {
                throw new InvalidOperationException(
                    $"BattleSessionLifetimeScope content library is invalid.\n{validation.Describe()}");
            }

            builder.RegisterInstance(battleRules);
            builder.RegisterInstance(catalog);
            builder.RegisterInstance(CreateSetup());
            builder.RegisterInstance(bridge);
            builder.Register(_ => new ActorViewRegistry(actorPrefab, actorRoot, floatingTextPool), Lifetime.Singleton);
            builder.RegisterEntryPoint<BattleSession>(Lifetime.Singleton);
        }

        private BattleSessionSetup CreateSetup()
        {
            var authoring = actorPrefab == null ? null : actorPrefab.GetComponent<ColliderCompoundAuthoring>();
            var actorColliderShapes = authoring == null ? null : authoring.CreateCompoundShapes();

            var navigationObstacles = new List<NavigationObstacle>();
            for (var index = 0; index < navigationColliders.Length; index++)
            {
                var collider = navigationColliders[index];
                if (collider == null)
                    continue;

                var created = collider.CreateNavigationObstacles(index * 1000);
                for (var obstacleIndex = 0; obstacleIndex < created.Length; obstacleIndex++)
                    navigationObstacles.Add(created[obstacleIndex]);
            }

            return new BattleSessionSetup(
                maximumHealth: maximumHealth,
                attackPower: attackPower,
                moveSpeed: moveSpeed,
                visionRange: visionRange,
                actorRadius: actorRadius,
                enableWaves: enableWaves,
                meleePerWave: meleePerWave,
                rangedPerWave: rangedPerWave,
                waveUnitInterval: waveUnitInterval,
                waveInterval: waveInterval,
                waveInitialDelay: waveInitialDelay,
                actorLoadouts: actorLoadouts,
                actorColliderShapes: actorColliderShapes,
                navigationObstacles: navigationObstacles.ToArray(),
                projectilePrefab: projectilePrefab,
                projectileRoot: projectileRoot);
        }
    }
}
