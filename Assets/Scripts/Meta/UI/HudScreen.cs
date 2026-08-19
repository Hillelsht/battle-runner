using UnityEngine;
using UnityEngine.UI;

namespace BattleRunner.Meta.UI
{
    /// <summary>Run + boss HUD. The force counter updates on change only — never per frame (doc 04).</summary>
    public sealed class HudScreen
    {
        private readonly GameObject _root;
        private readonly Text _forceLabel;
        private readonly Text _spellLabel;
        private readonly Text _shieldLabel;
        private readonly GameObject _bossBarRoot;
        private readonly RectTransform _bossBarFill;
        private readonly Text _bossName;
        private long _lastForce = long.MinValue;

        public HudScreen(Transform canvas)
        {
            var rootGo = new GameObject("Hud", typeof(RectTransform));
            rootGo.transform.SetParent(canvas, false);
            UiFactory.Stretch((RectTransform)rootGo.transform);
            _root = rootGo;
            Transform root = rootGo.transform;

            _forceLabel = UiFactory.Label(root, "Force", "5", 84, UiFactory.Gold);
            UiFactory.Place((RectTransform)_forceLabel.transform, 0.5f, 0.92f, 700f, 110f);

            _spellLabel = UiFactory.Label(root, "Spell", "SPELL ^", 34, UiFactory.Arcane);
            UiFactory.Place((RectTransform)_spellLabel.transform, 0.82f, 0.07f, 320f, 70f);

            _shieldLabel = UiFactory.Label(root, "Shield", "SHIELD v", 34, UiFactory.Parchment);
            UiFactory.Place((RectTransform)_shieldLabel.transform, 0.18f, 0.07f, 320f, 70f);

            RectTransform barBack = UiFactory.Panel(root, "BossBarBack", UiFactory.InkSoft);
            UiFactory.Place(barBack, 0.5f, 0.83f, 820f, 44f);
            _bossBarRoot = barBack.gameObject;

            _bossBarFill = UiFactory.Panel(barBack, "Fill", UiFactory.Blood);
            _bossBarFill.anchorMin = new Vector2(0f, 0f);
            _bossBarFill.anchorMax = new Vector2(1f, 1f);
            _bossBarFill.offsetMin = new Vector2(4f, 4f);
            _bossBarFill.offsetMax = new Vector2(-4f, -4f);
            _bossBarFill.pivot = new Vector2(0f, 0.5f);

            _bossName = UiFactory.Label(barBack, "BossName", "", 30, UiFactory.Parchment);
            UiFactory.Stretch((RectTransform)_bossName.transform);

            HideBossBar();
            Hide();
        }

        public void Show() => _root.SetActive(true);
        public void Hide() => _root.SetActive(false);

        public void SetForce(long force)
        {
            if (force == _lastForce) return;
            _lastForce = force;
            _forceLabel.text = force.ToString("N0");
        }

        public void SetCooldowns(float spellRemaining, float shieldRemaining, bool shieldActive)
        {
            _spellLabel.text = spellRemaining <= 0f ? "SPELL ^" : $"SPELL {spellRemaining:0.0}s";
            _spellLabel.color = spellRemaining <= 0f ? UiFactory.Arcane : UiFactory.InkSoft * 2f;

            _shieldLabel.text = shieldActive ? "SHIELDED" :
                shieldRemaining <= 0f ? "SHIELD v" : $"SHIELD {shieldRemaining:0.0}s";
            _shieldLabel.color = shieldActive ? UiFactory.Gold :
                shieldRemaining <= 0f ? UiFactory.Parchment : UiFactory.InkSoft * 2f;
        }

        public void ShowBossBar(string bossName)
        {
            _bossName.text = bossName;
            _bossBarRoot.SetActive(true);
        }

        public void SetBossHp(float fraction)
        {
            fraction = Mathf.Clamp01(fraction);
            _bossBarFill.anchorMax = new Vector2(Mathf.Max(0.001f, fraction), 1f);
        }

        public void HideBossBar() => _bossBarRoot.SetActive(false);
    }
}
