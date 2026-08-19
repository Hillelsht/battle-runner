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
    }
}
