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

        private SimulationHost<RpgSimulationState> _host;
        private ActorViewRegistry _registry;
        private SimulationUnityBridge _bridge;
        private readonly List<ISimulationClient> _clients = new();

        private void Start()
        {
            ConfigureCamera();
            var state = new RpgSimulationState();
            var application = new RpgSimulationApplication();
            _host = new SimulationHost<RpgSimulationState>(state, application, new SimulationOptions(tickRate));
            _registry = new ActorViewRegistry();
            var viewRoot = new GameObject("ActorViews");
            var floatingTextRoot = new GameObject("FloatingCombatTextPool");
            var floatingTextPool = floatingTextRoot.AddComponent<FloatingCombatTextPool>();
            var driver = gameObject.AddComponent<TurnBattleDemoDriver>();
            _bridge = gameObject.AddComponent<SimulationUnityBridge>();

            var orderedIds = new List<SimulationEntityId>(actorsPerFaction * 2);
            var clientsByActor = new Dictionary<SimulationEntityId, ISimulationClient>();
            var redIds = new List<SimulationEntityId>(actorsPerFaction);
            var blueIds = new List<SimulationEntityId>(actorsPerFaction);
            for (var index = 0; index < actorsPerFaction; index++)
            {
                redIds.Add(SpawnActor(state, clientsByActor, viewRoot.transform, floatingTextPool, FactionId.Red, index));
                blueIds.Add(SpawnActor(state, clientsByActor, viewRoot.transform, floatingTextPool, FactionId.Blue, index));
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

        private static void ConfigureCamera()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                var cameraObject = new GameObject("BattleCamera");
                camera = cameraObject.AddComponent<Camera>();
                cameraObject.tag = "MainCamera";
            }

            camera.orthographic = true;
            camera.orthographicSize = 5.25f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.05f, 0.09f, 1f);
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.transform.rotation = Quaternion.identity;
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

            var viewObject = new GameObject($"{faction}_{index}");
            viewObject.transform.SetParent(viewRoot, false);
            viewObject.transform.position = new Vector3(
                faction == FactionId.Red ? -2f : 2f,
                (index - (actorsPerFaction - 1) * 0.5f) * 1.5f,
                0f);
            var view = viewObject.AddComponent<ActorView>();
            view.Initialize(id, faction, floatingTextPool);
            _registry.Add(id, view);
            return id;
        }
    }
}
