using System.Collections.Generic;
using RPG.Core.Actors;
using RPG.Simulation.Contracts;
using SimulationEntityId = RPG.Simulation.Contracts.EntityId;
using UnityEngine;

namespace RPG.Unity
{
    public sealed class SimulationUnityBridge : MonoBehaviour
    {
        private ISimulationClient _client;
        private ActorViewRegistry _registry;
        private TurnBattleDemoDriver _driver;
        private readonly Dictionary<SimulationEntityId, float> _pendingRemoval = new();

        public void Initialize(ISimulationClient client, ActorViewRegistry registry, TurnBattleDemoDriver driver)
        {
            _client = client;
            _registry = registry;
            _driver = driver;
        }

        private void Update()
        {
            if (_client == null || !_client.TryRead(out var update))
            {
                ProcessPendingRemoval();
                return;
            }

            if (!(update.Payload is WorldFrameUpdate frame)) return;
            var liveIds = new HashSet<SimulationEntityId>();
            foreach (var snapshot in frame.Actors.Span)
            {
                liveIds.Add(snapshot.Entity);
                if (_registry.TryGet(snapshot.Entity, out var view))
                    view.ApplySnapshot(snapshot);
                _pendingRemoval.Remove(snapshot.Entity);
            }

            foreach (var signal in frame.Signals.Span)
            {
                if (_registry.TryGet(signal.Entity, out var view))
                    view.PlaySignal(signal);

                if (signal.Kind == PresentationSignalKind.Died)
                    _pendingRemoval[signal.Entity] = 0.9f;
            }

            foreach (var entry in _registry.Entries)
            {
                if (!liveIds.Contains(entry.Key) && !_pendingRemoval.ContainsKey(entry.Key))
                    _pendingRemoval[entry.Key] = 0.9f;
            }

            _driver.OnFrame(frame);
            ProcessPendingRemoval();
        }

        public void Release()
        {
            _pendingRemoval.Clear();
            _registry?.ReleaseAll();
            _client = null;
        }

        private void ProcessPendingRemoval()
        {
            if (_registry == null) return;
            var expired = new List<SimulationEntityId>();
            var keys = new List<SimulationEntityId>(_pendingRemoval.Keys);
            foreach (var id in keys)
            {
                _pendingRemoval[id] -= Time.deltaTime;
                if (_pendingRemoval[id] <= 0f)
                    expired.Add(id);
            }

            foreach (var id in expired)
            {
                _pendingRemoval.Remove(id);
                _registry.Remove(id);
            }
        }
    }
}
