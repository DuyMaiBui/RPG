using System;
using LitMotion;
using LitMotion.Extensions;
using RPG.Core.Actors;
using UnityEngine;

namespace RPG.Unity
{
    public sealed class ActorCombatFeedbackView : ActorViewComponent
    {
        [SerializeField] private SpriteRenderer _renderer;
        private FloatingCombatTextPool _floatingTextPool;
        private MotionHandle _attackMotion;
        private MotionHandle _hurtMotion;
        private Vector3 _baseScale;
        private Color _factionColor;

        public override void Initialize(in ActorViewContext context)
        {
            if (_renderer == null)
                throw new InvalidOperationException("ActorCombatFeedbackView prefab references are incomplete.");

            _floatingTextPool = context.FloatingTextPool;
            _factionColor = context.FactionColor;
            _baseScale = transform.localScale;
        }

        public override void ApplySnapshot(ActorSnapshot snapshot)
        {
        }

        public override void PlaySignal(PresentationSignal signal)
        {
            switch (signal.Kind)
            {
                case PresentationSignalKind.AttackStarted:
                    PlayAttack();
                    break;
                case PresentationSignalKind.Damaged:
                    PlayHurt(signal.Value);
                    break;
            }
        }

        public override void Release()
        {
            if (_attackMotion.IsActive()) _attackMotion.Cancel();
            if (_hurtMotion.IsActive()) _hurtMotion.Cancel();
            _floatingTextPool = null;
        }

        private void PlayAttack()
        {
            if (_attackMotion.IsActive()) _attackMotion.Cancel();
            _attackMotion = LMotion.Create(_baseScale, _baseScale * 1.18f, 0.12f)
                .WithLoops(2, LoopType.Yoyo)
                .WithEase(Ease.InOutQuad)
                .BindToLocalScale(transform);
        }

        private void PlayHurt(int damage)
        {
            if (_hurtMotion.IsActive()) _hurtMotion.Cancel();
            _hurtMotion = LMotion.Create(0f, 1f, 0.18f)
                .WithLoops(2, LoopType.Yoyo)
                .WithEase(Ease.InOutQuad)
                .Bind(value => _renderer.color = Color.Lerp(_factionColor, Color.white, value));
            if (_floatingTextPool != null)
                _floatingTextPool.Rent(transform.position + Vector3.up * 0.45f, damage, Color.white);
        }
    }
}
