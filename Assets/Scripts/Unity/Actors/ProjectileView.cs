using RPG.Core.Actors;
using RPG.Simulation.Contracts;
using UnityEngine;

namespace RPG.Unity
{
    public sealed class ProjectileView : MonoBehaviour
    {
        private const float ReconciliationDuration = 0.08f;
        private SimulationVector2 _lastAuthoritativePosition;
        private SimulationVector2 _predictedPosition;
        private SimulationVector2 _velocity;
        private SimulationVector2 _correctionRemaining;
        private bool _hasSnapshot;

        public void ApplySnapshot(ProjectileSnapshot snapshot, float snapshotInterval)
        {
            var position = snapshot.Position;
            if (!_hasSnapshot)
            {
                _lastAuthoritativePosition = position;
                _predictedPosition = position;
                _velocity = SimulationVector2.Zero;
                _correctionRemaining = SimulationVector2.Zero;
                _hasSnapshot = true;
            }
            else
            {
                var measuredVelocity = (position - _lastAuthoritativePosition) /
                                        Mathf.Max(0.0001f, snapshotInterval);
                if (measuredVelocity.LengthSquared > 0.000001f)
                    _velocity = measuredVelocity;

                _correctionRemaining = position - _predictedPosition;
                _lastAuthoritativePosition = position;
            }

            ApplyPosition(_predictedPosition);
            gameObject.SetActive(true);
        }

        public void TickPrediction(float deltaTime)
        {
            if (!_hasSnapshot) return;

            _predictedPosition += _velocity * deltaTime;
            if (_correctionRemaining.LengthSquared > 0.000001f)
            {
                var correctionStep = Mathf.Min(1f, deltaTime / ReconciliationDuration);
                var appliedCorrection = _correctionRemaining * correctionStep;
                _predictedPosition += appliedCorrection;
                _correctionRemaining -= appliedCorrection;
            }

            ApplyPosition(_predictedPosition);
        }

        public void Release()
        {
            _hasSnapshot = false;
            _lastAuthoritativePosition = SimulationVector2.Zero;
            _predictedPosition = SimulationVector2.Zero;
            _velocity = SimulationVector2.Zero;
            _correctionRemaining = SimulationVector2.Zero;
            if (this == null) return;
            gameObject.SetActive(false);
        }

        private void ApplyPosition(SimulationVector2 position)
        {
            transform.position = new Vector3(position.X, position.Y, transform.position.z);
        }
    }
}
