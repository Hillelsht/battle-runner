using System;
using System.Collections.Generic;
using BattleRunner.Core.Heroes;
using UnityEngine;
using UnityEngine.UI;

namespace BattleRunner.Meta.UI
{
    /// <summary>
    /// Who you are going to be. Four cards, shown once per save before the first round.
    ///
    /// TWO TAPS, NOT ONE. The first selects and the second confirms, because the choice is
    /// permanent for the slot and the thing that distinguishes these four — the RULE — takes
    /// a sentence to read. A one-tap grid would have players committing to a name and a
    /// colour before they had seen what the name meant.
    ///
    /// Each card is painted in its hero's army colour rather than in the menu's ink, so the
    /// screen teaches the palette the run will be played in: the blue army, the ember army,
    /// the green one, the violet one. That is the same colour the crowd is about to be.
    /// </summary>
    public sealed class HeroSelectScreen
    {
        private sealed class Card
        {
            public Button Button;
            public Image Fill;
            public Text Label;
            public Color Rest;
        }

        private readonly GameObject _root;
        private readonly List<Card> _cards = new List<Card>();
        private readonly Text _detail;
        private readonly Button _confirm;

        private Action<int> _onChoose;
        private int _selected;

        public HeroSelectScreen(Transform canvas)
        {
            RectTransform root = UiFactory.FullscreenPanel(canvas, "HeroSelect", UiFactory.Ink);
            _root = root.gameObject;

            Text title = UiFactory.Label(root, "Title", "CHOOSE YOUR CHAMPION", 62, UiFactory.Gold);
            UiFactory.Place((RectTransform)title.transform, 0.5f, 0.92f, 1000f, 100f);

            Text subtitle = UiFactory.Label(root, "Subtitle",
                "this choice is for the life of this save", 28, UiFactory.Parchment);
            UiFactory.Place((RectTransform)subtitle.transform, 0.5f, 0.865f, 900f, 50f);

            HeroProfile[] roster = HeroRoster.All();
            for (int i = 0; i < roster.Length; i++)
            {
                int index = i;
                HeroProfile hero = roster[i];
                // Held well down from the army colour: a card at full army brightness is a
                // slab of saturated colour with white text over it, and the tagline is the
                // part that has to be legible.
                var rest = new Color(hero.Army.R * 0.42f, hero.Army.G * 0.42f, hero.Army.B * 0.42f, 0.95f);

                Button card = UiFactory.ActionButton(root, $"Hero{i}", string.Empty, rest,
                    () => Select(index));
                UiFactory.Place((RectTransform)card.transform, 0.5f, CardY(i), 940f, 150f);

                Text label = card.GetComponentInChildren<Text>();
                label.fontSize = 30;
                label.text = $"{hero.Name}\n{hero.Tagline}";

                _cards.Add(new Card { Button = card, Fill = card.GetComponent<Image>(),
                    Label = label, Rest = rest });
            }

            _detail = UiFactory.Label(root, "Detail", string.Empty, 28, UiFactory.Parchment);
            UiFactory.Place((RectTransform)_detail.transform, 0.5f, 0.235f, 940f, 90f);

            _confirm = UiFactory.ActionButton(root, "Confirm", "BEGIN", UiFactory.InkSoft,
                () => _onChoose?.Invoke(_selected));
            UiFactory.Place((RectTransform)_confirm.transform, 0.5f, 0.125f, 620f, 140f);

            Hide();
        }

        private static float CardY(int row) => 0.76f - row * 0.125f;

        public void Show(Action<int> onChoose, int initial)
        {
            _onChoose = onChoose;
            _root.SetActive(true);
            Select(initial >= 0 && initial < _cards.Count ? initial : (int)HeroRoster.Default);
        }

        public void Hide() => _root.SetActive(false);

        private void Select(int index)
        {
            _selected = index;
            for (int i = 0; i < _cards.Count; i++)
            {
                Card card = _cards[i];
                bool on = i == index;
                // Brightened AND outlined by the label colour, because a colour-only
                // selection cue on four differently-coloured cards is not a cue at all —
                // the selected green card would just look like the green card.
                card.Fill.color = on
                    ? new Color(card.Rest.r * 2.1f + 0.10f, card.Rest.g * 2.1f + 0.10f,
                        card.Rest.b * 2.1f + 0.10f, 1f)
                    : card.Rest;
                card.Label.color = on ? Color.white : new Color(0.78f, 0.76f, 0.72f);
            }
            _detail.text = HeroRoster.StatLine((HeroClass)index);
        }
    }
}
