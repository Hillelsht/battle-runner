namespace BattleRunner.Core.Text
{
    /// <summary>
    /// Which grammatical form a count takes.
    ///
    /// WHY THIS IS NOT `n == 1 ? "" : "s"`. That is what the four counted strings in this game did
    /// — "1 point to spend" against "4 points to spend" — and it is a rule about English that
    /// happens to be spelled as if it were a rule about counting. Russian has three forms and
    /// picks between them on the LAST TWO digits, so 21 takes the same form as 1, 22 takes the
    /// same as 2, and 11 through 14 take the many-form despite ending in 1 through 4. A ternary
    /// gets 21 and 111 wrong, which are numbers this game shows.
    ///
    /// Three forms is all any of the three languages needs, so the return is an index into up to
    /// three authored variants rather than a CLDR category name. Callers never see the index:
    /// Loc.Count picks the variant.
    /// </summary>
    public static class Plural
    {
        /// <summary>The most variants any language here needs.</summary>
        public const int MaxForms = 3;

        /// <summary>
        /// Which variant <paramref name="count"/> takes, 0 to 2.
        ///
        /// English and Hebrew use two: one, and everything else. Hebrew has a dual for a handful
        /// of nouns but nothing this game counts is one of them, and using it where it is not
        /// idiomatic reads worse than not using it at all.
        ///
        /// Russian uses three, chosen on the last two digits:
        ///   0  one          1, 21, 31 … but not 11
        ///   1  few          2-4, 22-24 … but not 12-14
        ///   2  many         0, 5-20, 25-30 …
        /// </summary>
        public static int Form(Language language, long count)
        {
            long n = count < 0 ? -count : count;

            if (language != Language.Russian)
                return n == 1 ? 0 : 1;

            long lastTwo = n % 100;
            // The teens are the exception the ternary always misses: 11, 12, 13 and 14 all take
            // the many-form however their last digit reads.
            if (lastTwo >= 11 && lastTwo <= 14) return 2;

            long last = n % 10;
            if (last == 1) return 0;
            if (last >= 2 && last <= 4) return 1;
            return 2;
        }

        /// <summary>How many variants a language actually authors.</summary>
        public static int FormCount(Language language) =>
            language == Language.Russian ? 3 : 2;
    }
}
