using LitMotion;
using LitMotion.Extensions;
using RPG.Core.Actors;
using UnityEngine;

namespace RPG.Unity
{
    public sealed class HealthBarPresenter
    {
        private readonly HealthBarView _view;
        private MotionHandle _currentMotion;
        private MotionHandle _delayedMotion;
        private float _displayedCurrent;
        private float _displayedDelayed;

        public HealthBarPresenter(HealthBarView view)
        {
            _view = view;
            _displayedCurrent = 1f;
            _displayedDelayed = 1f;
        }

        public void Apply(ActorSnapshot snapshot)
        {
            var target = snapshot.MaximumHealth <= 0 ? 0f : (float)snapshot.CurrentHealth / snapshot.MaximumHealth;
            if (_currentMotion.IsActive()) _currentMotion.Cancel();
            if (_delayedMotion.IsActive()) _delayedMotion.Cancel();
            _currentMotion = LMotion.Create(_displayedCurrent, target, 0.18f)
                .WithEase(Ease.OutCubic)
                .Bind(value =>
                {
                    _displayedCurrent = value;
                    _view.SetFill(_view.CurrentFill, value);
                });
            _delayedMotion = LMotion.Create(_displayedDelayed, target, 0.55f)
                .WithDelay(0.12f)
                .WithEase(Ease.OutCubic)
                .Bind(value =>
                {
                    _displayedDelayed = value;
                    _view.SetFill(_view.DelayedFill, value);
                });
            _view.Label.text = $"{snapshot.CurrentHealth}/{snapshot.MaximumHealth}";
            if (snapshot.VisualState == ActorVisualState.Dead)
                _view.CanvasGroup.alpha = 0f;
        }

        public void Release()
        {
            if (_currentMotion.IsActive()) _currentMotion.Cancel();
            if (_delayedMotion.IsActive()) _delayedMotion.Cancel();
        }
    }
}
