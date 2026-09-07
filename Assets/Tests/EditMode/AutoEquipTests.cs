using System.Collections.Generic;
using BattleRunner.Core.Loot;
using BattleRunner.Core.Stats;
using NUnit.Framework;

namespace BattleRunner.Tests
{
    [TestFixture]
    public class AutoEquipTests
    {
        private static readonly Dictionary<string, float> Weights = new Dictionary<string, float>
        {
            [StatIds.Damage] = 1f,
            [StatIds.Health] = 0.5f,
            [StatIds.Cooldown] = 0.8f
        };

        private static GearItemModel Item(string id, GearSlot slot, float damageFlat, float healthFlat = 0f) =>
            new GearItemModel(id, slot, Rarity.Common, new[]
            {
                new StatModifier(StatIds.Damage, ModifierKind.Flat, damageFlat),
                new StatModifier(StatIds.Health, ModifierKind.Flat, healthFlat)
            });

        [Test]
        public void PicksHighestPowerPerSlot()
        {
            var inventory = new[]
            {
                new OwnedItem("a", Item("weak_sword", GearSlot.Weapon, 5f)),
                new OwnedItem("b", Item("strong_sword", GearSlot.Weapon, 50f)),
                new OwnedItem("c", Item("armor", GearSlot.Armor, 0f, 40f))
            };

            var picks = AutoEquip.PickBest(inventory, Weights);
            Assert.AreEqual("b", picks[GearSlot.Weapon]);
            Assert.AreEqual("c", picks[GearSlot.Armor]);
            Assert.IsFalse(picks.ContainsKey(GearSlot.Relic), "no relic owned, none equipped");
        }

        [Test]
        public void PercentModifiers_CountTowardPower()
        {
            var flat = new GearItemModel("flat", GearSlot.Weapon, Rarity.Common,
                new[] { new StatModifier(StatIds.Damage, ModifierKind.Flat, 30f) });
            var pct = new GearItemModel("pct", GearSlot.Weapon, Rarity.Common,
                new[] { new StatModifier(StatIds.Damage, ModifierKind.Percent, 0.5f) });

            // 0.5 percent * 100 percentScale = 50 normalized > 30 flat.
            var picks = AutoEquip.PickBest(new[]
            {
                new OwnedItem("f", flat),
                new OwnedItem("p", pct)
            }, Weights);
            Assert.AreEqual("p", picks[GearSlot.Weapon]);
        }

        [Test]
        public void Ties_AreDeterministic_FirstWins()
        {
            var inventory = new[]
            {
                new OwnedItem("first", Item("s1", GearSlot.Weapon, 10f)),
                new OwnedItem("second", Item("s2", GearSlot.Weapon, 10f))
            };
            for (int i = 0; i < 5; i++)
                Assert.AreEqual("first", AutoEquip.PickBest(inventory, Weights)[GearSlot.Weapon]);
        }

        [Test]
        public void EmptyInventory_ReturnsNoPicks()
        {
            Assert.IsEmpty(AutoEquip.PickBest(new OwnedItem[0], Weights));
        }

        [Test]
        public void StatOutsideWeights_ContributesNothing()
        {
            var exotic = new GearItemModel("exotic", GearSlot.Weapon, Rarity.Epic,
                new[] { new StatModifier("mystery", ModifierKind.Flat, 9999f) });
            var plain = Item("plain", GearSlot.Weapon, 1f);

            var picks = AutoEquip.PickBest(new[]
            {
                new OwnedItem("e", exotic),
                new OwnedItem("p", plain)
            }, Weights);
            Assert.AreEqual("p", picks[GearSlot.Weapon]);
        }
        // --- Item Power units --------------------------------------------------
        //
        // This is the same bug that produced "Focus -0 %" on the loot card, in a second
        // place. ModifierKind alone does NOT decide a stat's units: fraction-valued stats
        // are granted Flat because StatSheet resolves final = (base + flat) * (1 + percent)
        // and their base is 0, so Percent would multiply nothing. ItemPower scored those
        // raw, making a "+1% Focus" affix worth 0.01 points instead of 1.

        private static GearItemModel Relic(params StatModifier[] mods) =>
            new GearItemModel("relic", GearSlot.Relic, Rarity.Common, mods);

        [Test]
        public void FractionStatGrantedFlatIsScoredAsAPercentage()
        {
            var weights = new Dictionary<string, float> { [StatIds.SpellPower] = 1f };

            // +40% spell power, granted Flat as 0.40 the way every talent and affix does.
            float power = ItemPower.Compute(
                Relic(new StatModifier(StatIds.SpellPower, ModifierKind.Flat, 0.40f)), weights);

            Assert.AreEqual(40f, power, 1e-4f,
                "0.40 on a fraction stat is 40 points, not 0.40 — it used to score 0.4");
        }

        [Test]
        public void AbsoluteStatGrantedFlatIsStillScoredRaw()
        {
            var weights = new Dictionary<string, float> { [StatIds.Damage] = 1f };

            float power = ItemPower.Compute(
                Relic(new StatModifier(StatIds.Damage, ModifierKind.Flat, 4f)), weights);

            Assert.AreEqual(4f, power, 1e-4f, "+4 Might is four points, not four hundred");
        }

        [Test]
        public void EveryFractionStatAgreesWithStatFormat()
        {
            // The two units rules must not drift apart: whatever StatFormat prints as a
            // percentage, ItemPower has to score as one, or the card and the number under
            // it describe different items.
            string[] all =
            {
                StatIds.Damage, StatIds.Health, StatIds.Cooldown, StatIds.GateYield,
                StatIds.RunSpeed, StatIds.EnemyResist, StatIds.SpellPower, StatIds.Fortune,
                StatIds.ShieldDuration
            };

            foreach (string statId in all)
            {
                var weights = new Dictionary<string, float> { [statId] = 1f };
                float scored = ItemPower.Compute(
                    Relic(new StatModifier(statId, ModifierKind.Flat, 0.5f)), weights);
                float expected = StatFormat.IsFraction(statId) ? 50f : 0.5f;
                Assert.AreEqual(expected, scored, 1e-4f, statId);
            }
        }

        [Test]
        public void AFractionAffixCanNowOutrankAFlatOne()
        {
            // The Ember Talisman case from the device: +2 Might and +1% Focus scored
            // exactly 2, identical to an item with no second affix at all.
            var weights = new Dictionary<string, float>
            {
                [StatIds.Damage] = 1f,
                [StatIds.Fortune] = 1f
            };

            float withFocus = ItemPower.Compute(Relic(
                new StatModifier(StatIds.Damage, ModifierKind.Flat, 2f),
                new StatModifier(StatIds.Fortune, ModifierKind.Flat, 0.01f)), weights);
            float without = ItemPower.Compute(Relic(
                new StatModifier(StatIds.Damage, ModifierKind.Flat, 2f)), weights);

            Assert.Greater(withFocus, without, "the second affix has to be worth something");
            Assert.AreEqual(3f, withFocus, 1e-4f);
        }

    }
}
