using System;
using System.Collections.Generic;
using BattleRunner.Core.Loot;
using BattleRunner.Core.Stats;
using NUnit.Framework;

namespace BattleRunner.Tests
{
    [TestFixture]
    public class LootRollerTests
    {
        private static readonly StatModifier[] NoMods = Array.Empty<StatModifier>();

        private static Dictionary<string, GearItemModel> Items() => new Dictionary<string, GearItemModel>
        {
            ["sword_common"] = new GearItemModel("sword_common", GearSlot.Weapon, Rarity.Common, NoMods),
            ["sword_rare"] = new GearItemModel("sword_rare", GearSlot.Weapon, Rarity.Rare, NoMods),
            ["sword_epic"] = new GearItemModel("sword_epic", GearSlot.Weapon, Rarity.Epic, NoMods),
            ["sword_legendary"] = new GearItemModel("sword_legendary", GearSlot.Weapon, Rarity.Legendary, NoMods)
        };

        private static LootTableModel Table(int pity = 20) => new LootTableModel(new[]
        {
            new LootEntry("sword_common", 70f),
            new LootEntry("sword_rare", 20f),
            new LootEntry("sword_epic", 8f),
            new LootEntry("sword_legendary", 2f)
        }, pity);

        [Test]
        public void Distribution_MatchesWeightsWithinTolerance()
        {
            var items = Items();
            var table = Table(pity: 0); // pity off for a clean distribution check
            var random = new Random(42);
            var counts = new Dictionary<string, int>();
            const int rolls = 10_000;
            int pity = 0;

            for (int i = 0; i < rolls; i++)
            {
                GearItemModel item = LootRoller.Roll(table, items, random, ref pity);
                counts.TryGetValue(item.Id, out int c);
                counts[item.Id] = c + 1;
            }

            Assert.AreEqual(0.70f, counts["sword_common"] / (float)rolls, 0.02f);
            Assert.AreEqual(0.20f, counts["sword_rare"] / (float)rolls, 0.02f);
            Assert.AreEqual(0.08f, counts["sword_epic"] / (float)rolls, 0.01f);
            Assert.AreEqual(0.02f, counts["sword_legendary"] / (float)rolls, 0.01f);
        }

        [Test]
        public void PityFloor_GuaranteesLegendary()
        {
            var items = Items();
            var table = Table(pity: 20);
            var random = new Random(7);
            int pity = 0;
            int sinceLegendary = 0;

            for (int i = 0; i < 2_000; i++)
            {
                GearItemModel item = LootRoller.Roll(table, items, random, ref pity);
                if (item.Rarity == Rarity.Legendary)
                {
                    sinceLegendary = 0;
                }
                else
                {
                    sinceLegendary++;
                    Assert.LessOrEqual(sinceLegendary, 20, "pity floor breached");
                }
            }
        }

        [Test]
        public void PityCounter_ResetsOnLegendary()
        {
            var items = Items();
            var table = Table(pity: 3);
            var random = new Random(1);
            int pity = 3; // at the floor: next roll must be legendary
            GearItemModel item = LootRoller.Roll(table, items, random, ref pity);
            Assert.AreEqual(Rarity.Legendary, item.Rarity);
            Assert.AreEqual(0, pity);
        }

        [Test]
        public void LuckMultiplier_ShiftsRollsTowardHighRarity()
        {
            var items = Items();
            var table = Table(pity: 0);
            int highLucky = 0, highPlain = 0;
            const int rolls = 10_000;

            var randomA = new Random(99);
            int pityA = 0;
            for (int i = 0; i < rolls; i++)
                if (LootRoller.Roll(table, items, randomA, ref pityA).Rarity >= Rarity.Epic) highPlain++;

            var randomB = new Random(99);
            int pityB = 0;
            for (int i = 0; i < rolls; i++)
                if (LootRoller.Roll(table, items, randomB, ref pityB, luckMultiplier: 3f).Rarity >= Rarity.Epic) highLucky++;

            Assert.Greater(highLucky, highPlain * 2, "3x luck should much more than double Epic+ rate at these weights");
        }

        [Test]
        public void TableWithoutLegendaries_PityDoesNotLoopForever()
        {
            var items = Items();
            var table = new LootTableModel(new[] { new LootEntry("sword_common", 1f) }, 2);
            var random = new Random(5);
            int pity = 10; // far past the floor
            GearItemModel item = LootRoller.Roll(table, items, random, ref pity);
            Assert.AreEqual("sword_common", item.Id);
        }

        [Test]
        public void EmptyTable_Throws()
        {
            int pity = 0;
            Assert.Throws<ArgumentException>(() =>
                LootRoller.Roll(new LootTableModel(null, 0), Items(), new Random(1), ref pity));
        }
    }
}
