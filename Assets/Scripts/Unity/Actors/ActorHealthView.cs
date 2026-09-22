using System;
using RPG.Core.Actors;
using UnityEngine;

namespace RPG.Unity
{
    public sealed class ActorHealthView : ActorViewComponent
    {
        [SerializeField] private HealthBarView _healthBar;
        private HealthBarPresenter _healthPresenter;

        public override void Initialize(in ActorViewContext context)
        {
            if (_healthBar == null)
                throw new InvalidOperationException("ActorHealthView prefab references are incomplete.");

            _healthBar.Initialize(context.FactionColor);
            _healthPresenter = new HealthBarPresenter(_healthBar);
        }

        public override void ApplySnapshot(ActorSnapshot snapshot) => _healthPresenter.Apply(snapshot);

        public override void PlaySignal(PresentationSignal signal)
        {
            if (signal.Kind == PresentationSignalKind.Died)
                _healthBar.CanvasGroup.alpha = 0f;
        }

        public override void Release()
        {
            _healthPresenter?.Release();
            _healthPresenter = null;
        }
    }
}
