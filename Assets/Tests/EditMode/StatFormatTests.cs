using BattleRunner.Core.Stats;
using NUnit.Framework;

namespace BattleRunner.Tests
{
    [TestFixture]
    public class StatFormatTests
    {
        // --- The two bugs seen on device ------------------------------------------------

        [Test]
        public void AFlatModifierOnAFractionStatPrintsAsAPercentage()
        {
            // Shipped in v0.4.0 as "+0.01 Focus" on the loot card. The Ember Talisman's
            // affix is ModifierKind.Flat with value 0.01, and the old formatter chose its
            // units from the KIND. Kind is about composition, not units.
            Assert.AreEqual("+1% Focus", StatFormat.Affix(StatIds.Cooldown, 0.01f));
            Assert.AreEqual("+5% Focus", StatFormat.Affix(StatIds.Cooldown, 0.05f));
            Assert.AreEqual("+40% Spell", StatFormat.Affix(StatIds.SpellPower, 0.40f));
        }

        [Test]
        public void ZeroNeverPrintsAsNegativeZero()
        {
            // Shipped as "Focus -0 %" on the main menu: a hard-coded minus in front of a
            // base Cooldown of zero.
            Assert.AreEqual("Focus 0%", StatFormat.Total(StatIds.Cooldown, 0f));
            Assert.AreEqual("Focus 0%", StatFormat.Total(StatIds.Cooldown, -0f));
            Assert.AreEqual("Might 0", StatFormat.Total(StatIds.Damage, 0f));

            // A value too small to display must round to a clean zero, not to "-0".
            Assert.AreEqual("Focus 0%", StatFormat.Total(StatIds.Cooldown, -0.0001f));
            Assert.AreEqual("+0% Focus", StatFormat.Affix(StatIds.Cooldown, -0.0001f));
        }

        // --- The rule itself --------------------------------------------------------------

        [Test]
        public void AbsoluteStatsKeepTheirUnits()
        {
            Assert.AreEqual("+2 Might", StatFormat.Affix(StatIds.Damage, 2f));
            Assert.AreEqual("+15 Vigor", StatFormat.Affix(StatIds.Health, 15f));
            Assert.AreEqual("+1 Shield", StatFormat.Affix(StatIds.ShieldDuration, 1f));
            Assert.AreEqual("Might 10", StatFormat.Total(StatIds.Damage, 10f));
            Assert.AreEqual("Vigor 100", StatFormat.Total(StatIds.Health, 100f));
        }

        [Test]
        public void FractionStatsScaleToPercent()
        {
            Assert.AreEqual("Gates 12%", StatFormat.Total(StatIds.GateYield, 0.12f));
            Assert.AreEqual("Speed 12%", StatFormat.Total(StatIds.RunSpeed, 0.12f));
            Assert.AreEqual("Resist 35%", StatFormat.Total(StatIds.EnemyResist, 0.35f));
            Assert.AreEqual("Fortune 30%", StatFormat.Total(StatIds.Fortune, 0.30f));
        }

        [Test]
        public void ShieldDurationIsSecondsNotAPercentage()
        {
            // The one run-axis stat that is NOT a fraction: StatIds documents it as
            // "Extra seconds a raised shield holds". Bramble grants +1s, not +100%.
            Assert.IsFalse(StatFormat.IsFraction(StatIds.ShieldDuration));
            Assert.AreEqual("Shield 1", StatFormat.Total(StatIds.ShieldDuration, 1f));
        }

        [Test]
        public void EveryKnownStatHasARealName()
        {
            foreach (string id in StatIds.All)
            {
                string name = StatFormat.DisplayName(id);
                Assert.IsNotEmpty(name, $"{id} has no display name");
                Assert.AreNotEqual(id, name, $"{id} falls through to its raw id");
            }
        }

        [Test]
        public void AnUnknownStatDegradesInsteadOfThrowing()
        {
            // Junk in a save must render as something, not crash the loot screen.
            Assert.AreEqual("not_a_stat", StatFormat.DisplayName("not_a_stat"));
            Assert.IsFalse(StatFormat.IsFraction("not_a_stat"));
            Assert.AreEqual("?", StatFormat.DisplayName(null));
        }

        [Test]
        public void NegativeAffixesKeepTheirSign()
        {
            Assert.AreEqual("-2 Might", StatFormat.Affix(StatIds.Damage, -2f));
            Assert.AreEqual("-5% Focus", StatFormat.Affix(StatIds.Cooldown, -0.05f));
        }
    }
}
