using System;
using System.Collections.Generic;
using RPG.Core.Actors;
using RPG.Simulation.Contracts;
using RPG.Simulation.Runtime;
using SimulationEntityId = RPG.Simulation.Contracts.EntityId;
using UnityEngine;

namespace RPG.Unity
{
    public sealed class BattleDemoBootstrap : MonoBehaviour
    {
        [SerializeField] private int actorsPerFaction = 3;
        [SerializeField] private int maximumHealth = 30;
        [SerializeField] private int attackPower = 5;
        [SerializeField] private int tickRate = 30;
        [SerializeField] private float moveSpeed = 0.8f;
        [SerializeField] private ActorLoadoutAuthoring[] actorLoadouts =
        {
            new ActorLoadoutAuthoring(AttackType.Melee, 0.1f, 0f, 0.05f, 0f),
            new ActorLoadoutAuthoring(AttackType.Projectile, 3.5f, 5f, 0.05f, 5f),
        };
        [SerializeField] private ActorView actorPrefab;
        [SerializeField] private Transform actorRoot;
        [SerializeField] private FloatingCombatTextPool floatingTextPool;
        [SerializeField] private TurnBattleDemoDriver driver;
        [SerializeField] private SimulationUnityBridge bridge;
        [SerializeField] private ProjectileView projectilePrefab;
        [SerializeField] private Transform projectileRoot;
        [SerializeField] private NavigationObstacleAuthoring[] navigationObstacles = new NavigationObstacleAuthoring[0];

        private SimulationHost<RpgSimulationState> _host;
        private ActorViewRegistry _registry;
        private SimulationUnityBridge _bridge;
        private readonly List<ISimulationClient> _clients = new();

        private void Start()
        {
            if (actorPrefab == null || actorRoot == null || floatingTextPool == null || driver == null || bridge == null ||
                projectilePrefab == null || projectileRoot == null)
                throw new InvalidOperationException("BattleDemoBootstrap scene references are incomplete.");
            if (actorLoadouts == null || actorLoadouts.Length == 0)
                throw new InvalidOperationException("BattleDemoBootstrap requires at least one actor loadout.");

            var state = new RpgSimulationState();
            for (var index = 0; index < navigationObstacles.Length; index++)
            {
                var obstacle = navigationObstacles[index];
                if (obstacle != null && obstacle.IsEnabled)
                    state.Navigation.ApplyObstacle(obstacle.ToSimulationObstacle());
            }

            var application = new RpgSimulationApplication();
            _host = new SimulationHost<RpgSimulationState>(state, application, new SimulationOptions(tickRate));
            _registry = new ActorViewRegistry();
            _bridge = bridge;

            var orderedIds = new List<SimulationEntityId>(actorsPerFaction * 2);
            var clientsByActor = new Dictionary<SimulationEntityId, ISimulationClient>();
            var redIds = new List<SimulationEntityId>(actorsPerFaction);
            var blueIds = new List<SimulationEntityId>(actorsPerFaction);
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

            driver.Initialize(clientsByActor);
            _bridge.Initialize(
                clientsByActor[orderedIds[0]],
                _registry,
                driver,
                projectilePrefab,
                projectileRoot,
                orderedIds[0],
                moveSpeed,
                1f / tickRate);
            _host.Start();
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
            var position = new SimulationVector2(
                faction == FactionId.Red ? -2f : 2f,
                (index - (actorsPerFaction - 1) * 0.5f) * 1.5f);
            var loadout = actorLoadouts[index % actorLoadouts.Length];
            if (loadout == null)
                throw new InvalidOperationException($"BattleDemoBootstrap actor loadout at index {index % actorLoadouts.Length} is missing.");
            var id = state.Actors.Spawn(kind, faction, loadout.CreateSpawnData(
                maximumHealth, attackPower, position, 0.35f, moveSpeed, 6f, 0.8f));
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
            return id;
        }
    }
}
