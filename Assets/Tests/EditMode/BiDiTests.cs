using BattleRunner.Core.Text;
using NUnit.Framework;

namespace BattleRunner.Tests
{
    /// <summary>
    /// The Hebrew reordering, which is the one piece of this localization that cannot be checked
    /// by reading the code and squinting: every case here is a string that looks plausible and is
    /// wrong in a way only a Hebrew reader would catch.
    /// </summary>
    [TestFixture]
    public class BiDiTests
    {
        private const string Shalom = "שלום";          // שלום, logical order
        private const string ShalomReversed = "םולש"; // as it must be DRAWN

        [Test]
        public void TextWithNoHebrewIsLeftExactlyAlone()
        {
            // Called on everything, so it has to cost nothing and change nothing in the other
            // two languages. Reference equality, not just value: no allocation either.
            foreach (string text in new[]
                     { "SET FORTH", "Уровень 3 — Пепельный тракт", "+3 Might", "", "12.4K" })
                Assert.AreSame(text, BiDi.Visual(text), $"\"{text}\" was touched");
            Assert.IsNull(BiDi.Visual(null));
        }

        [Test]
        public void AHebrewWordIsDrawnRightToLeft()
        {
            Assert.AreEqual(ShalomReversed, BiDi.Visual(Shalom));
        }

        [Test]
        public void ReorderingTwiceGivesTheOriginalBack()
        {
            // The operation is its own inverse for a single run, which is the cheapest possible
            // check that nothing is being dropped or duplicated along the way.
            Assert.AreEqual(Shalom, BiDi.Visual(BiDi.Visual(Shalom)));
        }

        [Test]
        public void NumbersKeepTheirOwnDirectionInsideHebrew()
        {
            // THE CASE THE WHOLE FILE TURNS ON. Digits read left to right even in a right-to-left
            // line, so "42" must survive as "42" while the words around it move.
            string logical = Shalom + " 42 world";
            string visual = BiDi.Visual(logical);

            Assert.IsTrue(visual.Contains("42"), $"the number came out reversed: \"{visual}\"");
            Assert.IsTrue(visual.Contains("world"), $"the Latin came out reversed: \"{visual}\"");
            Assert.IsTrue(visual.EndsWith(ShalomReversed),
                $"the Hebrew is not on the right-hand end: \"{visual}\"");
            Assert.AreEqual("world 42 " + ShalomReversed, visual);
        }

        [Test]
        public void ASignStaysWithItsNumber()
        {
            // StatFormat produces "+3 Might" and "-12.5% Shield". If + and - are treated as
            // neutral punctuation the reversal strands them on the wrong end — "3+" — and every
            // stat line in the game reads as nonsense in exactly one language.
            string visual = BiDi.Visual("+3 " + Shalom);
            Assert.IsTrue(visual.Contains("+3"), $"the sign left its number: \"{visual}\"");
            Assert.IsFalse(visual.Contains("3+"), $"the sign ended up behind: \"{visual}\"");

            string percent = BiDi.Visual("-12.5% " + Shalom);
            Assert.IsTrue(percent.Contains("-12.5%"), $"the percentage broke up: \"{percent}\"");
        }

        [Test]
        public void BracketsPointTheRightWay()
        {
            // Reversal turns "(" into a ")" in the wrong place unless it is swapped for its
            // partner, so a parenthesised Hebrew phrase comes out inside-out.
            string visual = BiDi.Visual("(" + Shalom + ")");
            Assert.AreEqual("(" + ShalomReversed + ")", visual);
        }

        [Test]
        public void NothingIsLostOrInvented()
        {
            // Whatever the ordering does, it is a permutation — and a bracket swap. Counting the
            // characters catches a reversal that drops one off the end of a run, which is the
            // classic off-by-one here and is invisible by eye in a script you cannot read.
            foreach (string text in new[]
                     {
                         Shalom, Shalom + " 42", "+3 " + Shalom, Shalom + " world " + Shalom,
                         Shalom + "!", "   " + Shalom + "   ", Shalom + " 1.5% " + Shalom
                     })
                Assert.AreEqual(text.Length, BiDi.Visual(text).Length,
                    $"length changed for \"{text}\"");
        }

        [Test]
        public void EachLineIsReorderedOnItsOwn()
        {
            // FOR PRE-WRAPPED TEXT. Reordering a whole paragraph and then breaking it puts the
            // break at a point that means nothing in visual order, and the result is scrambled
            // rather than re-flowed. Breaking first and reordering per line is the fix.
            string logical = Shalom + "\n" + Shalom;
            Assert.AreEqual(ShalomReversed + "\n" + ShalomReversed, BiDi.VisualLines(logical));

            // And a line that is pure Latin inside a Hebrew block stays put.
            Assert.AreEqual("SET FORTH\n" + ShalomReversed,
                BiDi.VisualLines("SET FORTH\n" + Shalom));
        }

