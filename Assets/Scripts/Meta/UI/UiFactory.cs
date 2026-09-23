using System.Collections.Generic;
using BattleRunner.Core.Text;
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
        /// <summary>
        /// Raised by every ActionButton. A static event rather than a service reference
        /// because UiFactory is a static widget vocabulary with no context and no lifetime —
        /// GameBootstrap points this at the audio service once and nothing else knows.
        /// </summary>
        public static event System.Action Tap;

        /// <summary>
        /// Every label built with a key, so the language can change without rebuilding the UI.
        ///
        /// A STATIC LIST, WHICH IS SAFE HERE FOR A SPECIFIC REASON. The eight screens are built
        /// once in GameBootstrap.CreateUi and toggled with SetActive for the rest of the process's
        /// life — nothing is ever destroyed, so these references cannot go stale. In a project
        /// that tore screens down this would be a leak; here it is the cheapest correct answer,
        /// and it follows the Tap event above, added for the same reason: so a screen written
        /// later works without anybody remembering to make it.
        /// </summary>
        private static readonly List<(Text Label, LocKey Key)> Keyed =
            new List<(Text, LocKey)>(128);

        /// <summary>
        /// Rewrite every keyed label in the current language. Called when the language changes.
        ///
        /// It does not reach labels whose text is set at runtime — the HUD's round marker, the
        /// loot card, the talent cells. Those are rewritten by their own screen the next time it
        /// is shown or refreshed, and the only screen visible when the language button is pressed
        /// is the menu, which refreshes itself on the same tap.
        /// </summary>
        public static void ReapplyText()
        {
            for (int i = 0; i < Keyed.Count; i++)
            {
                (Text label, LocKey key) = Keyed[i];
                if (label != null) label.text = Shape(Loc.Get(key));
            }
        }

        /// <summary>
        /// A string, in the order it should be DRAWN.
        ///
        /// THE LAST MOMENT BEFORE THE GLYPHS. Legacy Text performs no bidirectional reordering
        /// at all, so Hebrew handed to it straight from the table renders backwards. Everything
        /// that assigns .text goes through here; it is a no-op in English and Russian.
        /// </summary>
        public static string Shape(string text) =>
            Loc.IsRightToLeft ? BiDi.VisualLines(text) : text;

        /// <summary>Set a label's text, reordering it for the language first.</summary>
        public static void SetText(Text label, string text)
        {
            if (label != null) label.text = Shape(text);
        }

        /// <summary>
        /// Set the text of a label that WRAPS, breaking the lines here rather than letting the
        /// component do it.
        ///
        /// `SetText` is wrong for a wrapping label and wrong in a way nobody who cannot read
        /// Hebrew would ever notice. It reorders the whole string into one visual line; the
        /// component, set to <see cref="HorizontalWrapMode.Wrap"/>, then breaks that line
        /// wherever it stops fitting -- a position chosen in visual order, which corresponds to
        /// nothing in the sentence. The result is fluent-looking Hebrew with its clauses dealt
        /// out across the lines in the wrong order.
        ///
        /// So the breaking happens first, in logical order, and each finished line is reordered
        /// on its own. English and Russian are not touched at all: <see cref="BiDi.WrapVisual"/>
        /// returns them unchanged and the component keeps doing its own wrapping exactly as
        /// before.
        /// </summary>
        /// <param name="maxChars">
        /// How many characters fit on a line, estimated by the caller -- Core cannot measure a
        /// font. Estimate LOW: short lines are left alone by the component, over-long ones are
        /// re-broken, which is the failure this exists to prevent.
        /// </param>
        public static void SetTextWrapped(Text label, string text, int maxChars)
        {
            if (label == null) return;
            label.text = Loc.IsRightToLeft ? BiDi.WrapVisual(text, maxChars) : text;
        }

        /// <summary>
        /// Roughly how many characters of <paramref name="fontSize"/> fit across
        /// <paramref name="widthPx"/>, for <see cref="SetTextWrapped"/>.
        ///
        /// 0.62 em per character is deliberately wider than Arimo's Hebrew actually averages
        /// (nearer 0.55), which makes the answer too SMALL -- the safe direction, per the
        /// parameter note above. It also leaves room for best-fit, which only ever shrinks the
        /// font and so only ever fits more than this predicts.
        ///
        /// NOT VERIFIED ON A DEVICE. It is arithmetic over the reference resolution, and the
        /// only real arbiter is a phone.
        /// </summary>
        public static int CharsPerLine(float widthPx, int fontSize) =>
            Mathf.Max(1, Mathf.FloorToInt(widthPx / (fontSize * 0.62f)));

        /// <summary>
        /// A normalized x, mirrored when the language reads right to left.
        ///
        /// For the dozen places that are genuinely directional — the talent tree's tab row and
        /// two-column grid, ERASE against PLAY, the hero chips. The road and the bars are NOT
        /// put through this: a lane is a physical position and a draining bar is a quantity,
        /// and a bar that empties the other way reads as filling.
        /// </summary>
        public static float Mirror(float x) => Loc.IsRightToLeft ? 1f - x : x;

        /// <summary>
        /// A normalized SPAN, mirrored. Both edges move AND swap places, so the thing keeps its
        /// width and its gutter. Mirroring the two edges independently and leaving them in the
        /// old order gives a rect with negative width, which draws as nothing at all.
        /// </summary>
        public static void MirrorSpan(ref float lo, ref float hi)
        {
            if (!Loc.IsRightToLeft) return;
            float low = 1f - hi, high = 1f - lo;
            lo = low;
            hi = high;
        }

        /// <summary>An alignment, flipped when the language reads right to left.</summary>
        public static TextAnchor Flip(TextAnchor anchor)
        {
            if (!Loc.IsRightToLeft) return anchor;
            switch (anchor)
            {
                case TextAnchor.UpperLeft: return TextAnchor.UpperRight;
                case TextAnchor.UpperRight: return TextAnchor.UpperLeft;
                case TextAnchor.MiddleLeft: return TextAnchor.MiddleRight;
                case TextAnchor.MiddleRight: return TextAnchor.MiddleLeft;
                case TextAnchor.LowerLeft: return TextAnchor.LowerRight;
                case TextAnchor.LowerRight: return TextAnchor.LowerLeft;
                default: return anchor;
            }
        }

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

        /// <summary>
        /// The one font in the build, and the one line that decides whether this game can be
        /// read in Russian or Hebrew at all.
        ///
        /// It used to be Unity's built-in LegacyRuntime.ttf. That font carries Latin and nothing
        /// this project can rely on beyond it: Unity configures no fallback chain, so a missing
        /// codepoint renders as an empty box on device and the build machine cannot tell. This
        /// project has already paid that twice and left the scars in comments — HudScreen's pips
        /// were deleted and TutorialDirector's arrows became ASCII ^ and v, both because a glyph
        /// could not be counted on.
        ///
        /// Arimo is metric-compatible with Arial, which is what LegacyRuntime already is, so the
        /// English screens — every one of them laid out by eye against fixed pixel widths — move
        /// as little as it is possible to move them while changing typeface at all. It carries
        /// Latin, Cyrillic and Hebrew in one 316 KB file, which is why there is still exactly one
        /// Font here and one atlas rather than a fallback chain. tooling/fetch_font.py proves the
        /// coverage by reading the font's own cmap rather than asserting it.
        ///
        /// THE FALLBACK IS DELIBERATE AND IS NOT A SAFETY NET. If the asset is missing the game
        /// still runs, in English, looking exactly as it did before — which is the failure mode
        /// worth having, because the alternative is a blank UI. It is not a licence to skip the
        /// asset: Russian and Hebrew would both render as boxes.
        /// </summary>
        public static Font Font
        {
            get
            {
                if (_font == null) _font = Resources.Load<Font>("Fonts/Arimo-Regular");
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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

        /// <param name="key">
        /// When given, the label is remembered and rewritten by <see cref="ReapplyText"/> on a
        /// language change. Leave it out for a label whose text is set at runtime — its own
        /// screen owns that.
        /// </param>
        public static Text Label(Transform parent, string name, string content, int size, Color color,
            TextAnchor anchor = TextAnchor.MiddleCenter, LocKey? key = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Outline));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = Font;
            text.text = Shape(content);
            text.fontSize = size;
            text.color = color;
            text.alignment = Flip(anchor);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            // SHRINK RATHER THAN SPILL, and this is what makes a translated build survive at all.
            // Overflow means long text does not wrap and does not shrink — it runs straight out
            // past the bevelled frame of whatever button it is on. That was fine while every
            // string was English and hand-fitted to a fixed pixel width; Russian runs 10-30%
            // longer than English and would have walked out of every button in the game.
            //
            // Widening the buttons instead is the obvious fix and the wrong one: SlotSelectScreen
            // records that exact bug biting once already, when a 640 px button and a 210 px button
            // overlapped by 53 units on a tall phone and one of them silently took the other's
            // taps. Place() mixes a normalised centre with a pixel width, so growing anything
            // grows it into its neighbour.
            //
            // The max is the size the caller asked for, so ENGLISH IS UNCHANGED: it already fits,
            // so best-fit never has anything to do. Only a longer translation shrinks, and only as
            // far as it must. The floor is two thirds, below which a label stops being readable at
            // arm's length and the honest answer is shorter copy — which is what the per-key
            // length budgets in the Core tests are for.
            text.resizeTextForBestFit = true;
            text.resizeTextMaxSize = size;
            text.resizeTextMinSize = Mathf.Max(10, Mathf.RoundToInt(size * 0.67f));

            go.GetComponent<Outline>().effectColor = Shadow;
            if (key.HasValue) Keyed.Add((text, key.Value));
            return text;
        }

        /// <summary>
        /// A framed, bevelled button. GetComponent&lt;Image>() still returns the FILL, and
        /// GetComponentInChildren&lt;Text>() still returns the label, so callers that repaint
        /// a button to show state are untouched.
        ///
        /// Child order is the draw order: fill (on the button itself), then frame, then
        /// text. The frame must sit over the fill and under the label.
        /// </summary>
        /// <param name="labelSize">
        /// The label's font size, and therefore the ceiling best-fit will not grow past.
        ///
        /// A PARAMETER RATHER THAN AN ASSIGNMENT AFTERWARDS, which it used to be at ten call
        /// sites. Label() now sets resizeTextMaxSize from the size it is given, so a caller that
        /// built a 40 pt label and then quietly set fontSize = 26 would leave the ceiling at 40 —
        /// and best-fit, finding room, would grow it straight back. The English UI would have
        /// changed size on screens nobody touched.
        /// </param>
        public static Button ActionButton(Transform parent, string name, string label, Color tint,
            UnityEngine.Events.UnityAction onClick, int labelSize = 40, LocKey? key = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var fill = go.GetComponent<Image>();
            fill.sprite = UiTextures.Fill;
            fill.type = Image.Type.Sliced;
            fill.color = tint;

            var button = go.GetComponent<Button>();
            button.targetGraphic = fill;
            // The tap goes on EVERY button, here, rather than at forty call sites. UiFactory
            // is already the single place the widget vocabulary lives, so a screen added
            // later is audible without anybody remembering to make it so.
            button.onClick.AddListener(() => Tap?.Invoke());
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

            Text text = Label(go.transform, "Label", label, labelSize, Color.white,
                TextAnchor.MiddleCenter, key);
            Stretch((RectTransform)text.transform);
            // Inset off the bevelled frame. Best-fit fits text to its RECT, and the label's rect
            // is the whole button — so without this a shrunk Russian label sits hard against the
            // frame it was shrunk to stay inside.
            var rect = (RectTransform)text.transform;
            rect.offsetMin = new Vector2(LabelInset, 0f);
            rect.offsetMax = new Vector2(-LabelInset, 0f);
            return button;
        }

        /// <summary>Pixels of breathing room between a button's label and its frame.</summary>
        private const float LabelInset = 14f;

        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// Anchor a rect to a normalized RECTANGLE of its parent, so it scales with the
        /// canvas instead of holding a fixed pixel size.
        ///
        /// Place() mixes a normalized centre with a pixel size, which is right for a button
        /// and wrong for a region: a 1150-px-tall scroll viewport pinned that way overruns
        /// the tabs above it the moment the CanvasScaler shrinks the canvas on a tall phone.
        /// </summary>
        public static void PlaceRegion(RectTransform rt, float xMin, float yMin, float xMax, float yMax)
        {
            rt.anchorMin = new Vector2(xMin, yMin);
            rt.anchorMax = new Vector2(xMax, yMax);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// A cell in a top-down scrolling column: normalized across, pixels down from the
        /// content's top edge.
        ///
        /// Both anchors sit on the parent's top edge, so offsetMax.y and offsetMin.y are
        /// distances DOWN from it and the cell keeps its height however tall the content
        /// grows. Anchoring across rather than sizing in pixels is what lets a two-column
        /// row keep its gutter on every aspect ratio.
        /// </summary>
        public static void PlaceCell(RectTransform rt, float xMin, float xMax,
            float topOffset, float height)
        {
            rt.anchorMin = new Vector2(xMin, 1f);
            rt.anchorMax = new Vector2(xMax, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(0f, -(topOffset + height));
            rt.offsetMax = new Vector2(0f, -topOffset);
        }

        /// <summary>
        /// A vertical scroll region, returning the content rect callers fill with PlaceCell.
        ///
        /// Built by hand rather than from a prefab for the same reason as the rest of this
        /// file, and masked with RectMask2D rather than Mask: Mask needs a stencil material
        /// per masked graphic, and a tree of sixty buttons would break batching for all of
        /// them. Content is pivoted to its TOP so growing it downward never shifts what is
        /// already laid out.
        /// </summary>
        public static ScrollRect ScrollColumn(Transform parent, string name, out RectTransform content)
        {
            var rootGo = new GameObject(name, typeof(RectTransform), typeof(ScrollRect));
            rootGo.transform.SetParent(parent, false);
            var root = (RectTransform)rootGo.transform;

            // The viewport carries a fully transparent Image purely as a RAYCAST TARGET.
            // Without a graphic the ScrollRect only receives drags that start on a child
            // button, so a swipe begun in the gap between two cells does nothing — which on
            // a tree of sixty nodes is most of the screen and reads as a stuck list.
            var viewportGo = new GameObject("Viewport",
                typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            viewportGo.transform.SetParent(root, false);
            var catcher = viewportGo.GetComponent<Image>();
            catcher.color = new Color(0f, 0f, 0f, 0f);
            catcher.raycastTarget = true;
            var viewport = (RectTransform)viewportGo.transform;
            Stretch(viewport);

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(viewport, false);
            content = (RectTransform)contentGo.transform;
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = new Vector2(0f, 0f);
            content.offsetMax = new Vector2(0f, 0f);

            var scroll = rootGo.GetComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.elasticity = 0.1f;
            scroll.inertia = true;
            scroll.decelerationRate = 0.135f;
            scroll.scrollSensitivity = 30f;
            return scroll;
        }

        /// <summary>Set a scroll column's content height in reference pixels.</summary>
        public static void SetContentHeight(RectTransform content, float height) =>
            content.sizeDelta = new Vector2(content.sizeDelta.x, height);

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
