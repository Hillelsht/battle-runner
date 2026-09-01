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
        private readonly Text _newRunLabel;
        private bool _newRunArmed;

        private const string NewRunIdle = "NEW GAME";
        private const string NewRunArmed = "ERASE EVERYTHING?";

        /// <summary>The armed state expires on its own. Show() is the only other thing that
        /// disarms, and it needs a state transition — so without this, arming it and then
        /// backgrounding the app leaves a one-tap wipe waiting on resume.</summary>
        private const float ArmedSeconds = 4f;

        private float _armedFor;

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

            _statsLabel = UiFactory.Label(root, "Stats", "", 32, UiFactory.Arcane);
            UiFactory.Place((RectTransform)_statsLabel.transform, 0.5f, 0.50f, 900f, 120f);

            Button play = UiFactory.ActionButton(root, "Play", "SET FORTH", UiFactory.Blood, () => onPlay?.Invoke());
            UiFactory.Place((RectTransform)play.transform, 0.5f, 0.32f, 560f, 140f);

            // Wiping a save is not undoable, so it takes two taps. The first arms it and says
            // exactly what is about to happen; the second does it. Re-opening the menu disarms.
            Button newRun = UiFactory.ActionButton(root, "NewRun", NewRunIdle, UiFactory.InkSoft, () =>
            {
                if (!_newRunArmed)
                {
                    _newRunArmed = true;
                    _newRunLabel.text = NewRunArmed;
                    _newRunLabel.color = UiFactory.Blood;
                    _armedFor = 0f;
                    return;
                }
                Disarm();
                onNewRun?.Invoke();
            });
            UiFactory.Place((RectTransform)newRun.transform, 0.5f, 0.19f, 560f, 96f);
            _newRunLabel = newRun.GetComponentInChildren<Text>();
            _newRunLabel.fontSize = 32;

            Hide();
        }

        public void Show(int levelIndex, string levelName, string statsSummary)
        {
            _levelLabel.text = $"Level {levelIndex + 1} — {levelName}";
            _statsLabel.text = statsSummary;
            Disarm();
            _root.SetActive(true);
        }

        /// <summary>Driven by MainMenuState so the armed state can expire.</summary>
        public void Tick(float deltaTime)
        {
            if (!_newRunArmed) return;
            _armedFor += deltaTime;
            if (_armedFor >= ArmedSeconds) Disarm();
        }

        private void Disarm()
        {
            _newRunArmed = false;
            _armedFor = 0f;
            _newRunLabel.text = NewRunIdle;
            _newRunLabel.color = UiFactory.Parchment;
        }

        public void Hide() => _root.SetActive(false);
    }
}
