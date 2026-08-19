using System;
using BattleRunner.Core.Loot;
using UnityEngine;
using UnityEngine.UI;

namespace BattleRunner.Meta.UI
{
    /// <summary>
    /// Post-boss loot reveal. One tap on Auto-Equip keeps the loop under five seconds
    /// (doc 01, R6); the double-loot rewarded ad is the first monetization touchpoint.
    /// </summary>
    public sealed class LootScreen
    {
        private readonly GameObject _root;
        private readonly Text _header;
        private readonly RectTransform _card;
        private readonly Text _itemName;
        private readonly Text _itemStats;
        private readonly Text _powerLabel;
        private readonly GameObject _doubleButtonGo;

        private Action _onContinue;
        private Action _onDouble;

        public LootScreen(Transform canvas)
        {
            RectTransform root = UiFactory.FullscreenPanel(canvas, "Loot", UiFactory.Ink);
            _root = root.gameObject;

            _header = UiFactory.Label(root, "Header", "THE BOSS YIELDS...", 56, UiFactory.Gold);
            UiFactory.Place((RectTransform)_header.transform, 0.5f, 0.85f, 900f, 90f);

            _card = UiFactory.Panel(root, "Card", UiFactory.InkSoft);
            UiFactory.Place(_card, 0.5f, 0.62f, 760f, 460f);

            _itemName = UiFactory.Label(_card, "Name", "", 52, Color.white);
            UiFactory.Place((RectTransform)_itemName.transform, 0.5f, 0.82f, 700f, 80f);

            _itemStats = UiFactory.Label(_card, "Stats", "", 38, UiFactory.Parchment);
            UiFactory.Place((RectTransform)_itemStats.transform, 0.5f, 0.5f, 700f, 220f);

            _powerLabel = UiFactory.Label(_card, "Power", "", 34, UiFactory.Arcane);
            UiFactory.Place((RectTransform)_powerLabel.transform, 0.5f, 0.13f, 700f, 60f);

            Button doubleBtn = UiFactory.ActionButton(root, "Double", "DOUBLE LOOT  (AD)", UiFactory.Arcane,
                () => _onDouble?.Invoke());
            UiFactory.Place((RectTransform)doubleBtn.transform, 0.5f, 0.32f, 560f, 110f);
            _doubleButtonGo = doubleBtn.gameObject;

            Button continueBtn = UiFactory.ActionButton(root, "Continue", "AUTO-EQUIP & CONTINUE", UiFactory.Blood,
                () => _onContinue?.Invoke());
            UiFactory.Place((RectTransform)continueBtn.transform, 0.5f, 0.18f, 640f, 130f);

            Hide();
        }

        public void Show(GearItemModel item, string displayName, string statsText, float itemPower,
            bool equippedUpgrade, bool adAvailable, Action onContinue, Action onDouble)
        {
            _onContinue = onContinue;
            _onDouble = onDouble;

            Color rarityColor = UiFactory.RarityColors[(int)item.Rarity];
            _itemName.text = $"{displayName}\n<size=30>{item.Rarity} {item.Slot}</size>";
            _itemName.color = rarityColor;
            _itemStats.text = statsText;
            _powerLabel.text = equippedUpgrade
                ? $"Item Power {itemPower:0}  — equipped!"
                : $"Item Power {itemPower:0}  — kept in inventory";
            _doubleButtonGo.SetActive(adAvailable);
            _root.SetActive(true);
        }

        public void SetHeader(string text) => _header.text = text;

        public void HideDoubleButton() => _doubleButtonGo.SetActive(false);

        public void Hide() => _root.SetActive(false);
    }
}
