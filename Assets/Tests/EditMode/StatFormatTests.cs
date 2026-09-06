using BattleRunner.Core.Stats;
using NUnit.Framework;

namespace BattleRunner.Tests
{
    [TestFixture]
    public class StatFormatTests
    {
        // --- The matrix, which is the whole point ---------------------------------------
        //
        // Two INDEPENDENT reasons to print a percentage. Getting one right and the other
        // wrong is how both shipped bugs happened, in opposite directions:
        //
        //   stat is fraction | kind is Percent | correct
        //   -----------------+-----------------+-------------
        //   no               | no              | +2 Might      <- plain
        //   no               | YES             | +5% Might     <- v0.5 broke this
        //   YES              | no              | +1% Focus     <- v0.4 broke this
        //   YES              | YES             | +15% Focus

        [Test]
        public void PlainOnlyWhenTheStatIsAbsoluteAndTheKindIsFlat()
        {
            Assert.AreEqual("+2 Might", StatFormat.Affix(StatIds.Damage, ModifierKind.Flat, 2f));
            Assert.AreEqual("+15 Vigor", StatFormat.Affix(StatIds.Health, ModifierKind.Flat, 15f));
            Assert.AreEqual("+1 Shield", StatFormat.Affix(StatIds.ShieldDuration, ModifierKind.Flat, 1f));
        }

        [Test]
        public void APercentModifierOnAnAbsoluteStatIsStillAPercentage()
        {
            // Seven of the fifteen shipped items carry Percent affixes on Might or Vigor
            // (Emberbrand, Soulcleaver, Ruinblade, Shroud of Ash, Bulwark of the Fallen,
            // Eye of the Abyss, Crown of Embers). Choosing units from the STAT alone
            // printed every one of them as "+0.05 Might".
            //
            // A Percent modifier scales the resolved total — StatSheet does
            // final = (base + flat) * (1 + pct) — so it is a percentage whatever unit the
            // stat itself carries.
            Assert.AreEqual("+5% Might", StatFormat.Affix(StatIds.Damage, ModifierKind.Percent, 0.05f));
            Assert.AreEqual("+25% Might", StatFormat.Affix(StatIds.Damage, ModifierKind.Percent, 0.25f));
            Assert.AreEqual("+10% Vigor", StatFormat.Affix(StatIds.Health, ModifierKind.Percent, 0.10f));
        }

        [Test]
        public void AFlatModifierOnAFractionStatIsAPercentage()
        {
            // Shipped as "+0.01 Focus" on the loot card: the Ember Talisman's affix is
            // ModifierKind.Flat, and choosing units from the KIND alone got it wrong.
            Assert.AreEqual("+1% Focus", StatFormat.Affix(StatIds.Cooldown, ModifierKind.Flat, 0.01f));
            Assert.AreEqual("+8% Focus", StatFormat.Affix(StatIds.Cooldown, ModifierKind.Flat, 0.08f));
            Assert.AreEqual("+40% Spell", StatFormat.Affix(StatIds.SpellPower, ModifierKind.Percent, 0.40f));
            Assert.AreEqual("+12% Gates", StatFormat.Affix(StatIds.GateYield, ModifierKind.Flat, 0.12f));
        }

        [Test]
        public void TheOverloadAgreesWithTheExplicitForm()
        {
            var m = new StatModifier(StatIds.Damage, ModifierKind.Percent, 0.05f);
            Assert.AreEqual(StatFormat.Affix(m.StatId, m.Kind, m.Value), StatFormat.Affix(m));
        }

        // --- Zero -----------------------------------------------------------------------

