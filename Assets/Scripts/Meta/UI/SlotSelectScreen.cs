using System;
using System.Collections.Generic;
using BattleRunner.Core.Save;
using UnityEngine;
using UnityEngine.UI;

namespace BattleRunner.Meta.UI
{
    /// <summary>
    /// Pick which game to play. Three slots, each either an invitation or a save.
    ///
    /// Erase is per-slot and takes two taps, because it cannot be undone: the first arms it
    /// and names what it will destroy, and the arm expires on its own so that backgrounding
    /// the app mid-decision cannot leave a one-tap wipe waiting on resume.
    /// </summary>
    public sealed class SlotSelectScreen
    {
        private sealed class SlotWidget
        {
            public Text Label;
            public Button Erase;
            public Text EraseLabel;
            public RectTransform PlayRect;
            public int Row;
            public bool Armed;
            public float ArmedFor;
        }

        private const float ArmedSeconds = 4f;
        private const string EraseIdle = "ERASE";
        private const string EraseArmed = "SURE?";

        private readonly GameObject _root;
        private readonly List<SlotWidget> _slots = new List<SlotWidget>();

        private Action<int> _onPlay;
        private Action<int> _onErase;

        public SlotSelectScreen(Transform canvas)
        {
            RectTransform root = UiFactory.FullscreenPanel(canvas, "SlotSelect", UiFactory.Ink);
            _root = root.gameObject;

            // The whole screen used to sit in its top two thirds: 256 px of margin above the
            // title against 654 px of nothing below the last slot, a 2.6:1 imbalance on a
            // 1920-tall phone. Everything moves down and the block closes up.
            Text title = UiFactory.Label(root, "Title", "BATTLE RUNNER", 84, UiFactory.Gold);
            UiFactory.Place((RectTransform)title.transform, 0.5f, 0.80f, 900f, 130f);

            Text subtitle = UiFactory.Label(root, "Subtitle", "choose your war", 32, UiFactory.Parchment);
            UiFactory.Place((RectTransform)subtitle.transform, 0.5f, 0.735f, 800f, 55f);

            for (int i = 0; i < SaveSlots.Count; i++)
            {
                int slot = i;
                float y = SlotY(i);

                Button play = UiFactory.ActionButton(root, $"Slot{i}", string.Empty,
                    UiFactory.InkSoft, () => _onPlay?.Invoke(slot));

                var widget = new SlotWidget { Label = play.GetComponentInChildren<Text>() };
                widget.Label.fontSize = 30;
                widget.PlayRect = (RectTransform)play.transform;
                widget.Row = i;
                PlacePlay(widget, false); // Refresh re-places it once occupancy is known

                Button erase = UiFactory.ActionButton(root, $"Erase{i}", EraseIdle,
                    new Color(0.30f, 0.12f, 0.12f), () => OnErasePressed(slot));
                UiFactory.Place((RectTransform)erase.transform, 0.84f, y, 200f, 130f);
                widget.Erase = erase;
                widget.EraseLabel = erase.GetComponentInChildren<Text>();
                widget.EraseLabel.fontSize = 26;

                _slots.Add(widget);
            }

            Hide();
        }

        public void Show(Action<int> onPlay, Action<int> onErase)
        {
            _onPlay = onPlay;
            _onErase = onErase;
            for (int i = 0; i < _slots.Count; i++) Disarm(i);
            _root.SetActive(true);
        }

        public void Refresh(IReadOnlyList<SaveSlotSummary> summaries)
        {
            for (int i = 0; i < _slots.Count && i < summaries.Count; i++)
            {
                SaveSlotSummary summary = summaries[i];
                _slots[i].Label.text = summary.Describe();
                _slots[i].Label.color = summary.Occupied ? Color.white : new Color(0.62f, 0.60f, 0.55f);

                // Nothing to erase in an empty slot, so do not offer it.
                _slots[i].Erase.gameObject.SetActive(summary.Occupied);

                // PLAY has to be re-placed, not just re-labelled. Place() mixes a
                // normalised centre with a pixel width, so 640 px at x = 0.44 and 210 px at
                // x = 0.82 overlapped by 53 units on a phone taller than 16:9 — ERASE drew
                // over PLAY's right end and, being the later sibling, took the taps there.
                // With nothing to erase the row is PLAY's alone, so it centres.
                PlacePlay(_slots[i], summary.Occupied);
                if (!summary.Occupied) Disarm(i);
            }
        }

        private static float SlotY(int row) => 0.56f - row * 0.16f;

        private static void PlacePlay(SlotWidget widget, bool occupied) =>
            UiFactory.Place(widget.PlayRect, occupied ? 0.42f : 0.5f, SlotY(widget.Row),
                occupied ? 560f : 640f, 130f);

        /// <summary>Driven by the state so an armed erase can expire on its own.</summary>
        public void Tick(float deltaTime)
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                SlotWidget widget = _slots[i];
                if (!widget.Armed) continue;
                widget.ArmedFor += deltaTime;
                if (widget.ArmedFor >= ArmedSeconds) Disarm(i);
            }
        }

        public void Hide() => _root.SetActive(false);

        private void OnErasePressed(int slot)
        {
            SlotWidget widget = _slots[slot];
            if (!widget.Armed)
            {
                for (int i = 0; i < _slots.Count; i++) Disarm(i); // only one armed at a time
                widget.Armed = true;
                widget.ArmedFor = 0f;
                widget.EraseLabel.text = EraseArmed;
                return;
            }

            Disarm(slot);
            _onErase?.Invoke(slot);
        }

        private void Disarm(int slot)
        {
            SlotWidget widget = _slots[slot];
            widget.Armed = false;
            widget.ArmedFor = 0f;
            widget.EraseLabel.text = EraseIdle;
        }
    }
}
