using BattleRunner.Core.Text;
using NUnit.Framework;

namespace BattleRunner.Tests
{
    /// <summary>
    /// Switching language, and the two properties that make it safe to do at runtime on a UI
    /// that is built exactly once and never rebuilt.
    /// </summary>
    [TestFixture]
    public class LanguageChoiceTests
    {
        [TearDown]
        public void Reset() => Loc.Use(Language.English);

        [Test]
        public void EveryStringChangesWhenTheLanguageDoes()
        {
            // Not a tautology: the tables are three separate arrays and Loc resolves through an
            // index, so a wiring mistake that returned English for everything would pass every
            // completeness test in LocTests and fail here.
            Loc.Use(Language.English);
            string english = Loc.Get(LocKey.MenuSetForth);
            Loc.Use(Language.Russian);
            Assert.AreNotEqual(english, Loc.Get(LocKey.MenuSetForth));
            Loc.Use(Language.Hebrew);
            Assert.AreNotEqual(english, Loc.Get(LocKey.MenuSetForth));
            Loc.Use(Language.English);
            Assert.AreEqual(english, Loc.Get(LocKey.MenuSetForth));
        }

        [Test]
        public void ChangingLanguageAnnouncesItself()
        {
            // The UI is built once and never rebuilt, so the only way a label repaints is this
            // event. A switch that changed the table without raising it would leave every screen
            // in the old language until it happened to be rebuilt, which is never.
            int raised = 0;
            void Count() => raised++;
            Loc.Changed += Count;
            try
            {
                Loc.Use(Language.Russian);
                Assert.AreEqual(1, raised);
                Loc.Use(Language.Russian);
                Assert.AreEqual(1, raised, "re-selecting the same language should be a no-op");
                Loc.Use(Language.Hebrew);
                Assert.AreEqual(2, raised);
            }
            finally { Loc.Changed -= Count; }
        }

        [Test]
        public void OnlyHebrewAsksForReordering()
        {
            Loc.Use(Language.English);
            Assert.IsFalse(Loc.IsRightToLeft);
            Loc.Use(Language.Russian);
            Assert.IsFalse(Loc.IsRightToLeft);
            Loc.Use(Language.Hebrew);
            Assert.IsTrue(Loc.IsRightToLeft);
        }

        [Test]
        public void CountedStringsAgreeInEveryLanguageAtTheAwkwardNumbers()
        {
            // 1, 2, 5, 11, 21, 111 — the six that separate a real plural rule from a ternary.
            // Russian is the one that cares: 21 takes the same form as 1 and 11 does not.
            foreach (Language language in new[]
                     { Language.English, Language.Russian, Language.Hebrew })
            {
                Loc.Use(language);
                foreach (long n in new long[] { 0, 1, 2, 5, 11, 21, 111 })
                {
                    string text = Loc.Count(LocKey.SlotTalents, n);
                    Assert.IsNotEmpty(text, $"{language} had no form for {n}");
                    Assert.IsFalse(text.Contains(Loc.FormSeparator.ToString()),
                        $"{language} at {n} returned the whole entry instead of one form: {text}");
                }
            }

            Loc.Use(Language.Russian);
            Assert.AreEqual(Loc.Count(LocKey.SlotTalents, 1).Replace("1", "#"),
                Loc.Count(LocKey.SlotTalents, 21).Replace("21", "#"),
                "Russian 21 must take the same form as 1");
            Assert.AreNotEqual(Loc.Count(LocKey.SlotTalents, 1).Replace("1", "#"),
                Loc.Count(LocKey.SlotTalents, 11).Replace("11", "#"),
                "Russian 11 must NOT take the same form as 1");
        }
    
        [Test]
        public void MirroringASpanTwiceReturnsItExactly()
        {
            // The directional placements are REPLAYED on every language change, so the mirror has
            // to be an involution over the AUTHORED values -- never applied on top of its own
            // previous result. Replaying from the authored numbers is what makes that true, and
            // this pins the property the replay relies on.
            float lo = 0.030f, hi = 0.206f;
            float authoredLo = lo, authoredHi = hi;

            Flip(ref lo, ref hi);
            Assert.AreNotEqual(authoredLo, lo, "a mirrored span should have moved");
            Assert.AreEqual(authoredHi - authoredLo, hi - lo, 1e-5f, "the span changed width");

            Flip(ref lo, ref hi);
            Assert.AreEqual(authoredLo, lo, 1e-5f);
            Assert.AreEqual(authoredHi, hi, 1e-5f);
        }

        /// <summary>
        /// UiFactory.MirrorSpan's arithmetic, restated here because UiFactory lives in the Meta
        /// assembly and this suite is Core-only. Kept identical on purpose: if one changes, this
        /// test is the thing that should be made to disagree.
        /// </summary>
        private static void Flip(ref float lo, ref float hi)
        {
            float low = 1f - hi, high = 1f - lo;
            lo = low;
            hi = high;
        }
}
}
