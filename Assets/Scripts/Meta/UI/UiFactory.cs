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

        /// <summary>
        /// A rounded, bevelled panel body. The colour still tints it exactly as a flat
        /// Image did, so every screen that sets <c>Image.color</c> to mean something keeps
        /// working — the sprite adds shape and a lit-from-above bevel, not colour.
        /// </summary>
        /// <param name="rounded">
        /// False for thin progress fills. A 9-sliced rounded sprite on a bar a few pixels
        /// wide spends its whole width on corner radius and the fill stops reading as a
        /// quantity, which for a boss health bar is the one thing it has to do.
        /// </param>
        public static RectTransform Panel(Transform parent, string name, Color color,
            bool rounded = true)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            if (rounded)
            {
                image.sprite = UiTextures.Fill;
                image.type = Image.Type.Sliced;
            }
            image.color = color;
            return (RectTransform)go.transform;
        }

        /// <summary>
        /// The bronze border, as a separate child drawn over a panel. Separate because the
        /// frame is always bronze whatever the fill beneath it is saying, and one tinted
        /// image cannot be two colours.
        /// </summary>
        public static Image AddFrame(RectTransform panel)
        {
            var go = new GameObject("Frame", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(panel, false);
            var image = go.GetComponent<Image>();
            image.sprite = UiTextures.Frame;
            image.type = Image.Type.Sliced;
            image.raycastTarget = false;   // the panel underneath takes the taps
            Stretch((RectTransform)go.transform);
            return image;
        }

        /// <summary>
        /// The screen backdrop. A vertical gradient rather than a flat wash: a single
        /// unbroken colour behind everything is most of what reads as "unfinished app".
        /// </summary>
        /// <param name="gradient">
        /// False for a scrim — an overlay dimming the live game behind it, like the
        /// resurrect prompt. There the caller's colour and alpha ARE the design, and
        /// replacing them with a warm opaque gradient would hide the very thing the
        /// player is being asked to decide about.
        /// </param>
        public static RectTransform FullscreenPanel(Transform parent, string name, Color color,
            bool gradient = true)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            if (gradient)
            {
                image.sprite = UiTextures.Backdrop;
                image.type = Image.Type.Simple;
                // White, not the caller's colour: the gradient carries its own palette, and
                // tinting it with the old flat ink would flatten it straight back out.
                image.color = new Color(1f, 1f, 1f, Mathf.Max(color.a, 0.94f));
            }
            else
            {
                image.color = color;
            }

            var rt = (RectTransform)go.transform;
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

        /// <summary>
        /// A framed, bevelled button. GetComponent&lt;Image&gt;() still returns the FILL, and
        /// GetComponentInChildren&lt;Text&gt;() still returns the label, so callers that repaint
        /// a button to show state are untouched.
        ///
        /// Child order is the draw order: fill (on the button itself), then frame, then
        /// text. The frame must sit over the fill and under the label.
        /// </summary>
        public static Button ActionButton(Transform parent, string name, string label, Color tint,
            UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var fill = go.GetComponent<Image>();
            fill.sprite = UiTextures.Fill;
            fill.type = Image.Type.Sliced;
            fill.color = tint;

            var button = go.GetComponent<Button>();
            button.targetGraphic = fill;
            button.onClick.AddListener(onClick);

            // Explicit states. The default ColorBlock fades a disabled button to 50% alpha,
            // which on a dark background is indistinguishable from an enabled one.
            button.colors = new ColorBlock
            {
                normalColor = Color.white,
                highlightedColor = new Color(1.12f, 1.12f, 1.12f),
                pressedColor = new Color(0.72f, 0.72f, 0.76f),
                selectedColor = Color.white,
                disabledColor = new Color(0.42f, 0.42f, 0.46f, 1f),
                colorMultiplier = 1f,
                fadeDuration = 0.08f
            };

            AddFrame((RectTransform)go.transform);

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
