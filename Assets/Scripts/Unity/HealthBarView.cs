using System;
using UnityEngine;
using UnityEngine.UI;

namespace RPG.Unity
{
    public sealed class HealthBarView : MonoBehaviour
    {
        [SerializeField] private Image _currentFill;
        [SerializeField] private Image _delayedFill;
        [SerializeField] private Text _label;
        [SerializeField] private CanvasGroup _canvasGroup;

        public Image CurrentFill => _currentFill;
        public Image DelayedFill => _delayedFill;
        public Text Label => _label;
        public CanvasGroup CanvasGroup => _canvasGroup;

        public void Initialize(Color factionColor)
        {
            if (_currentFill == null || _delayedFill == null || _label == null || _canvasGroup == null)
                throw new InvalidOperationException("HealthBarView prefab references are incomplete.");

            _delayedFill.color = new Color(0.95f, 0.7f, 0.15f, 1f);
            _currentFill.color = factionColor;
            SetFill(_delayedFill, 1f);
            SetFill(_currentFill, 1f);
        }

        public void SetFill(Image image, float value) => image.fillAmount = Mathf.Clamp01(value);

    }
}
