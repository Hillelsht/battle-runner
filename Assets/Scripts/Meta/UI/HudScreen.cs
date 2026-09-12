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
        private readonly Image _bossBarFillImage;
        private readonly Text _bossName;
        private readonly Text _roundLabel;
        private string _lastForce;

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

            _bossBarFill = UiFactory.Panel(barBack, "Fill", UiFactory.Blood, rounded: false);
            _bossBarFillImage = _bossBarFill.GetComponent<Image>();
            _bossBarFill.anchorMin = new Vector2(0f, 0f);
            _bossBarFill.anchorMax = new Vector2(1f, 1f);
            _bossBarFill.offsetMin = new Vector2(4f, 4f);
            _bossBarFill.offsetMax = new Vector2(-4f, -4f);
            _bossBarFill.pivot = new Vector2(0f, 0.5f);

            // After the fill, not before: the frame's corner notches sit 6-13 units in from
            // each edge while the opaque Blood fill is inset only 4, so a frame added at
            // sibling index 0 would keep its ring and lose all four notches behind the fill.
            UiFactory.AddFrame(barBack);

            _bossName = UiFactory.Label(barBack, "BossName", "", 30, UiFactory.Parchment);
            UiFactory.Stretch((RectTransform)_bossName.transform);

            // Where the player is, Mario-style: "3-2" plus the name of the world they are in.
            // During a round they were never told either — the HUD had exactly four text
            // elements (force, spell, shield, boss name) and the level name appeared only on
            // the main menu, which is the one place it does not matter.
            _roundLabel = UiFactory.Label(root, "Round", string.Empty, 26, UiFactory.Parchment,
                TextAnchor.MiddleLeft);
            UiFactory.Place((RectTransform)_roundLabel.transform, 0.30f, 0.965f, 520f, 44f);

            HideBossBar();
            Hide();
        }

        public void Show() => _root.SetActive(true);
        public void Hide() => _root.SetActive(false);

        public void SetForce(double force)
        {
            string shown = BattleRunner.Core.Stats.StatFormat.Army(force);
            if (shown == _lastForce) return;
            _lastForce = shown;
            _forceLabel.text = shown;
        }

        /// <summary>
        /// The two ability readouts, now that both are magazines rather than single casts.
        ///
        /// A player with three charges needs to see THREE, and a bare "SPELL 2.1s" cannot
        /// say that — it was the right readout for an ability that was either available or
        /// not, and it is the wrong one for an ability you can hold a stock of. Pips carry
        /// the count and the tail carries the refill, so a full magazine reads as a clean
        /// verb and a partial one shows both what is in hand and what is coming.
        /// </summary>
        public void SetAbilities(float spellFill, int spellCharges, int spellCapacity,
            float shieldFill, int shieldCharges, int shieldCapacity, bool shieldActive)
        {
            _spellLabel.text = spellCharges > 0
                ? "SPELL ^ " + Pips(spellCharges, spellCapacity)
                : $"SPELL {Pips(0, spellCapacity)} {RefillTail(spellFill)}";
            _spellLabel.color = spellCharges > 0 ? UiFactory.Arcane : UiFactory.InkSoft * 2f;

            _shieldLabel.text = shieldActive ? "SHIELDED " + Pips(shieldCharges, shieldCapacity)
                : shieldCharges > 0 ? "SHIELD v " + Pips(shieldCharges, shieldCapacity)
                : $"SHIELD {Pips(0, shieldCapacity)} {RefillTail(shieldFill)}";
            _shieldLabel.color = shieldActive ? UiFactory.Gold :
                shieldCharges > 0 ? UiFactory.Parchment : UiFactory.InkSoft * 2f;
        }

        /// <summary>
        /// Charges in hand, as a plain count over the capacity.
        ///
        /// Pips (filled and hollow diamonds) read better at a glance and were the first
        /// version of this, but U+25C6 and U+25C7 are Geometric Shapes and the HUD draws in
        /// Unity's built-in font — a glyph that is missing there renders as a box on device
        /// and there is no way to find that out from here. A count always renders. If a
        /// device screenshot shows the count reading poorly mid-run, pips with a bundled
        /// font are the fix, not a guess at what Arial happens to carry.
        ///
        /// Empty at capacity 1, so a player who has bought no charge talents sees exactly
        /// the readout that shipped before magazines existed.
        /// </summary>
        private static string Pips(int charges, int capacity)
        {
            if (capacity <= 1) return string.Empty;
            return charges.ToString() + "/" + capacity.ToString();
        }

        /// <summary>How close the next charge is, as a short bar rather than a countdown.</summary>
        private static string RefillTail(float fill)
        {
            int lit = Mathf.Clamp(Mathf.RoundToInt(fill * 3f), 0, 3);
            return new string('\u00B7', lit + 1);
        }

        /// <param name="withHealth">
        /// False while a boss is only THREATENING. There is nothing to whittle down on a
        /// round it does not fight, and a full red bar would promise otherwise — so the plate
        /// carries the name and the frame, and the fill is switched off entirely.
        /// </param>
        /// <param name="fillTint">
        /// A champion's aura colour, or null for an unmodified boss. The bar does not TAKE the
        /// colour — a green health bar on a Haunted boss would stop reading as health — it is
        /// pulled a third of the way toward it, which is enough to tell two champions apart at
        /// a glance while the bar stays a bar.
        /// </param>
        public void ShowBossBar(string bossName, bool withHealth = true, Color? fillTint = null)
        {
            _bossName.text = bossName;
            _bossBarFillImage.color = fillTint.HasValue
                ? Color.Lerp(UiFactory.Blood, Normalized(fillTint.Value), 0.34f)
                : UiFactory.Blood;
            _bossBarFill.gameObject.SetActive(withHealth);
            if (withHealth) SetBossHp(1f);
            _bossBarRoot.SetActive(true);
        }

        /// <summary>
        /// Affix tints are authored as HDR emission colours — Frenzied is (1.70, 0.30, 0.14) —
        /// and UI Image colours clamp at 1, which would turn every warm affix into the same
        /// saturated red. Dividing by the brightest channel keeps the HUE that distinguishes
        /// them and throws away the intensity the bar cannot show anyway.
        /// </summary>
        private static Color Normalized(Color c)
        {
            float peak = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            if (peak <= 0.0001f) return Color.white;
            return new Color(c.r / peak, c.g / peak, c.b / peak, 1f);
        }

        public void SetBossHp(float fraction)
        {
            fraction = Mathf.Clamp01(fraction);
            _bossBarFill.anchorMax = new Vector2(Mathf.Max(0.001f, fraction), 1f);
        }

        public void HideBossBar() => _bossBarRoot.SetActive(false);

        /// <summary>The act-and-round marker, e.g. "3-2  THE BONE WASTES".</summary>
        public void SetRound(string text) => _roundLabel.text = text ?? string.Empty;
    }
}
