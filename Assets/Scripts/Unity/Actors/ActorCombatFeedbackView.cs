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
        private Color _baseColor;

        public override void Initialize(in ActorViewContext context)
        {
            if (_renderer == null)
                throw new InvalidOperationException("ActorCombatFeedbackView prefab references are incomplete.");

            _floatingTextPool = context.FloatingTextPool;
            _factionColor = context.FactionColor;
            _baseColor = _factionColor;
            _baseScale = transform.localScale;
        }

        public override void ApplySnapshot(ActorSnapshot snapshot)
        {
            _baseColor = StatusColor(snapshot.StatusEffectMask);
            if (!_hurtMotion.IsActive())
                _renderer.color = _baseColor;
        }

        private Color StatusColor(byte mask)
        {
            const byte poison = 1 << (int)StatusEffectType.Poison;
            const byte slow = 1 << (int)StatusEffectType.Slow;
            if ((mask & poison) != 0)
                return Color.Lerp(_factionColor, new Color(0.35f, 0.85f, 0.35f), 0.6f);
            if ((mask & slow) != 0)
                return Color.Lerp(_factionColor, new Color(0.4f, 0.6f, 1f), 0.6f);
            return _factionColor;
        }

        public override void PlaySignal(PresentationSignal signal)
        {
            switch (signal.Kind)
            {
                case PresentationSignalKind.AttackStarted:
                    PlayAttack();
                    break;
                case PresentationSignalKind.AbilityCast:
                    PlayAbility();
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

        private void PlayAbility()
        {
            if (_attackMotion.IsActive()) _attackMotion.Cancel();
            _attackMotion = LMotion.Create(_baseScale, _baseScale * 1.3f, 0.16f)
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
                .Bind(value => _renderer.color = Color.Lerp(_baseColor, Color.white, value));
            if (_floatingTextPool != null)
                _floatingTextPool.Rent(transform.position + Vector3.up * 0.45f, damage, Color.white);
        }
    }
}
