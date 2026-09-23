using RPG.Core.Actors;
using RPG.Simulation.Contracts;
using UnityEngine;

namespace RPG.Unity
{
    public sealed class ActorMovementView : ActorViewComponent
    {
        private SimulationVector2 _fromPosition;
        private SimulationVector2 _toPosition;
        private float _interpolationTime;
        private bool _hasSnapshot;

        public override void Initialize(in ActorViewContext context)
        {
            _fromPosition = SimulationVector2.Zero;
            _toPosition = SimulationVector2.Zero;
            _interpolationTime = 1f;
            _hasSnapshot = false;
        }

        public override void ApplySnapshot(ActorSnapshot snapshot)
        {
            var position = snapshot.Position;
            if (!_hasSnapshot)
            {
                _fromPosition = position;
                _toPosition = position;
                _interpolationTime = 1f;
                _hasSnapshot = true;
                ApplyPosition(position);
                return;
            }

            _fromPosition = new SimulationVector2(transform.position.x, transform.position.y);
            _toPosition = position;
            _interpolationTime = 0f;
        }

        public void ApplyPredictedPosition(SimulationVector2 position)
        {
            _fromPosition = position;
            _toPosition = position;
            _interpolationTime = 1f;
            _hasSnapshot = true;
            ApplyPosition(position);
        }

        public void TickRemoteInterpolation(float deltaTime, float snapshotInterval)
        {
            if (!_hasSnapshot || _interpolationTime >= 1f)
                return;

            _interpolationTime = Mathf.Min(1f, _interpolationTime + deltaTime / Mathf.Max(0.0001f, snapshotInterval));
            ApplyPosition(new SimulationVector2(
                Mathf.Lerp(_fromPosition.X, _toPosition.X, _interpolationTime),
                Mathf.Lerp(_fromPosition.Y, _toPosition.Y, _interpolationTime)));
        }

        public override void PlaySignal(PresentationSignal signal)
        {
        }

        public override void Release()
        {
            _hasSnapshot = false;
        }

        private void ApplyPosition(SimulationVector2 position)
        {
            transform.position = new Vector3(position.X, position.Y, transform.position.z);
        }
    }
}
