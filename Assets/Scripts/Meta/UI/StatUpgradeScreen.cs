using System;
using BattleRunner.Core.Stats;
using UnityEngine;
using UnityEngine.UI;

namespace BattleRunner.Meta.UI
{
    /// <summary>Three buttons and a recommended default — skippable in one tap (doc 01, R6).</summary>
    public sealed class StatUpgradeScreen
    {
        private readonly GameObject _root;
        private readonly Text _pointsLabel;
        private readonly Text _damageLabel;
        private readonly Text _healthLabel;
        private readonly Text _cooldownLabel;

        private Action<string> _onSpend;
        private Action _onContinue;

        public StatUpgradeScreen(Transform canvas)
        {
            RectTransform root = UiFactory.FullscreenPanel(canvas, "StatUpgrade", UiFactory.Ink);
            _root = root.gameObject;

            Text header = UiFactory.Label(root, "Header", "GROW STRONGER", 64, UiFactory.Gold);
            UiFactory.Place((RectTransform)header.transform, 0.5f, 0.86f, 900f, 100f);

            _pointsLabel = UiFactory.Label(root, "Points", "", 40, UiFactory.Parchment);
            UiFactory.Place((RectTransform)_pointsLabel.transform, 0.5f, 0.78f, 900f, 70f);

            Button dmg = UiFactory.ActionButton(root, "Damage", "", UiFactory.Blood,
                () => _onSpend?.Invoke(StatIds.Damage));
            UiFactory.Place((RectTransform)dmg.transform, 0.5f, 0.64f, 700f, 110f);
            _damageLabel = dmg.GetComponentInChildren<Text>();

            Button hp = UiFactory.ActionButton(root, "Health", "", new Color(0.2f, 0.5f, 0.25f),
                () => _onSpend?.Invoke(StatIds.Health));
            UiFactory.Place((RectTransform)hp.transform, 0.5f, 0.51f, 700f, 110f);
            _healthLabel = hp.GetComponentInChildren<Text>();

            Button cd = UiFactory.ActionButton(root, "Cooldown", "", UiFactory.Arcane,
                () => _onSpend?.Invoke(StatIds.Cooldown));
            UiFactory.Place((RectTransform)cd.transform, 0.5f, 0.38f, 700f, 110f);
            _cooldownLabel = cd.GetComponentInChildren<Text>();

            Button continueBtn = UiFactory.ActionButton(root, "Continue", "CONTINUE", UiFactory.InkSoft,
                () => _onContinue?.Invoke());
            UiFactory.Place((RectTransform)continueBtn.transform, 0.5f, 0.18f, 560f, 130f);

            Hide();
        }

        public void Show(Action<string> onSpend, Action onContinue)
        {
            _onSpend = onSpend;
            _onContinue = onContinue;
            _root.SetActive(true);
        }

        /// <summary>Refreshes labels; the recommended stat (lowest investment) is starred.</summary>
        public void Refresh(int unspent, int damagePoints, int healthPoints, int cooldownPoints,
            float damagePerPoint, float healthPerPoint, float cooldownPerPoint)
        {
            _pointsLabel.text = unspent == 1 ? "1 point to spend" : $"{unspent} points to spend";

            int min = Mathf.Min(damagePoints, Mathf.Min(healthPoints, cooldownPoints));
            string star(int points) => points == min ? "  *" : string.Empty;

            _damageLabel.text = $"+{damagePerPoint:0.#} DAMAGE  ({damagePoints}){star(damagePoints)}";
            _healthLabel.text = $"+{healthPerPoint:0.#} HEALTH  ({healthPoints}){star(healthPoints)}";
            _cooldownLabel.text = $"-{cooldownPerPoint:P0} COOLDOWN  ({cooldownPoints}){star(cooldownPoints)}";
        }

        public void Hide() => _root.SetActive(false);
    }
}
