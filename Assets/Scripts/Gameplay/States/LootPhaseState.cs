using System;
using System.Collections.Generic;
using BattleRunner.Core.Flow;
using BattleRunner.Core.Loot;
using BattleRunner.Core.Save;
using BattleRunner.Data.Definitions;
using BattleRunner.Meta.Services;
using UnityEngine;
using Random = System.Random;

namespace BattleRunner.Gameplay.States
{
    /// <summary>
    /// Loot roll -> Auto-Equip -> one tap out (doc 01, R6). Overflow from over-cap
    /// gate chains feeds loot luck; the double-loot rewarded ad rolls a second item.
    /// </summary>
    public sealed class LootPhaseState : IGameState
    {
        private readonly GameContext _ctx;
        private readonly Random _random = new Random();
        private bool _adUsed;

        public LootPhaseState(GameContext ctx) => _ctx = ctx;

        public void Enter()
        {
            _adUsed = false;
            _ctx.Hud.Hide();

            GearItemModel rolled = RollAndStore();
            bool equipped = RunAutoEquip();
            _ctx.SaveProfile();

            ShowCard(rolled, equipped);
        }

        public void Tick(float deltaTime) { }

        public void Exit() => _ctx.LootScreen.Hide();

        private GearItemModel RollAndStore()
        {
            LootTableDefinition tableDef = _ctx.CurrentLevel.LootTable;
            LootTableModel table = tableDef.ToModel();
            Dictionary<string, GearItemModel> lookup = tableDef.BuildItemLookup();

            float luck = _ctx.LastResult?.OverflowBonus(_ctx.Config.Balance.SoftCap) ?? 1f;
            int pity = _ctx.Profile.PityCounter;
            GearItemModel rolled = LootRoller.Roll(table, lookup, _random, ref pity, luck);
            _ctx.Profile.PityCounter = pity;

            _ctx.Profile.Inventory.Add(new GearItemInstance
            {
                InstanceId = Guid.NewGuid().ToString("N"),
                DefinitionId = rolled.Id,
                RolledTier = 0
            });
            return rolled;
        }

        /// <summary>Returns true when the newest roll ended up equipped.</summary>
        private bool RunAutoEquip()
        {
            Dictionary<string, GearItemDefinition> gearById = ProfileStatsResolver.GearById(_ctx.Config);
            var owned = new List<OwnedItem>();
            foreach (GearItemInstance instance in _ctx.Profile.Inventory)
                if (gearById.TryGetValue(instance.DefinitionId, out GearItemDefinition def))
                    owned.Add(new OwnedItem(instance.InstanceId, def.ToModel()));

            Dictionary<GearSlot, string> best = AutoEquip.PickBest(owned, _ctx.Config.Balance.StatWeights());
            string newestInstance = _ctx.Profile.Inventory[_ctx.Profile.Inventory.Count - 1].InstanceId;
            bool newestEquipped = false;
            foreach (KeyValuePair<GearSlot, string> pick in best)
            {
                _ctx.Profile.SetEquipped(pick.Key, pick.Value);
                if (pick.Value == newestInstance) newestEquipped = true;
            }
            return newestEquipped;
        }

        private void ShowCard(GearItemModel rolled, bool equipped)
        {
            Dictionary<string, GearItemDefinition> gearById = ProfileStatsResolver.GearById(_ctx.Config);
            gearById.TryGetValue(rolled.Id, out GearItemDefinition def);
            string displayName = def != null ? def.DisplayName : rolled.Id;
            string statsText = def != null
                ? ProfileStatsResolver.DescribeModifiers(def) + $"\n\n<i>{def.Flavor}</i>"
                : string.Empty;
            float power = ItemPower.Compute(rolled, _ctx.Config.Balance.StatWeights());

            _ctx.LootScreen.Show(rolled, displayName, statsText, power, equipped,
                adAvailable: !_adUsed && _ctx.Ads.IsRewardedReady(AdPlacement.LootDouble),
                onContinue: () => _ctx.Machine.TransitionTo(_ctx.UpgradeState),
                onDouble: OnDoubleLoot);
        }

        private void OnDoubleLoot()
        {
            if (_adUsed) return;
            _adUsed = true;
            _ctx.LootScreen.HideDoubleButton();

            _ctx.Ads.ShowRewarded(AdPlacement.LootDouble, granted =>
            {
                // The player may have tapped Continue while the ad played; a reward
                // landing on an exited state must not resurrect this screen (review C1).
                if (!ReferenceEquals(_ctx.Machine.Current, this)) return;
                if (!granted) return;
                GearItemModel bonus = RollAndStore();
                bool equipped = RunAutoEquip();
                _ctx.SaveProfile();
                _ctx.LootScreen.SetHeader("THE BOSS YIELDS... TWICE!");
                ShowCard(bonus, equipped);
                Debug.Log("[Loot] Double-loot reward granted.");
            });
        }
    }
}
