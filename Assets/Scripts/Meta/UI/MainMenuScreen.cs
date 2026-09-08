using System;
using UnityEngine;
using UnityEngine.UI;

namespace BattleRunner.Meta.UI
{
    public sealed class MainMenuScreen
    {
        private readonly GameObject _root;
        private readonly Text _levelLabel;
        private readonly Text _statsLabel;
        private readonly Text _soundLabel;
        private Func<bool> _soundState;

        public MainMenuScreen(Transform canvas, Action onPlay, Action onNewRun)
        {
            RectTransform root = UiFactory.FullscreenPanel(canvas, "MainMenu", UiFactory.Ink);
            _root = root.gameObject;

            Text title = UiFactory.Label(root, "Title", "BATTLE RUNNER", 96, UiFactory.Gold);
            UiFactory.Place((RectTransform)title.transform, 0.5f, 0.78f, 900f, 140f);

            Text subtitle = UiFactory.Label(root, "Subtitle", "march. multiply. slay.", 34, UiFactory.Parchment);
            UiFactory.Place((RectTransform)subtitle.transform, 0.5f, 0.71f, 800f, 60f);

            _levelLabel = UiFactory.Label(root, "Level", "", 44, UiFactory.Parchment);
            UiFactory.Place((RectTransform)_levelLabel.transform, 0.5f, 0.58f, 900f, 70f);

            // Parchment, not Arcane. The stat readout was the only cool hue on a screen
            // that is otherwise gold, parchment and blood, and a saturated blue block of
            // text under a heading reads as a hyperlink, not as a character sheet.
            _statsLabel = UiFactory.Label(root, "Stats", "", 32, UiFactory.Parchment);
            UiFactory.Place((RectTransform)_statsLabel.transform, 0.5f, 0.50f, 900f, 120f);

            Button play = UiFactory.ActionButton(root, "Play", "SET FORTH", UiFactory.Blood, () => onPlay?.Invoke());
            UiFactory.Place((RectTransform)play.transform, 0.5f, 0.32f, 560f, 140f);

            // Starting over and erasing now live on the slot picker, where they act on a
            // named save rather than on "the" save.
            Button newRun = UiFactory.ActionButton(root, "ChangeSlot", "CHANGE SLOT", UiFactory.InkSoft,
                () => onNewRun?.Invoke());
            UiFactory.Place((RectTransform)newRun.transform, 0.5f, 0.19f, 560f, 96f);
            newRun.GetComponentInChildren<Text>().fontSize = 32;

            // A label-swapping button rather than a toggle, because UiFactory has no toggle
            // and no slider — the entire UI is Text, Image and Button. SlotSelectScreen and
            // SkillTreeScreen already swap a button's own label for their arm/disarm states,
            // so this is the vocabulary the game already speaks rather than a new widget.
            Button sound = UiFactory.ActionButton(root, "Sound", "SOUND ON", UiFactory.InkSoft,
                () => { _onToggleSound?.Invoke(); RefreshSound(); });
            UiFactory.Place((RectTransform)sound.transform, 0.5f, 0.10f, 360f, 74f);
            _soundLabel = sound.GetComponentInChildren<Text>();
            _soundLabel.fontSize = 26;

            Hide();
        }

        /// <summary>
        /// Wire the sound button to whatever owns the preference. Passed in rather than
        /// reached for: this screen is built by GameBootstrap before any service is handed
        /// around, and a UI class that knows about the audio service is a UI class that
        /// cannot be constructed without one.
        /// </summary>
        public void BindSound(Func<bool> readState, Action toggle)
        {
            _soundState = readState;
            _onToggleSound = toggle;
            RefreshSound();
        }

        private Action _onToggleSound;

        private void RefreshSound()
        {
            if (_soundLabel == null) return;
            bool on = _soundState == null || _soundState();
            _soundLabel.text = on ? "SOUND ON" : "SOUND OFF";
            _soundLabel.color = on ? UiFactory.Parchment : UiFactory.InkSoft * 2f;
        }

        public void Show(int levelIndex, string levelName, string statsSummary)
        {
            _levelLabel.text = $"Level {levelIndex + 1} — {levelName}";
            _statsLabel.text = statsSummary;
            RefreshSound();
            _root.SetActive(true);
        }



        public void Hide() => _root.SetActive(false);
    }
}
