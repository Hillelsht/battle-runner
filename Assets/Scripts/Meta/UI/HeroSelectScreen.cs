using System;
using System.Collections.Generic;
using BattleRunner.Core.Heroes;
using UnityEngine;
using UnityEngine.UI;

namespace BattleRunner.Meta.UI
{
    /// <summary>
    /// Who you are going to be: one hero at a time, large, standing on a lit stage.
    ///
    /// *"when I choose the main character this page has only cells with text, make it visuals,
    /// make all characters appear so I could see who do I choose with animation of them how they
    /// stay and greet and fight."*
    ///
    /// THE SCREEN IS A FRAME, NOT A PANEL, and that is the whole trick. The canvas is
    /// `ScreenSpaceOverlay`, so it composites over the finished frame and anything the UI leaves
    /// transparent shows what the camera rendered — which during the menus is an empty world and
    /// a sky, plus whatever `Gameplay.Menu.HeroStage` has put in it. So this builds its backdrop
    /// out of a top band and a bottom band with a window between them, and the hero stands in
    /// the window. `UiFactory.FullscreenPanel` cannot be used at all: it is opaque by
    /// construction, and an opaque backdrop is exactly the thing that would hide the feature.
    ///
    /// TWO TAPS, NOT ONE, and that is unchanged. The first selects and the second confirms,
    /// because the choice is permanent for the slot. What changed is what the first tap buys:
    /// it used to brighten a card, and now it brings a figure forward to greet you.
    ///
    /// Each name chip is painted in its hero's army colour, so the row teaches the palette the
    /// run will be played in — the same colour the crowd is about to be.
    /// </summary>
    public sealed class HeroSelectScreen
    {
        private sealed class Chip
        {
            public Image Fill;
            public Text Label;
            public Color Rest;
        }

        private readonly GameObject _root;
        private readonly List<Chip> _chips = new List<Chip>();
        private readonly Text _name;
        private readonly Text _tagline;
        private readonly Text _detail;

        private Action<int> _onChoose;
        private Action<int> _onPreview;
        private Action _onFight;
        private int _selected = -1;

        /// <summary>
        /// The window the stage shows through, as fractions of the screen height.
        ///
        /// The top band has to clear the title and the bottom band has to hold the chips, the
        /// stat line and the confirm button — so the window is what is left, and it is the
        /// larger part of the screen on purpose. A figure given a third of a portrait phone is
        /// a thumbnail with extra steps.
        /// </summary>
        private const float WindowBottom = 0.345f;
        private const float WindowTop = 0.845f;

        public HeroSelectScreen(Transform canvas)
        {
            var rootGo = new GameObject("HeroSelect", typeof(RectTransform));
            rootGo.transform.SetParent(canvas, false);
            var root = (RectTransform)rootGo.transform;
            UiFactory.PlaceRegion(root, 0f, 0f, 1f, 1f);
            _root = rootGo;

            // The two bands. Opaque, so the stage is framed rather than floating in a scrim,
            // and they are what the title and the controls sit on.
            RectTransform above = UiFactory.Panel(root, "Above", UiFactory.Ink, rounded: false);
            UiFactory.PlaceRegion(above, 0f, WindowTop, 1f, 1f);
            RectTransform below = UiFactory.Panel(root, "Below", UiFactory.Ink, rounded: false);
            UiFactory.PlaceRegion(below, 0f, 0f, 1f, WindowBottom);

            // A gradient either side of the window so the bands do not end in a hard line
            // across the hero's knees and forehead. Ink with zero alpha at the window edge.
            Fade(root, "FadeTop", WindowTop, WindowTop + 0.075f, from: 1f);
            Fade(root, "FadeBottom", WindowBottom - 0.075f, WindowBottom, from: 0f);

            Text title = UiFactory.Label(root, "Title", "CHOOSE YOUR CHAMPION", 58, UiFactory.Gold);
            UiFactory.Place((RectTransform)title.transform, 0.5f, 0.945f, 1000f, 90f);
            Text note = UiFactory.Label(root, "Note",
                "this choice is for the life of this save", 26, UiFactory.Parchment);
            UiFactory.Place((RectTransform)note.transform, 0.5f, 0.885f, 900f, 44f);

            // The hero's own name, large, under the window rather than over it — a caption on
            // the figure, which is what the window is for.
            _name = UiFactory.Label(root, "Name", string.Empty, 54, UiFactory.Gold);
            UiFactory.Place((RectTransform)_name.transform, 0.5f, 0.305f, 1000f, 76f);
            _tagline = UiFactory.Label(root, "Tagline", string.Empty, 27, UiFactory.Parchment);
            UiFactory.Place((RectTransform)_tagline.transform, 0.5f, 0.258f, 980f, 48f);
            _detail = UiFactory.Label(root, "Detail", string.Empty, 25, UiFactory.Parchment);
            UiFactory.Place((RectTransform)_detail.transform, 0.5f, 0.213f, 980f, 46f);

            // Four name chips in a row. A row rather than a stack, because the figure is what
            // is being compared now and the chips are only how you get to the next one.
            HeroProfile[] roster = HeroRoster.All();
            for (int i = 0; i < roster.Length; i++)
            {
                int index = i;
                HeroProfile hero = roster[i];
                var rest = new Color(hero.Army.R * 0.34f, hero.Army.G * 0.34f,
                    hero.Army.B * 0.34f, 0.95f);
                Button chip = UiFactory.ActionButton(root, $"Hero{i}", hero.Name, rest,
                    () => Select(index), labelSize: 23);
                UiFactory.Place((RectTransform)chip.transform,
                    0.145f + 0.237f * i, 0.145f, 250f, 92f);
                Text label = chip.GetComponentInChildren<Text>();
                _chips.Add(new Chip { Fill = chip.GetComponent<Image>(), Label = label, Rest = rest });
            }

            // SHOW ME is beside BEGIN rather than hidden behind a long-press, because *"and
            // fight"* is a thing the player asked to see and an unlabelled gesture is a thing
            // nobody finds. Tapping an already-selected chip replays the greeting; this replays
            // the attack, which is the one act that will not play on its own.
            Button fight = UiFactory.ActionButton(root, "Fight", "SHOW ME", UiFactory.InkSoft,
                () => _onFight?.Invoke());
            UiFactory.Place((RectTransform)fight.transform, 0.275f, 0.055f, 400f, 108f);

            Button confirm = UiFactory.ActionButton(root, "Confirm", "BEGIN", UiFactory.Gold,
                () => _onChoose?.Invoke(_selected));
            UiFactory.Place((RectTransform)confirm.transform, 0.705f, 0.055f, 400f, 108f);

            Hide();
        }

