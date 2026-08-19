using System.Collections.Generic;
using BattleRunner.Core.Stats;
using NUnit.Framework;

namespace BattleRunner.Tests
{
    [TestFixture]
    public class StatSheetTests
    {
        private static readonly Dictionary<string, float> BaseStats = new Dictionary<string, float>
        {
            [StatIds.Damage] = 10f,
            [StatIds.Health] = 100f,
            [StatIds.Cooldown] = 0f
        };

        [Test]
        public void NoModifiers_ReturnsBaseValues()
        {
            var sheet = StatSheet.Resolve(BaseStats, null);
            Assert.AreEqual(10f, sheet.Get(StatIds.Damage));
            Assert.AreEqual(100f, sheet.Get(StatIds.Health));
        }

        [Test]
        public void FlatThenPercent_OrderIsEnforced()
        {
            var mods = new[]
            {
                new StatModifier(StatIds.Damage, ModifierKind.Flat, 20f),
                new StatModifier(StatIds.Damage, ModifierKind.Percent, 0.5f)
            };
            // (10 + 20) * 1.5 — NOT 10 * 1.5 + 20.
            Assert.AreEqual(45f, StatSheet.Resolve(BaseStats, mods).Get(StatIds.Damage), 1e-4f);
        }

        [Test]
        public void ModifiersOfSameKind_Stack()
        {
            var mods = new[]
            {
                new StatModifier(StatIds.Health, ModifierKind.Flat, 25f),
                new StatModifier(StatIds.Health, ModifierKind.Flat, 25f),
                new StatModifier(StatIds.Health, ModifierKind.Percent, 0.1f),
                new StatModifier(StatIds.Health, ModifierKind.Percent, 0.1f)
            };
            Assert.AreEqual((100f + 50f) * 1.2f, StatSheet.Resolve(BaseStats, mods).Get(StatIds.Health), 1e-3f);
        }

        [Test]
        public void ModifierOnStatWithoutBase_TreatsBaseAsZero()
        {
            var mods = new[] { new StatModifier("crit", ModifierKind.Flat, 5f) };
            Assert.AreEqual(5f, StatSheet.Resolve(BaseStats, mods).Get("crit"));
        }

        [Test]
        public void UnknownStat_ReadsZero()
        {
            Assert.AreEqual(0f, StatSheet.Resolve(BaseStats, null).Get("nope"));
        }

        [Test]
        public void EmptyStatId_IsIgnored()
        {
            var mods = new[] { new StatModifier(null, ModifierKind.Flat, 999f) };
            Assert.AreEqual(10f, StatSheet.Resolve(BaseStats, mods).Get(StatIds.Damage));
        }
    }
}
