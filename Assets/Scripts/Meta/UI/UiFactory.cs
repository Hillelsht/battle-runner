using UnityEngine;
using UnityEngine.UI;

namespace BattleRunner.Meta.UI
{
    /// <summary>
    /// Builds the greybox UI from code — no prefabs, no TMP (legacy Text avoids the
    /// TMP Essentials import; the built-in font is LegacyRuntime.ttf in Unity 6).
    /// Palette: dark environment, emissive accents (doc 01, R5).
    /// </summary>
    public static class UiFactory
    {
        public static readonly Color Ink = new Color(0.07f, 0.06f, 0.09f, 0.94f);
        public static readonly Color InkSoft = new Color(0.11f, 0.10f, 0.14f, 0.92f);
        public static readonly Color Parchment = new Color(0.85f, 0.81f, 0.72f);
        public static readonly Color Gold = new Color(0.91f, 0.64f, 0.24f);
        public static readonly Color Blood = new Color(0.75f, 0.16f, 0.16f);
        public static readonly Color Arcane = new Color(0.35f, 0.62f, 0.95f);
        public static readonly Color Shadow = new Color(0f, 0f, 0f, 0.6f);

        public static readonly Color[] RarityColors =
        {
            new Color(0.65f, 0.65f, 0.65f), // Common
            new Color(0.30f, 0.55f, 0.95f), // Rare
            new Color(0.65f, 0.35f, 0.90f), // Epic
            new Color(0.95f, 0.60f, 0.15f)  // Legendary
        };

        private static Font _font;

        public static Font Font
        {
            get
            {
                if (_font == null)
                    _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _font;
            }
        }

        public static Canvas CreateCanvas(string name)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        public static RectTransform Panel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return (RectTransform)go.transform;
        }

        public static RectTransform FullscreenPanel(Transform parent, string name, Color color)
        {
            RectTransform rt = Panel(parent, name, color);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static Text Label(Transform parent, string name, string content, int size, Color color,
            TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Outline));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = Font;
            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            go.GetComponent<Outline>().effectColor = Shadow;
            return text;
        }

        public static Button ActionButton(Transform parent, string name, string label, Color tint,
            UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = tint;
            var button = go.GetComponent<Button>();
            button.onClick.AddListener(onClick);

            Text text = Label(go.transform, "Label", label, 40, Color.white);
            Stretch((RectTransform)text.transform);
            return button;
        }

        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>Anchor a rect by normalized center + pixel size (reference resolution space).</summary>
        public static void Place(RectTransform rt, float anchorX, float anchorY, float width, float height)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(anchorX, anchorY);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = Vector2.zero;
        }
    }
}