        /// <summary>
        /// A band that fades from ink to nothing, softening the edge of the window.
        ///
        /// Built from the backdrop gradient sprite, flipped for the lower one — the same asset
        /// the full-screen panels use, so there is no second texture for this.
        /// </summary>
        private static void Fade(RectTransform root, string name, float yMin, float yMax, float from)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(root, false);
            var image = go.GetComponent<Image>();
            image.sprite = UiTextures.Backdrop;
            image.color = new Color(UiFactory.Ink.r, UiFactory.Ink.g, UiFactory.Ink.b, 0.94f);
            image.raycastTarget = false;
            var rt = (RectTransform)go.transform;
            UiFactory.PlaceRegion(rt, 0f, yMin, 1f, yMax);
            // The gradient runs dark at one end; turning it upside down is how the lower band
            // fades the other way without a second sprite.
            rt.localRotation = Quaternion.Euler(0f, 0f, from > 0.5f ? 0f : 180f);
        }

        /// <summary>
        /// Show the screen.
        ///
        /// <paramref name="onPreview"/> fires on every selection, including the first, so the
        /// stage is never empty while the screen is up — the caller puts a hero on it.
        /// </summary>
        public void Show(Action<int> onChoose, Action<int> onPreview, Action onFight, int initial)
        {
            _onChoose = onChoose;
            _onPreview = onPreview;
            _onFight = onFight;
            _root.SetActive(true);
            _selected = -1;
            Select(initial >= 0 && initial < _chips.Count ? initial : (int)HeroRoster.Default);
        }

        public void Hide() => _root.SetActive(false);

        private void Select(int index)
        {
            // A REPEAT TAP IS NOT A NO-OP. It replays the greeting, which is the only way to
            // see that act again — and a chip that did nothing when tapped twice would read as
            // the screen having stopped responding.
            _selected = index;
            for (int i = 0; i < _chips.Count; i++)
            {
                Chip chip = _chips[i];
                bool on = i == index;
                // Brightened AND relabelled white, because a colour-only cue on four
                // differently-coloured chips is not a cue at all: the selected green chip
                // would just look like the green chip.
                chip.Fill.color = on
                    ? new Color(chip.Rest.r * 2.4f + 0.10f, chip.Rest.g * 2.4f + 0.10f,
                        chip.Rest.b * 2.4f + 0.10f, 1f)
                    : chip.Rest;
                chip.Label.color = on ? Color.white : new Color(0.74f, 0.72f, 0.68f);
            }

            HeroProfile profile = HeroRoster.For((HeroClass)index);
            _name.text = profile.Name;
            _tagline.text = profile.Tagline;
            _detail.text = HeroRoster.StatLine((HeroClass)index);
            _onPreview?.Invoke(index);
        }
    }
}
