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
        [SerializeField] private int tickRate = 10;
        [SerializeField] private ActorView actorPrefab;
        [SerializeField] private Transform actorRoot;
        [SerializeField] private FloatingCombatTextPool floatingTextPool;
        [SerializeField] private TurnBattleDemoDriver driver;
        [SerializeField] private SimulationUnityBridge bridge;

        private SimulationHost<RpgSimulationState> _host;
        private ActorViewRegistry _registry;
        private SimulationUnityBridge _bridge;
        private readonly List<ISimulationClient> _clients = new();

        private void Start()
        {
            if (actorPrefab == null || actorRoot == null || floatingTextPool == null || driver == null || bridge == null)
                throw new InvalidOperationException("BattleDemoBootstrap scene references are incomplete.");

            var state = new RpgSimulationState();
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

            state.Turns.Initialize(orderedIds);
            driver.Initialize(clientsByActor);
            _bridge.Initialize(clientsByActor[orderedIds[0]], _registry, driver);
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
            var id = state.Actors.Spawn(kind, faction, maximumHealth, attackPower);
            var player = new PlayerId($"{faction}-{index}");
            state.PlayerActors.Add(player, id);
            var session = new SessionContext(new SessionId(Guid.NewGuid()), player);
            var client = new LocalSimulationClient<RpgSimulationState>(_host, session);
            _clients.Add(client);
            clientsByActor.Add(id, client);

            var view = Instantiate(actorPrefab, viewRoot);
            view.name = $"{faction}_{index}";
            view.transform.position = new Vector3(
                faction == FactionId.Red ? -2f : 2f,
                (index - (actorsPerFaction - 1) * 0.5f) * 1.5f,
                0f);
            view.Initialize(id, faction, floatingTextPool);
            _registry.Add(id, view);
            return id;
        }
    }
}