        [Test]
        public void HasRtlOnlyFiresOnHebrew()
        {
            Assert.IsTrue(BiDi.HasRtl(Shalom));
            Assert.IsFalse(BiDi.HasRtl("Уровень 3"));
            Assert.IsFalse(BiDi.HasRtl("SET FORTH"));
            Assert.IsFalse(BiDi.HasRtl(string.Empty));
            Assert.IsFalse(BiDi.HasRtl(null));
        }

        [Test]
        public void WrappingHappensBeforeReorderingAndNotAfter()
        {
            // Four Hebrew words. Read right to left the sentence is aleph, bet, gimel, dalet.
            const string logical = "\u05D0\u05D0 \u05D1\u05D1 \u05D2\u05D2 \u05D3\u05D3";

            // A budget that fits two words a line.
            string wrapped = BiDi.WrapVisual(logical, 5);
            string[] lines = wrapped.Split('\n');
            Assert.AreEqual(2, lines.Length, "the budget should have produced two lines");

            // Line one must hold the FIRST two words and line two the last two. Reordering the
            // whole paragraph and letting a component break it afterwards puts the last two
            // words on the first line, which is the bug.
            Assert.AreEqual(BiDi.Visual("\u05D0\u05D0 \u05D1\u05D1"), lines[0]);
            Assert.AreEqual(BiDi.Visual("\u05D2\u05D2 \u05D3\u05D3"), lines[1]);

            // And the difference is real: doing it the wrong way round gives a different answer.
            Assert.AreNotEqual(wrapped, BiDi.Visual(logical).Insert(5, "\n"),
                "wrap-then-reorder and reorder-then-wrap must not agree, or the test proves nothing");
        }

        [Test]
        public void WrappingLeavesTextWithNoHebrewCompletelyAlone()
        {
            // English and Russian already wrap correctly in the component, so this must not
            // touch them — not even to normalise their spacing.
            const string english = "A long enough English sentence to exceed any small budget";
            Assert.AreEqual(english, BiDi.WrapVisual(english, 8));

            const string russian = "Достаточно длинное русское предложение чтобы превысить бюджет";
            Assert.AreEqual(russian, BiDi.WrapVisual(russian, 8));
        }

        [Test]
        public void NoLineEverExceedsTheBudgetUnlessOneWordDoes()
        {
            // The real content, at the budget the talent cells use. A line over budget is a line
            // the component will break again, which is what reordering cannot survive.
            const int budget = 42;
            foreach (LocKey key in System.Enum.GetValues(typeof(LocKey)))
            {
                string logical = Loc.In(Language.Hebrew, key);
                if (!BiDi.HasRtl(logical)) continue;
                foreach (string line in BiDi.WrapVisual(logical, budget).Split('\n'))
                {
                    if (line.Length <= budget) continue;
                    // Only forgivable when the line is a single unbreakable word.
                    Assert.IsFalse(line.Contains(" "),
                        $"{key} produced an over-budget line with a break point in it: \"{line}\"");
                }
            }
        }

        [Test]
        public void WrappingNeverLosesACharacterOfTheRealContent()
        {
            // Same guarantee as the unwrapped pass, now across the break points: the only
            // characters that may appear or vanish are the spaces turned into newlines.
            foreach (LocKey key in System.Enum.GetValues(typeof(LocKey)))
            {
                string logical = Loc.In(Language.Hebrew, key);
                if (!BiDi.HasRtl(logical)) continue;
                string wrapped = BiDi.WrapVisual(logical, 42);
                Assert.AreEqual(logical.Length, wrapped.Length, $"{key} changed length when wrapped");
                Assert.AreEqual(Count(logical, ' ') + Count(logical, '\n'),
                    Count(wrapped, ' ') + Count(wrapped, '\n'),
                    $"{key} gained or lost a separator");
            }
        }

        private static int Count(string s, char c)
        {
            int n = 0;
            foreach (char ch in s) if (ch == c) n++;
            return n;
        }

        [Test]
        public void EveryHebrewStringInTheTableSurvivesTheTrip()
        {
            // Run the real content through it. The property that must hold for all 352 entries is
            // that nothing is lost — a reordering that silently ate a character would show up
            // here rather than on a phone.
            foreach (LocKey key in System.Enum.GetValues(typeof(LocKey)))
            {
                string logical = Loc.In(Language.Hebrew, key);
                string visual = BiDi.Visual(logical);
                Assert.AreEqual(logical.Length, visual.Length, $"{key} changed length");
                foreach (char c in logical)
                    if (char.IsDigit(c))
                        Assert.IsTrue(visual.IndexOf(c) >= 0, $"{key} lost the digit {c}");
            }
        }
    }
}
