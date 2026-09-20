using LitMotion;
using LitMotion.Extensions;
using UnityEngine;
using UnityEngine.UI;

namespace RPG.Unity
{
    public sealed class FloatingCombatTextView : MonoBehaviour
    {
        private Text _label;
        private CanvasGroup _canvasGroup;
        private MotionHandle _positionMotion;
        private MotionHandle _alphaMotion;
        private FloatingCombatTextPool _owner;

        public bool IsAvailable => !gameObject.activeSelf;

        public void Initialize(FloatingCombatTextPool owner)
        {
            _owner = owner;
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 20;
            gameObject.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            var labelObject = new GameObject("Label");
            labelObject.transform.SetParent(transform, false);
            _label = labelObject.AddComponent<Text>();
            _label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _label.fontSize = 32;
            _label.alignment = TextAnchor.MiddleCenter;
            _label.fontStyle = FontStyle.Bold;
            _label.color = Color.white;
            var rect = _label.rectTransform;
            rect.sizeDelta = new Vector2(160f, 60f);
            rect.localScale = Vector3.one * 0.01f;
            gameObject.SetActive(false);
        }

        public void Show(Vector3 worldPosition, int damage, Color color)
        {
            CancelMotions();
            gameObject.SetActive(true);
            transform.position = worldPosition;
            _label.text = $"-{damage}";
            _label.color = color;
            _canvasGroup.alpha = 1f;
            var endPosition = worldPosition + Vector3.up * 0.8f;
            _positionMotion = LMotion.Create(worldPosition, endPosition, 0.55f)
                .WithEase(Ease.OutCubic)
                .WithOnComplete(ReturnToPool)
                .BindToPosition(transform);
            _alphaMotion = LMotion.Create(1f, 0f, 0.55f)
                .WithEase(Ease.InQuad)
                .BindToAlpha(_canvasGroup);
        }

        public void Release()
        {
            CancelMotions();
            gameObject.SetActive(false);
        }

        private void ReturnToPool()
        {
            if (_owner != null)
                _owner.Return(this);
        }

        private void CancelMotions()
        {
            if (_positionMotion.IsActive()) _positionMotion.Cancel();
            if (_alphaMotion.IsActive()) _alphaMotion.Cancel();
        }
    }
}
