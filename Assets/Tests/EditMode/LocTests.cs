using System;
using System.Collections.Generic;
using System.Linq;
using BattleRunner.Core.Text;
using NUnit.Framework;

namespace BattleRunner.Tests
{
    /// <summary>
    /// The string table, and the reason it can be trusted.
    ///
    /// Translations are the one kind of content where a mistake is invisible to whoever made it:
    /// a missing Hebrew entry, a dropped {0}, a mistyped percentage in a talent description — all
    /// of them compile, render, and look fine to someone who does not read that language. These
    /// tests are what stands in for a reader.
    /// </summary>
    [TestFixture]
    public class LocTests
    {
        private static readonly Language[] All =
            { Language.English, Language.Russian, Language.Hebrew };

        private static IEnumerable<LocKey> Keys() =>
            Enum.GetValues(typeof(LocKey)).Cast<LocKey>();

        private static (LocKey, string)[] Table(Language language) =>
            language == Language.Russian ? LocTables.Russian
            : language == Language.Hebrew ? LocTables.Hebrew
            : LocTables.English;

        [Test]
        public void EveryLanguageCarriesEveryKeyExactlyOnce()
        {
            // THE ONE THAT MAKES THE REST MEANINGFUL. A key absent from a table reads as an empty
            // label on a screen in a language the author does not speak; a key present twice
            // means one of the two is dead and nobody knows which.
            foreach (Language language in All)
            {
                var seen = new HashSet<LocKey>();
                foreach ((LocKey key, string _) in Table(language))
                    Assert.IsTrue(seen.Add(key), $"{language} lists {key} more than once");

                foreach (LocKey key in Keys())
                    Assert.IsTrue(seen.Contains(key), $"{language} is missing {key}");
            }
        }

        [Test]
        public void NothingIsBlank()
        {
            foreach (Language language in All)
                foreach (LocKey key in Keys())
                {
                    string text = Loc.In(language, key);
                    Assert.IsFalse(string.IsNullOrWhiteSpace(text),
                        $"{language} {key} is blank");
                    Assert.AreEqual(text, text.Trim(),
                        $"{language} {key} has leading or trailing space, which shows up as a "
                        + "label that looks mis-centred");
                }
        }

        [Test]
        public void EveryPlaceholderSurvivesTranslation()
        {
            // A dropped {0} does not throw — string.Format simply produces a sentence with the
            // number missing, which reads as a bug in the game rather than in the table. An
            // INVENTED {1} does throw, at the worst possible moment.
            foreach (LocKey key in Keys())
            {
                var expected = Placeholders(Loc.In(Language.English, key));
                foreach (Language language in All)
                {
                    var got = Placeholders(Loc.In(language, key));
                    CollectionAssert.AreEquivalent(expected, got,
                        $"{language} {key}: placeholders differ from the English source");
                }
            }
        }

        [Test]
        public void TheNumbersInATranslationAreTheNumbersInTheSource()
        {
            // THE GUARD FOR THE TALENT TREE. Around 158 talent descriptions quote a balance value
            // that also exists as a StatModifier somewhere else — "Packs cost 4% less" beside a
            // -0.04. A translation that typed 5 would state a rule the game does not follow, and
            // would be wrong only to the people who can read it.
            //
            // Digits are compared as a multiset rather than in order, because word order moves:
            // Hebrew may well put the number somewhere English does not.
            foreach (LocKey key in Keys())
            {
                List<string> expected = Numbers(Loc.In(Language.English, key));
                foreach (Language language in All)
                {
                    List<string> got = Numbers(Loc.In(language, key));
                    CollectionAssert.AreEquivalent(expected, got,
                        $"{language} {key}: the numbers do not match the English source "
                        + $"(\"{Loc.In(Language.English, key)}\" vs \"{Loc.In(language, key)}\")");
                }
            }
        }

