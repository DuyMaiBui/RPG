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
        private SimulationVector2 _localDirection;
        private SimulationVector2 _predictedPosition;
        private bool _hasPredictedPosition;
        private readonly Dictionary<SimulationEntityId, float> _pendingRemoval = new();
        private readonly Dictionary<SimulationEntityId, ActorSnapshot> _snapshots = new();
        private readonly HashSet<SimulationEntityId> _liveIds = new();
        private readonly List<SimulationEntityId> _expiredRemoval = new();
        private readonly List<SimulationEntityId> _pendingKeys = new();

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
            _localDirection = SimulationVector2.Zero;
            _hasPredictedPosition = false;
        }

        private void Update()
        {
            if (_client == null)
            {
                ProcessPendingRemoval();
                return;
            }

            if (_client.TryRead(out var update) && update.Payload is WorldFrameUpdate frame)
            {
                _lastServerTick = update.ServerTick;
                _projectiles.Apply(frame.Projectiles);
                _liveIds.Clear();
                _snapshots.Clear();
                foreach (var snapshot in frame.Actors.Span)
                {
                    _liveIds.Add(snapshot.Entity);
                    _snapshots[snapshot.Entity] = snapshot;
                    if (_registry.TryGet(snapshot.Entity, out var view))
                        view.ApplySnapshot(snapshot);
                    if (snapshot.Entity == _localActorId && _registry.TryGet(snapshot.Entity, out var localView))
                    {
                        _predictedPosition = _prediction.Reconcile(
                            snapshot.Position,
                            update.LastProcessedClientSequence,
                            _predictionSpeed,
                            _predictionDelta);
                        _hasPredictedPosition = true;
                        localView.ApplyPredictedPosition(_predictedPosition);
                    }
                    _pendingRemoval.Remove(snapshot.Entity);
                }

                foreach (var snapshot in frame.Actors.Span)
                {
                    if (!_registry.TryGet(snapshot.Entity, out var view)) continue;
                    var hasTarget = _snapshots.TryGetValue(snapshot.Target, out var targetSnapshot);
                    view.ApplyTargetPosition(
                        hasTarget ? new Vector3(targetSnapshot.Position.X, targetSnapshot.Position.Y, 0f) : Vector3.zero,
                        hasTarget);
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
                    if (!_liveIds.Contains(entry.Key) && !_pendingRemoval.ContainsKey(entry.Key))
                        _pendingRemoval[entry.Key] = 0.9f;
                }

                _driver.OnFrame(frame);
            }

            AdvanceLocalPrediction(Time.deltaTime);
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
            _localDirection = SimulationVector2.Zero;
            _hasPredictedPosition = false;
            _client = null;
        }

        public bool TrySendMoveIntent(Vector2 direction)
        {
            if (_client == null || _prediction == null)
                return false;

            var sequence = new ClientSequence(_nextSequence++);
            var intent = new SimulationVector2(direction.x, direction.y).Normalized();
            var command = new ClientCommandEnvelope(
                ProtocolVersion.Current,
                sequence,
                _lastServerTick,
                new MoveIntentCommand(intent));
            if (!_client.TrySend(command)) return false;

            _localDirection = intent;
            _prediction.Record(new PredictedMoveInput(sequence, _lastServerTick, intent));
            return true;
        }

        private void AdvanceLocalPrediction(float deltaTime)
        {
            if (!_hasPredictedPosition || _localDirection.LengthSquared <= 0.0001f)
                return;

            _predictedPosition += _localDirection * (_predictionSpeed * deltaTime);
            if (_registry.TryGet(_localActorId, out var localView))
                localView.ApplyPredictedPosition(_predictedPosition);
        }

        private void ProcessPendingRemoval()
        {
            if (_registry == null) return;
            _expiredRemoval.Clear();
            _pendingKeys.Clear();
            foreach (var id in _pendingRemoval.Keys)
                _pendingKeys.Add(id);

            foreach (var id in _pendingKeys)
            {
                _pendingRemoval[id] -= Time.deltaTime;
                if (_pendingRemoval[id] <= 0f)
                    _expiredRemoval.Add(id);
            }

            foreach (var id in _expiredRemoval)
            {
                _pendingRemoval.Remove(id);
                _registry.Remove(id);
            }
        }
    }
}
