using BattleRunner.Core.Stats;
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
        public void ASignedStatStaysWithItsNumberInHebrew()
        {
            // "+3 עוצמה" is the shape StatFormat.Affix produces on every gear item and every
            // talent detail line, so it is the single most-printed composition in the game. The
            // sign is a UAX #9 ES character and belongs to the number beside it; treated as a
            // plain neutral it drifts to the other end and "+3" comes out "3+", which reads as a
            // different quantity rather than as a rendering fault.
            Loc.Use(Language.Hebrew);
            try
            {
                foreach (string statId in new[] { StatIds.Damage, StatIds.Health, StatIds.Fortune })
                    foreach (float value in new[] { 3f, -3f, 12.5f })
                    {
                        string affix = StatFormat.Affix(statId, ModifierKind.Flat, value);
                        string visual = BiDi.Visual(affix);

                        Assert.AreEqual(affix.Length, visual.Length, $"{statId} {value}: length changed");

                        // Whatever else moves, the sign keeps its digit's company.
                        char sign = value < 0f ? '-' : '+';
                        int at = visual.IndexOf(sign);
                        Assert.GreaterOrEqual(at, 0, $"{statId} {value}: the sign vanished from \"{visual}\"");
                        Assert.Less(at + 1, visual.Length,
                            $"{statId} {value}: the sign ended up last in \"{visual}\"");
                        Assert.IsTrue(char.IsDigit(visual[at + 1]),
                            $"{statId} {value}: the sign parted from its number in \"{visual}\"");
                    }
            }
            finally
            {
                Loc.Use(Language.English);
            }
        }

        [Test]
        public void EveryTemplateSurvivesBeingFilledAndReordered()
        {
            // THE BLIND SPOT THE TABLE TESTS HAVE BY CONSTRUCTION. They read entries; the player
            // reads what Loc.Format makes out of them. A template is correct and its fragments
            // are correct and the composed string can still be wrong -- that is exactly how the
            // loot card's "<size=30>" got reordered into "<30=size>" with every table test green.
            //
            // So: fill every template that takes arguments, with plausible arguments of the kind
            // the game passes, and put the result through the display path.
            string[] fill =
            {
                "\u05E7\u05E1\u05D3\u05D4",   // a Hebrew word, as a name or a place
                "\u05E0\u05D3\u05D9\u05E8",   // another, as a rarity or a rank
                "\u05E8\u05D0\u05E9",          // a third, as a slot
                "12",                             // a number, which reorders differently again
            };

            int checked_ = 0;
            foreach (LocKey key in System.Enum.GetValues(typeof(LocKey)))
            {
                string template = Loc.In(Language.Hebrew, key);
                int slots = SlotCount(template);
                if (slots == 0) continue;
                // Plural entries are picked between by Loc.Count, not formatted whole.
                if (template.IndexOf(Loc.FormSeparator) >= 0) continue;

                var args = new object[slots];
                for (int i = 0; i < slots; i++) args[i] = fill[i % fill.Length];

                string composed;
                try { composed = string.Format(template, args); }
                catch (System.FormatException e)
                {
                    Assert.Fail($"{key} is not a usable template: {e.Message}");
                    return;
                }

                string visual = BiDi.VisualLines(composed);
                checked_++;

                Assert.AreEqual(composed.Length, visual.Length,
                    $"{key} changed length once composed: \"{visual}\"");
                Assert.IsFalse(visual.Contains("{") || visual.Contains("}"),
                    $"{key} left a placeholder brace in the output: \"{visual}\"");

                // Any markup in the composed string has to come out as markup.
                Assert.AreEqual(Count(composed, '<'), Count(visual, '<'),
                    $"{key} gained or lost an angle bracket: \"{visual}\"");
                foreach (string tag in Tags(composed))
                    Assert.IsTrue(visual.Contains(tag),
                        $"{key} mangled the tag {tag}: \"{visual}\"");

                // Every digit that went in comes back out.
                foreach (char c in composed)
                    if (char.IsDigit(c))
                        Assert.IsTrue(visual.IndexOf(c) >= 0, $"{key} lost the digit {c}");
            }

            Assert.Greater(checked_, 0, "no templates were exercised, so this test proves nothing");
        }

        private static int SlotCount(string template)
        {
            int highest = -1;
            for (int i = 0; i + 2 < template.Length; i++)
                if (template[i] == '{' && char.IsDigit(template[i + 1]) && template[i + 2] == '}')
                {
                    int index = template[i + 1] - '0';
                    if (index > highest) highest = index;
                }
            return highest + 1;
        }

        private static System.Collections.Generic.List<string> Tags(string s)
        {
            var found = new System.Collections.Generic.List<string>();
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] != '<') continue;
                int close = s.IndexOf('>', i + 1);
                if (close > i && close - i <= 40) { found.Add(s.Substring(i, close - i + 1)); i = close; }
            }
            return found;
        }

        [Test]
        public void RichTextTagsComeOutTheSameWayTheyWentIn()
        {
            // uGUI markup went through the reordering like any other text, and the angle
            // brackets mirror like any other pair: "<i>...</i>" came back "</i>...<i>" and
            // "<size=30>" came back "<30=size>", which Unity draws as literal characters. The
            // loot card is the only screen in the game carrying markup, and it carried both.
            const string open = "<size=30>";
            const string close = "</size>";
            string logical = open + "\u05E0\u05D3\u05D9\u05E8 \u05E7\u05E1\u05D3\u05D4" + close;
            string visual = BiDi.Visual(logical);

            Assert.IsTrue(visual.StartsWith(open), $"opening tag did not survive: {visual}");
            Assert.IsTrue(visual.EndsWith(close), $"closing tag did not survive: {visual}");
            Assert.AreEqual(logical.Length, visual.Length);

            // And the Hebrew between them is genuinely reordered -- the tags being intact must
            // not have come at the price of doing nothing at all.
            Assert.AreNotEqual(logical, visual);
        }

        [Test]
        public void TheLootItemLineSurvivesBeingComposedAndReordered()
        {
            // The real template, filled the way LootScreen fills it. This is the one string in
            // the game where a format template, three translated fragments and rich-text markup
            // all meet, and it is composed at runtime so no table test ever sees the result.
            string line = Loc.In(Language.Hebrew, LocKey.LootItemLine);
            string composed = string.Format(line,
                "\u05E7\u05E1\u05D3\u05D4", "\u05E0\u05D3\u05D9\u05E8", "\u05E8\u05D0\u05E9");
            string visual = BiDi.VisualLines(composed);

            Assert.IsTrue(visual.Contains("<size=30>"), $"opening tag mangled: {visual}");
            Assert.IsTrue(visual.Contains("</size>"), $"closing tag mangled: {visual}");
            Assert.IsFalse(visual.Contains("<30=size>"), "the tag was reordered inside out");
            Assert.AreEqual(composed.Length, visual.Length);
        }

        [Test]
        public void AComparisonSignIsStillTextRatherThanATag()
        {
            // A lone '<' with no partner is ordinary text and must keep mirroring like the
            // paired character it is -- the tag handling must not swallow it.
            string visual = BiDi.Visual("5 < 7 \u05D1\u05E2\u05D1\u05E8\u05D9\u05EA");
            Assert.IsTrue(visual.Contains(">"), $"the comparison did not mirror: {visual}");
            Assert.IsFalse(visual.Contains("<"), $"the comparison mirrored to both: {visual}");
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