        [Test]
        public void ZeroNeverPrintsAsNegativeZero()
        {
            // Shipped as "Focus -0 %" on the menu: a hard-coded minus in front of a base
            // Cooldown of exactly 0, compounded by .NET rendering negative zero as "-0".
            foreach (string id in StatIds.All)
            {
                foreach (float v in new[] { 0f, -0f, -1e-5f, 1e-5f, -0.0001f })
                {
                    StringAssert.DoesNotContain("-0", StatFormat.Total(id, v), $"Total({id}, {v})");
                    StringAssert.DoesNotContain("-0", StatFormat.Affix(id, ModifierKind.Flat, v));
                    StringAssert.DoesNotContain("-0", StatFormat.Affix(id, ModifierKind.Percent, v));
                }
            }

            Assert.AreEqual("Focus 0%", StatFormat.Total(StatIds.Cooldown, 0f));
            Assert.AreEqual("Might 0", StatFormat.Total(StatIds.Damage, 0f));
        }

        [Test]
        public void ACooldownReductionReadsAsAGainNotAPenalty()
        {
            // Cooldown is stored POSITIVE as a fractional reduction: both SpellSystem and
            // ShieldSystem compute 1 - min(0.6, Cooldown). Higher is better. The old line
            // printed a 12% bonus as "Focus -12 %", next to "Might 10", reading as a
            // penalty for a talent the tree describes as a benefit.
            Assert.AreEqual("Focus 12%", StatFormat.Total(StatIds.Cooldown, 0.12f));
            StringAssert.DoesNotContain("-", StatFormat.Total(StatIds.Cooldown, 0.12f));
        }

        [Test]
        public void TheVisibilityGateAgreesWithWhatIsPrinted()
        {
            // A caller's own epsilon would drift from the formatter's. Every value that
            // ShowsAsNonZero admits must actually render as non-zero, and vice versa.
            foreach (string id in new[] { StatIds.GateYield, StatIds.ShieldDuration, StatIds.Damage })
            {
                foreach (float v in new[] { 0f, 1e-4f, 4.9e-4f, 5e-4f, 0.02f, 0.049f, 0.05f, 0.051f, 0.12f, 1f })
                {
                    bool shown = StatFormat.ShowsAsNonZero(id, v);
                    string rendered = StatFormat.Total(id, v);
                    bool rendersZero = rendered.EndsWith(" 0%") || rendered.EndsWith(" 0");
                    Assert.AreNotEqual(shown, rendersZero, $"{id} at {v} rendered '{rendered}'");
                }
            }
        }

        // --- Units and names --------------------------------------------------------------

        [Test]
        public void FractionStatsScaleToPercent()
        {
            Assert.AreEqual("Gates 12%", StatFormat.Total(StatIds.GateYield, 0.12f));
            Assert.AreEqual("Speed 12%", StatFormat.Total(StatIds.RunSpeed, 0.12f));
            Assert.AreEqual("Resist 35%", StatFormat.Total(StatIds.EnemyResist, 0.35f));
            Assert.AreEqual("Fortune 30%", StatFormat.Total(StatIds.Fortune, 0.30f));
            Assert.AreEqual("Might 10", StatFormat.Total(StatIds.Damage, 10f));
            Assert.AreEqual("Vigor 100", StatFormat.Total(StatIds.Health, 100f));
        }

        [Test]
        public void ShieldDurationIsSecondsNotAPercentage()
        {
            // The one run-axis stat that is NOT a fraction: StatIds documents it as
            // "Extra seconds a raised shield holds". Bulwark grants +1s, not +100%.
            Assert.IsFalse(StatFormat.IsFraction(StatIds.ShieldDuration));
            Assert.AreEqual("Shield 1", StatFormat.Total(StatIds.ShieldDuration, 1f));
            Assert.AreEqual("+1 Shield", StatFormat.Affix(StatIds.ShieldDuration, ModifierKind.Flat, 1f));
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
            Assert.AreEqual("-2 Might", StatFormat.Affix(StatIds.Damage, ModifierKind.Flat, -2f));
            Assert.AreEqual("-5% Focus", StatFormat.Affix(StatIds.Cooldown, ModifierKind.Flat, -0.05f));
            Assert.AreEqual("-5% Might", StatFormat.Affix(StatIds.Damage, ModifierKind.Percent, -0.05f));
        }
    }
}
