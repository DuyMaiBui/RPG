using System;
using RPG.Core.Actors;
using RPG.Simulation.Contracts;
using UnityEngine;

namespace RPG.Unity
{
    public sealed class ActorTargetView : ActorViewComponent
    {
        [SerializeField] private LineRenderer _line;

        public override void Initialize(in ActorViewContext context)
        {
            if (_line == null) throw new InvalidOperationException("ActorTargetView requires a LineRenderer reference.");
            _line.useWorldSpace = true;
            _line.positionCount = 2;
            _line.enabled = false;
        }

        public override void ApplySnapshot(ActorSnapshot snapshot)
        {
        }

        public void ApplyTargetPosition(Vector3 targetPosition, bool visible)
        {
            if (!visible)
            {
                _line.enabled = false;
                return;
            }

            _line.SetPosition(0, transform.position);
            _line.SetPosition(1, targetPosition);
            _line.enabled = true;
        }

        public override void PlaySignal(PresentationSignal signal)
        {
        }

        public override void Release()
        {
            if (_line != null) _line.enabled = false;
        }
    }
}