        [Test]
        public void EveryCountedStringHasAFormForEveryCount()
        {
            // Russian picks between three forms on the last two digits; English and Hebrew use
            // two. An entry with the wrong number of variants silently reuses the last one, which
            // is grammatical nonsense for exactly the counts nobody tests by hand — 21, 111.
            foreach (LocKey key in Keys())
            {
                string english = Loc.In(Language.English, key);
                if (!english.Contains(Loc.FormSeparator)) continue;

                foreach (Language language in All)
                {
                    int forms = Loc.In(language, key).Split(Loc.FormSeparator).Length;
                    Assert.AreEqual(Plural.FormCount(language), forms,
                        $"{language} {key} has {forms} plural forms, needs "
                        + $"{Plural.FormCount(language)}");
                }
            }
        }

        [Test]
        public void HebrewCarriesNoCombiningMarks()
        {
            // Nikud are combining marks, and reordering a string for display puts a combining
            // mark before the letter it belongs to — the rendering breaks in a way that looks
            // like a font problem rather than a data one. Modern Hebrew interfaces do not use
            // them, so the simplest guarantee is that none are ever authored.
            foreach (LocKey key in Keys())
                foreach (char c in Loc.In(Language.Hebrew, key))
                    Assert.IsFalse(c >= '֑' && c <= 'ׇ',
                        $"Hebrew {key} contains combining mark U+{(int)c:X4}");
        }

        [Test]
        public void TheLanguageNamesAreNeverTranslated()
        {
            // The control that changes language has to be legible to someone who cannot read the
            // language currently on screen, so each option names itself in itself.
            Assert.AreEqual("ENGLISH", Languages.NativeName(Language.English));
            Assert.AreEqual("РУССКИЙ", Languages.NativeName(Language.Russian));
            Assert.AreEqual("עברית", Languages.NativeName(Language.Hebrew));
        }

        [Test]
        public void CyclingReachesEveryLanguageAndComesBack()
        {
            Language at = Language.English;
            var seen = new HashSet<Language>();
            for (int i = 0; i < Languages.Count; i++)
            {
                seen.Add(at);
                at = Languages.Next(at);
            }
            Assert.AreEqual(Languages.Count, seen.Count, "the cycle does not reach every language");
            Assert.AreEqual(Language.English, at, "the cycle does not come back round");
        }

        [Test]
        public void OnlyHebrewReadsRightToLeft()
        {
            Assert.IsTrue(Languages.IsRightToLeft(Language.Hebrew));
            Assert.IsFalse(Languages.IsRightToLeft(Language.English));
            Assert.IsFalse(Languages.IsRightToLeft(Language.Russian));
        }

        [Test]
        public void ANonsenseSavedLanguageFallsBackToEnglish()
        {
            Assert.AreEqual(Language.English, Languages.FromSaved(-1));
            Assert.AreEqual(Language.English, Languages.FromSaved(99));
            Assert.AreEqual(Language.Hebrew, Languages.FromSaved(2));
        }

        // ===================================================================

        private static List<string> Placeholders(string text)
        {
            var found = new List<string>();
            for (int i = 0; i < text.Length - 1; i++)
            {
                if (text[i] != '{') continue;
                int close = text.IndexOf('}', i);
                if (close < 0) continue;
                found.Add(text.Substring(i, close - i + 1));
                i = close;
            }
            // Distinct: a plural entry repeats {0} once per variant, and how many variants a
            // language has is a different test's business.
            return found.Distinct().OrderBy(s => s, StringComparer.Ordinal).ToList();
        }

        private static List<string> Numbers(string text)
        {
            var found = new List<string>();
            int i = 0;
            while (i < text.Length)
            {
                if (text[i] == '{')
                {
                    // A placeholder's index is not a number the player reads.
                    int close = text.IndexOf('}', i);
                    i = close < 0 ? text.Length : close + 1;
                    continue;
                }
                if (!char.IsDigit(text[i])) { i++; continue; }

                int start = i;
                while (i < text.Length && (char.IsDigit(text[i])
                       || (text[i] == '.' && i + 1 < text.Length && char.IsDigit(text[i + 1]))))
                    i++;
                found.Add(text.Substring(start, i - start));
            }
            return found.OrderBy(s => s, StringComparer.Ordinal).ToList();
        }
    }
}
