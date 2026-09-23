using BattleRunner.Core.Text;
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

            Text title = UiFactory.Label(root, "Title", Loc.Get(LocKey.GameTitle), 96, UiFactory.Gold,
                TextAnchor.MiddleCenter, LocKey.GameTitle);
            UiFactory.Place((RectTransform)title.transform, 0.5f, 0.78f, 900f, 140f);

            Text subtitle = UiFactory.Label(root, "Subtitle", Loc.Get(LocKey.MenuTagline), 34, UiFactory.Parchment,
                TextAnchor.MiddleCenter, LocKey.MenuTagline);
            UiFactory.Place((RectTransform)subtitle.transform, 0.5f, 0.71f, 800f, 60f);

            _levelLabel = UiFactory.Label(root, "Level", "", 44, UiFactory.Parchment);
            UiFactory.Place((RectTransform)_levelLabel.transform, 0.5f, 0.58f, 900f, 70f);

            // Parchment, not Arcane. The stat readout was the only cool hue on a screen
            // that is otherwise gold, parchment and blood, and a saturated blue block of
            // text under a heading reads as a hyperlink, not as a character sheet.
            _statsLabel = UiFactory.Label(root, "Stats", "", 32, UiFactory.Parchment);
            UiFactory.Place((RectTransform)_statsLabel.transform, 0.5f, 0.50f, 900f, 120f);

            Button play = UiFactory.ActionButton(root, "Play", Loc.Get(LocKey.MenuSetForth), UiFactory.Blood,
                () => onPlay?.Invoke(), key: LocKey.MenuSetForth);
            UiFactory.Place((RectTransform)play.transform, 0.5f, 0.32f, 560f, 140f);

            // Starting over and erasing now live on the slot picker, where they act on a
            // named save rather than on "the" save.
            Button newRun = UiFactory.ActionButton(root, "ChangeSlot", Loc.Get(LocKey.MenuChangeSlot), UiFactory.InkSoft,
                () => onNewRun?.Invoke(), labelSize: 32, key: LocKey.MenuChangeSlot);
            UiFactory.Place((RectTransform)newRun.transform, 0.5f, 0.19f, 560f, 96f);

            // A label-swapping button rather than a toggle, because UiFactory has no toggle
            // and no slider — the entire UI is Text, Image and Button. SlotSelectScreen and
            // SkillTreeScreen already swap a button's own label for their arm/disarm states,
            // so this is the vocabulary the game already speaks rather than a new widget.
            Button sound = UiFactory.ActionButton(root, "Sound", Loc.Get(LocKey.MenuSoundOn), UiFactory.InkSoft,
                () => { _onToggleSound?.Invoke(); RefreshSound(); }, labelSize: 26);
            UiFactory.Place((RectTransform)sound.transform, 0.30f, 0.10f, 320f, 74f);
            _soundLabel = sound.GetComponentInChildren<Text>();

            // THE LABEL IS ALWAYS IN THE LANGUAGE IT SWITCHES TO, never translated into the
            // current one. Someone whose device guessed wrong is looking at a screen they
            // cannot read, and the one control that rescues them has to be legible from
            // outside the language it is sitting in.
            Button language = UiFactory.ActionButton(root, "Language",
                Languages.NativeName(Languages.Next(Loc.Language)), UiFactory.InkSoft,
                () => { _onCycleLanguage?.Invoke(); }, labelSize: 26);
            UiFactory.Place((RectTransform)language.transform, 0.70f, 0.10f, 320f, 74f);
            _languageLabel = language.GetComponentInChildren<Text>();

            Hide();
        }

        /// <summary>
        /// Wire the sound button to whatever owns the preference. Passed in rather than
        /// reached for: this screen is built by GameBootstrap before any service is handed
        /// around, and a UI class that knows about the audio service is a UI class that
        /// cannot be constructed without one.
        /// </summary>
        private Action _onCycleLanguage;
        private Text _languageLabel;

        /// <summary>
        /// Wire the language button. Late-bound like BindSound, and for the same reason: the
        /// screen is built by GameBootstrap before the services are handed around.
        /// </summary>
        public void BindLanguage(Action cycle)
        {
            _onCycleLanguage = cycle;
            RefreshLanguage();
        }

        /// <summary>Re-label the button with whatever it switches to next.</summary>
        public void RefreshLanguage()
        {
            if (_languageLabel != null)
                _languageLabel.text = Languages.NativeName(Languages.Next(Loc.Language));
        }

        public void BindSound(Func<bool> readState, Action toggle)
        {
            _soundState = readState;
            _onToggleSound = toggle;
            RefreshSound();
            RefreshLanguage();
        }

        private Action _onToggleSound;

        public void RefreshSound()
        {
            if (_soundLabel == null) return;
            bool on = _soundState == null || _soundState();
            UiFactory.SetText(_soundLabel, Loc.Get(on ? LocKey.MenuSoundOn : LocKey.MenuSoundOff));
            _soundLabel.color = on ? UiFactory.Parchment : UiFactory.InkSoft * 2f;
        }

        public void Show(int levelIndex, string levelName, string statsSummary)
        {
            UiFactory.SetText(_levelLabel, Loc.Format(LocKey.MenuLevelLine, levelIndex + 1, levelName));
            UiFactory.SetText(_statsLabel, statsSummary);
            RefreshSound();
            RefreshLanguage();
            _root.SetActive(true);
        }



        public void Hide() => _root.SetActive(false);
    }
}
