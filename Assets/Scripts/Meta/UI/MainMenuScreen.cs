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

            // Starting over and erasing now live on the slot picker, where they act on a
            // named save rather than on "the" save.
            Button newRun = UiFactory.ActionButton(root, "ChangeSlot", "CHANGE SLOT", UiFactory.InkSoft,
                () => onNewRun?.Invoke());
            UiFactory.Place((RectTransform)newRun.transform, 0.5f, 0.19f, 560f, 96f);
            newRun.GetComponentInChildren<Text>().fontSize = 32;

            Hide();
        }

        public void Show(int levelIndex, string levelName, string statsSummary)
        {
            _levelLabel.text = $"Level {levelIndex + 1} — {levelName}";
            _statsLabel.text = statsSummary;
            _root.SetActive(true);
        }



        public void Hide() => _root.SetActive(false);
    }
}
