using System.Collections.Generic;
using RPG.Core.Actors;
using RPG.Simulation.Contracts;
using RPG.Simulation.Runtime;
using SimulationEntityId = RPG.Simulation.Contracts.EntityId;
using UnityEngine;

namespace RPG.Unity
{
    public sealed class SimulationUnityBridge : MonoBehaviour
    {
        private ISimulationClient _client;
        private ActorViewRegistry _registry;
        private ProjectileViewRegistry _projectiles;
        private TurnBattleDemoDriver _driver;
        private PredictionBuffer _prediction;
        private SimulationEntityId _localActorId;
        private float _predictionSpeed;
        private float _predictionDelta;
        private long _nextSequence;
        private SimulationTick _lastServerTick;
        private readonly Dictionary<SimulationEntityId, float> _pendingRemoval = new();

        public void Initialize(
            ISimulationClient client,
            ActorViewRegistry registry,
            TurnBattleDemoDriver driver,
            ProjectileView projectilePrefab,
            Transform projectileRoot,
            SimulationEntityId localActorId,
            float predictionSpeed,
            float fixedDeltaTime)
        {
            if (projectilePrefab == null || projectileRoot == null)
                throw new System.InvalidOperationException("SimulationUnityBridge projectile references are incomplete.");

            _client = client;
            _registry = registry;
            _driver = driver;
            _projectiles = new ProjectileViewRegistry(projectilePrefab, projectileRoot);
            _prediction = new PredictionBuffer(64);
            _localActorId = localActorId;
            _predictionSpeed = predictionSpeed;
            _predictionDelta = fixedDeltaTime;
            _nextSequence = 0;
        }

        private void Update()
        {
            if (_client == null || !_client.TryRead(out var update))
            {
                ProcessPendingRemoval();
                return;
            }

            if (!(update.Payload is WorldFrameUpdate frame)) return;
            _lastServerTick = update.ServerTick;
            _projectiles.Apply(frame.Projectiles);
            var liveIds = new HashSet<SimulationEntityId>();
            foreach (var snapshot in frame.Actors.Span)
            {
                liveIds.Add(snapshot.Entity);
                if (_registry.TryGet(snapshot.Entity, out var view))
                    view.ApplySnapshot(snapshot);
                if (snapshot.Entity == _localActorId && _registry.TryGet(snapshot.Entity, out var localView))
                {
                    var predicted = _prediction.Reconcile(
                        snapshot.Position,
                        update.LastProcessedClientSequence,
                        _predictionSpeed,
                        _predictionDelta);
                    localView.ApplyPredictedPosition(predicted);
                }
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
            _projectiles?.ReleaseAll();
            _projectiles = null;
            _prediction?.Clear();
            _prediction = null;
            _client = null;
        }

        public bool TrySendMoveIntent(Vector2 direction)
        {
            if (_client == null || _prediction == null || direction.sqrMagnitude <= 0.0001f)
                return false;

            var sequence = new ClientSequence(_nextSequence++);
            var intent = new SimulationVector2(direction.x, direction.y).Normalized();
            var command = new ClientCommandEnvelope(
                ProtocolVersion.Current,
                sequence,
                _lastServerTick,
                new MoveIntentCommand(intent));
            if (!_client.TrySend(command)) return false;

            _prediction.Record(new PredictedMoveInput(sequence, _lastServerTick, intent));
            return true;
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
