using System;
using RPG.Core.Actors;
using UnityEngine;

namespace RPG.Unity
{
    public sealed class ActorRangeView : ActorViewComponent
    {
        private const int PointCount = 32;

        [SerializeField] private LineRenderer _range;
        private Vector3[] _points;

        public override void Initialize(in ActorViewContext context)
        {
            if (_range == null)
                throw new InvalidOperationException("ActorRangeView prefab requires a LineRenderer reference.");

            _points = new Vector3[PointCount + 1];
            _range.useWorldSpace = false;
            _range.loop = false;
            _range.positionCount = _points.Length;
        }

        public override void ApplySnapshot(ActorSnapshot snapshot)
        {
            var radius = snapshot.Radius + snapshot.AttackRange;
            for (var index = 0; index <= PointCount; index++)
            {
                var angle = index * MathF.PI * 2f / PointCount;
                _points[index] = new Vector3(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius, 0f);
            }

            _range.SetPositions(_points);
            _range.enabled = snapshot.BehaviorState != AutoCombatState.Dead;
            var activeColor = snapshot.AttackType == AttackType.Projectile
                ? new Color(0.2f, 0.75f, 1f, 0.85f)
                : new Color(1f, 0.25f, 0.1f, 0.85f);
            var idleColor = snapshot.AttackType == AttackType.Projectile
                ? new Color(0.2f, 0.75f, 1f, 0.35f)
                : new Color(1f, 0.8f, 0.15f, 0.35f);
            _range.startColor = snapshot.BehaviorState == AutoCombatState.AttackTarget ? activeColor : idleColor;
            _range.endColor = _range.startColor;
        }

        public override void PlaySignal(PresentationSignal signal)
        {
        }

        public override void Release()
        {
            if (_range != null) _range.enabled = false;
            _points = null;
        }
    }
}
