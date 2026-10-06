using System;
using System.Collections.Generic;
using RPG.Content;
using RPG.Core.Actors;
using RPG.Core.Navigation;
using RPG.Core.Physics;
using RPG.Simulation.Contracts;
using RPG.Simulation.Runtime;
using SimulationEntityId = RPG.Simulation.Contracts.EntityId;
using UnityEngine;

namespace RPG.Unity
{
    public sealed class BattleDemoBootstrap : MonoBehaviour
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

        private SimulationHost<RpgSimulationState> _host;
        private ActorViewRegistry _registry;
        private SimulationUnityBridge _bridge;
        private BattleRulesAsset _rules;
        private ContentCatalog _catalog;
        private readonly List<ISimulationClient> _clients = new();
        private readonly List<SimulationVector2> _spawnedPositions = new();
        private ColliderShapeData[] _actorColliderShapes;

        private void Start()
        {
            if (actorPrefab == null || actorRoot == null || floatingTextPool == null || bridge == null ||
                projectilePrefab == null || projectileRoot == null)
                throw new InvalidOperationException("BattleDemoBootstrap scene references are incomplete.");
            if (battleRules == null || content == null)
                throw new InvalidOperationException("BattleDemoBootstrap requires battle rules and a content library.");
            if (actorLoadouts == null || actorLoadouts.Length == 0)
                throw new InvalidOperationException("BattleDemoBootstrap requires at least one actor loadout.");

            if (!content.TryBuild(out _catalog, out var validation))
                throw new InvalidOperationException($"BattleDemoBootstrap content library is invalid.\n{validation.Describe()}");
            _rules = battleRules;

            var state = new RpgSimulationState(_rules.NavigationWidth, _rules.NavigationHeight, _rules.NavigationCellSize);
            for (var index = 0; index < navigationColliders.Length; index++)
            {
                var collider = navigationColliders[index];
                if (collider == null) continue;
                var obstacles = collider.CreateNavigationObstacles(index * 1000);
                for (var obstacleIndex = 0; obstacleIndex < obstacles.Length; obstacleIndex++)
                    state.Navigation.ApplyObstacle(obstacles[obstacleIndex]);
            }

            var actorCollider = actorPrefab.GetComponent<ColliderCompoundAuthoring>();
            _actorColliderShapes = actorCollider == null ? null : actorCollider.CreateCompoundShapes();

            var application = new RpgSimulationApplication();
            _host = new SimulationHost<RpgSimulationState>(state, application, new SimulationOptions(_rules.TickRate));
            _registry = new ActorViewRegistry(actorPrefab, actorRoot, floatingTextPool);
            _bridge = bridge;
            if (enableWaves)
                ConfigureWaves(state);

            var actorsPerFaction = _rules.ActorsPerFaction;
            var orderedIds = new List<SimulationEntityId>(actorsPerFaction * 2);
            var clientsByActor = new Dictionary<SimulationEntityId, ISimulationClient>();
            var redIds = new List<SimulationEntityId>(actorsPerFaction);
            var blueIds = new List<SimulationEntityId>(actorsPerFaction);
            _spawnedPositions.Clear();
            for (var index = 0; index < actorsPerFaction; index++)
            {
                redIds.Add(SpawnActor(state, clientsByActor, actorRoot, floatingTextPool, FactionId.Red, index));
                blueIds.Add(SpawnActor(state, clientsByActor, actorRoot, floatingTextPool, FactionId.Blue, index));
            }

            for (var index = 0; index < actorsPerFaction; index++)
            {
                orderedIds.Add(redIds[index]);
                orderedIds.Add(blueIds[index]);
            }

            ISimulationClient bridgeClient;
            var bridgeActorId = SimulationEntityId.None;
            if (orderedIds.Count > 0)
            {
                bridgeActorId = orderedIds[0];
                bridgeClient = clientsByActor[bridgeActorId];
            }
            else
            {
                bridgeClient = new LocalSimulationClient<RpgSimulationState>(
                    _host,
                    new SessionContext(new SessionId(Guid.NewGuid()), new PlayerId("observer")));
                _clients.Add(bridgeClient);
            }

            _bridge.Initialize(
                bridgeClient,
                _registry,
                projectilePrefab,
                projectileRoot,
                bridgeActorId,
                moveSpeed,
                1f / _rules.TickRate);
            _host.Start();
        }

