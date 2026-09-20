using UnityEngine;
using UnityEngine.UI;

namespace RPG.Unity
{
    public sealed class HealthBarView : MonoBehaviour
    {
        public Image CurrentFill { get; private set; }
        public Image DelayedFill { get; private set; }
        public Text Label { get; private set; }
        public CanvasGroup CanvasGroup { get; private set; }

        public void Initialize(Color factionColor)
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 10;
            gameObject.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;
            CanvasGroup = gameObject.AddComponent<CanvasGroup>();
            var background = CreateImage("Background", new Color(0.08f, 0.08f, 0.08f, 0.9f));
            background.rectTransform.sizeDelta = new Vector2(160f, 18f);
            DelayedFill = CreateImage("DelayedFill", new Color(0.95f, 0.7f, 0.15f, 1f));
            CurrentFill = CreateImage("CurrentFill", factionColor);
            SetFill(DelayedFill, 1f);
            SetFill(CurrentFill, 1f);
            var labelObject = new GameObject("HealthLabel");
            labelObject.transform.SetParent(transform, false);
            Label = labelObject.AddComponent<Text>();
            Label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Label.fontSize = 22;
            Label.alignment = TextAnchor.MiddleCenter;
            Label.color = Color.white;
            Label.rectTransform.sizeDelta = new Vector2(160f, 32f);
            Label.rectTransform.localPosition = Vector3.down * 18f;
            transform.localScale = Vector3.one * 0.01f;
        }

        public void SetFill(Image image, float value) => image.fillAmount = Mathf.Clamp01(value);

        private Image CreateImage(string name, Color color)
        {
            var imageObject = new GameObject(name);
            imageObject.transform.SetParent(transform, false);
            var image = imageObject.AddComponent<Image>();
            image.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), Vector2.one * 0.5f);
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = 0;
            image.color = color;
            image.rectTransform.sizeDelta = new Vector2(160f, 18f);
            image.rectTransform.pivot = new Vector2(0f, 0.5f);
            image.rectTransform.localPosition = Vector3.zero;
            return image;
        }
    }
}
