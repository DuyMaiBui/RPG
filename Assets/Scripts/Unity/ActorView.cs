using System;
using LitMotion;
using LitMotion.Extensions;
using RPG.Core.Actors;
using RPG.Simulation.Contracts;
using SimulationEntityId = RPG.Simulation.Contracts.EntityId;
using UnityEngine;

namespace RPG.Unity
{
    public sealed class ActorView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _renderer;
        [SerializeField] private HealthBarView _healthBar;
        private HealthBarPresenter _healthPresenter;
        private FloatingCombatTextPool _floatingTextPool;
        private MotionHandle _attackMotion;
        private MotionHandle _hurtMotion;
        private Vector3 _baseScale;
        private bool _released;

        public SimulationEntityId EntityId { get; private set; }
        public FactionId Faction { get; private set; }

        public void Initialize(SimulationEntityId entityId, FactionId faction, FloatingCombatTextPool floatingTextPool)
        {
            EntityId = entityId;
            Faction = faction;
            _floatingTextPool = floatingTextPool;
            if (_renderer == null || _healthBar == null)
                throw new InvalidOperationException("ActorView prefab references are incomplete.");
            _renderer.color = faction == FactionId.Red ? new Color(0.85f, 0.18f, 0.2f) : new Color(0.18f, 0.35f, 0.9f);
            _baseScale = transform.localScale;
            _healthBar.Initialize(_renderer.color);
            _healthPresenter = new HealthBarPresenter(_healthBar);
        }

        public void ApplySnapshot(ActorSnapshot snapshot) => _healthPresenter.Apply(snapshot);

        public void PlaySignal(PresentationSignal signal)
        {
            switch (signal.Kind)
            {
                case PresentationSignalKind.AttackStarted:
                    PlayAttack();
                    break;
                case PresentationSignalKind.Damaged:
                    PlayHurt(signal.Value);
                    break;
                case PresentationSignalKind.Died:
                    _healthBar.CanvasGroup.alpha = 0f;
                    break;
            }
        }

        public void Release()
        {
            if (_released) return;
            _released = true;
            if (_attackMotion.IsActive()) _attackMotion.Cancel();
            if (_hurtMotion.IsActive()) _hurtMotion.Cancel();
            _healthPresenter?.Release();
            if (this != null) Destroy(gameObject);
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
                .Bind(value => _renderer.color = Color.Lerp(Faction == FactionId.Red ? new Color(0.85f, 0.18f, 0.2f) : new Color(0.18f, 0.35f, 0.9f), Color.white, value));
            if (_floatingTextPool != null)
                _floatingTextPool.Rent(transform.position + Vector3.up * 0.45f, damage, Color.white);
        }
    }
}