        private void Awake()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            Application.runInBackground = true;
        }

        private void OnDestroy()
        {
            _bridge?.Release();
            foreach (var client in _clients)
                client.Dispose();
            (_host as IDisposable)?.Dispose();
        }

        private SimulationEntityId SpawnActor(
            RpgSimulationState state,
            Dictionary<SimulationEntityId, ISimulationClient> clientsByActor,
            Transform viewRoot,
            FloatingCombatTextPool floatingTextPool,
            FactionId faction,
            int index)
        {
            var kind = faction == FactionId.Red ? ActorKind.Player : ActorKind.Monster;
            var actorsPerFaction = _rules.ActorsPerFaction;
            var position = new SimulationVector2(
                faction == FactionId.Red ? -2f : 2f,
                (index - (actorsPerFaction - 1) * 0.5f) * 1.5f);
            if (actorsPerFaction > 10)
            {
                var columns = (int)MathF.Ceiling(MathF.Sqrt(actorsPerFaction));
                var row = index / columns;
                var column = index % columns;
                var rows = (int)MathF.Ceiling(actorsPerFaction / (float)columns);
                var spacing = MathF.Max(0.8f, _rules.NavigationCellSize * 0.8f);
                var side = faction == FactionId.Red ? -1f : 1f;
                position = new SimulationVector2(
                    side * _rules.NavigationWidth * _rules.NavigationCellSize * 0.32f - side * column * spacing,
                    (row - (rows - 1) * 0.5f) * spacing);
            }
            position = FindSpawnPosition(state.Navigation, position);
            var loadout = actorLoadouts[index % actorLoadouts.Length];
            if (loadout == null)
                throw new InvalidOperationException($"BattleDemoBootstrap actor loadout at index {index % actorLoadouts.Length} is missing.");
            var id = state.Actors.Spawn(kind, faction, loadout.CreateSpawnData(
                _catalog, maximumHealth, attackPower, position, actorRadius, moveSpeed, visionRange, 0.8f,
                colliderShapes: _actorColliderShapes));
            var player = new PlayerId($"{faction}-{index}");
            state.PlayerActors.Add(player, id);
            var session = new SessionContext(new SessionId(Guid.NewGuid()), player);
            var client = new LocalSimulationClient<RpgSimulationState>(_host, session);
            _clients.Add(client);
            clientsByActor.Add(id, client);

            var view = Instantiate(actorPrefab, viewRoot);
            view.name = $"{faction}_{index}";
            view.transform.position = new Vector3(position.X, position.Y, 0f);
            view.Initialize(id, faction, floatingTextPool);
            _registry.Add(id, view);
            _spawnedPositions.Add(position);
            return id;
        }

        private void ConfigureWaves(RpgSimulationState state)
        {
            if (actorLoadouts.Length < 2 || actorLoadouts[0] == null || actorLoadouts[1] == null)
                throw new InvalidOperationException("Wave mode requires melee and ranged actor loadouts at indices 0 and 1.");

            var resolvedBaseOffset = _rules.ResolveBaseOffset();
            var redBase = new SimulationVector2(-resolvedBaseOffset, 0f);
            var blueBase = new SimulationVector2(resolvedBaseOffset, 0f);
            state.ConfigureBases(redBase, blueBase, actorRadius * 2f);

            var redSpawn = new SimulationVector2(redBase.X + actorRadius * 3f, redBase.Y);
            var blueSpawn = new SimulationVector2(blueBase.X - actorRadius * 3f, blueBase.Y);
            var redMelee = actorLoadouts[0].CreateSpawnData(
                _catalog, maximumHealth, attackPower, SimulationVector2.Zero, actorRadius, moveSpeed, visionRange, 1f,
                colliderShapes: _actorColliderShapes);
            var redRanged = actorLoadouts[1].CreateSpawnData(
                _catalog, maximumHealth, attackPower, SimulationVector2.Zero, actorRadius, moveSpeed, visionRange, 1f,
                colliderShapes: _actorColliderShapes);
            state.Waves.Configure(
                meleePerWave,
                rangedPerWave,
                waveUnitInterval,
                waveInterval,
                waveInitialDelay,
                redMelee,
                redRanged,
                redMelee,
                redRanged,
                redSpawn,
                blueSpawn);
        }

        private SimulationVector2 FindSpawnPosition(NavigationGrid navigation, SimulationVector2 desired)
        {
            const float spacing = 0.8f;
            const int maxSearchRings = 24;
            for (var ring = 0; ring <= maxSearchRings; ring++)
            {
                var radius = ring * spacing;
                var samples = ring == 0 ? 1 : ring * 8;
                for (var sample = 0; sample < samples; sample++)
                {
                    var angle = ring == 0 ? 0f : sample * MathF.PI * 2f / samples;
                    var candidate = new SimulationVector2(
                        desired.X + MathF.Cos(angle) * radius,
                        desired.Y + MathF.Sin(angle) * radius);
                    if (!navigation.IsPositionWalkable(candidate, actorRadius) || IsSpawnOverlapping(candidate))
                        continue;
                    return candidate;
                }
            }

            throw new InvalidOperationException($"Unable to find a walkable spawn position near {desired.X}, {desired.Y}.");
        }

        private bool IsSpawnOverlapping(SimulationVector2 candidate)
        {
            var minimumDistance = actorRadius * 2f;
            var minimumDistanceSquared = minimumDistance * minimumDistance;
            for (var index = 0; index < _spawnedPositions.Count; index++)
            {
                if ((candidate - _spawnedPositions[index]).LengthSquared < minimumDistanceSquared)
                    return true;
            }

            return false;
        }
    }
}
