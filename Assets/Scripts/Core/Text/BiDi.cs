namespace BattleRunner.Core.Text
{
    /// <summary>
    /// Hebrew, reordered so a component that knows nothing about direction draws it correctly.
    ///
    /// WHY THIS HAS TO EXIST AT ALL. The whole UI is legacy `UnityEngine.UI.Text`, which performs
    /// **no bidirectional reordering whatsoever** — it has no `isRightToLeft`, no bidi pass, and
    /// no notion that a script might run the other way. Hand it Hebrew in logical order and it
    /// lays the characters out left to right exactly as given, which reads backwards. Nothing in
    /// Unity does this for us at this layer, so it is done here.
    ///
    /// THE ALGORITHM, WORKED THROUGH. A simplified UAX #9: reverse the whole string, then
    /// re-reverse each run that is not right-to-left.
    ///
    ///     logical   "שלום 42 world"
    ///     reversed  "dlrow 24 םולש"
    ///     runs back "world 42 םולש"    ← what the eye should see, left to right
    ///
    /// The Hebrew word ends up rightmost and reads right-to-left; the number and the Latin word
    /// sit to its left, each still readable. That is correct for a right-to-left paragraph.
    ///
    /// IT IS SAFE HERE ONLY BECAUSE NOTHING WRAPS. `UiFactory.Label` sets
    /// `horizontalOverflow = Overflow`, so Unity never re-breaks a line — which matters enormously,
    /// because visual-order text broken at a new point is scrambled, not merely re-flowed. The two
    /// labels in the game that DO wrap must be pre-wrapped and passed through
    /// <see cref="VisualLines"/> line by line.
    ///
    /// AND WHY THE TABLE IS NOT STORED THIS WAY. Reordering belongs at the very last moment
    /// before drawing. Stored reversed, `Loc.Format` would compose already-reversed fragments and
    /// produce confident garbage.
    /// </summary>
    public static class BiDi
    {
        /// <summary>
        /// The character's direction class, reduced to the three that matter here.
        /// </summary>
        private enum Kind
        {
            /// <summary>Hebrew, and the punctuation that belongs to it.</summary>
            Rtl,
            /// <summary>Latin, Cyrillic, digits — anything that reads left to right.</summary>
            Ltr,
            /// <summary>Spaces and punctuation, which take their side from their neighbours.</summary>
            Neutral
        }

        /// <summary>
        /// A string in logical order, returned in the order it should be DRAWN.
        ///
        /// A no-op for text with no right-to-left character in it, so it is safe to call on
        /// everything and costs nothing in English or Russian.
        /// </summary>
        public static string Visual(string logical)
        {
            if (string.IsNullOrEmpty(logical) || !HasRtl(logical)) return logical;

            // RICH-TEXT TAGS ARE NOT TEXT and reordering them destroys them. Unity's uGUI markup
            // goes through this the same as anything else, and the result is not subtly wrong:
            //
            //     <i>שלל כפול</i>   became   </i>לופכ ללש<i>
            //     <size=30>...      became   <30=size>...
            //
            // The angle brackets mirror like any other pair and the run reverses, so the tag
            // comes out inside out. Unity then draws "<30=size>" as literal characters, and the
            // loot card -- the only screen in the game with markup on it -- shows the player a
            // mangled tag instead of the size change it asked for.
            //
            // So the tags come out, the text is reordered without them, and they go back at the
            // same offsets into the text. Every tag in this game wraps a whole line, which is the
            // case that makes exact: an opening tag at offset 0 returns to offset 0 and its
            // closing partner to the end. A tag in mid-line would land approximately, which is
            // worth far less than the guarantee that the markup always comes out valid.
            return Tagged(logical) ? WithTagsKept(logical) : Reorder(logical);
        }

        /// <summary>Is there anything that looks like a uGUI rich-text tag in here?</summary>
        private static bool Tagged(string s)
        {
            for (int i = 0; i < s.Length; i++)
                if (s[i] == '<' && TagEnd(s, i) > i) return true;
            return false;
        }

        /// <summary>
        /// The index of the '>' closing a tag that starts at <paramref name="open"/>, or -1.
        ///
        /// DELIBERATELY STRICTER THAN "a '&lt;' with a '&gt;' after it somewhere", because that
        /// description also fits `5 &lt; 7 &gt; 3`, and swallowing a pair of comparisons as
        /// markup would stop them mirroring — trading the corruption this code exists to fix for
        /// a quieter one. A uGUI tag opens with a letter or a slash and never contains
        /// whitespace, which separates the two cases completely.
        /// </summary>
        private static int TagEnd(string s, int open)
        {
            if (open + 1 >= s.Length) return -1;
            char first = s[open + 1];
            if (first != '/' && !((first >= 'a' && first <= 'z') || (first >= 'A' && first <= 'Z')))
                return -1;

            int limit = open + MaxTagLength;
            if (limit >= s.Length) limit = s.Length - 1;
            for (int i = open + 1; i <= limit; i++)
            {
                char c = s[i];
                if (c == '>') return i;
                if (c == '<' || char.IsWhiteSpace(c)) return -1;
            }
            return -1;
        }

        /// <summary>
        /// Reorder the text with the tags lifted out, then put them back where they were.
        /// </summary>
        private static string WithTagsKept(string logical)
        {
            var tagAt = new System.Collections.Generic.List<int>();
            var tags = new System.Collections.Generic.List<string>();
            var bare = new System.Text.StringBuilder(logical.Length);

            for (int i = 0; i < logical.Length; )
            {
                if (logical[i] == '<')
                {
                    // Anything that is not a tag by TagEnd's rules is ordinary text and keeps
                    // mirroring like the paired character it is.
                    int close = TagEnd(logical, i);
                    if (close > i)
                    {
                        tagAt.Add(bare.Length);
                        tags.Add(logical.Substring(i, close - i + 1));
                        i = close + 1;
                        continue;
                    }
                }
                bare.Append(logical[i]);
                i++;
            }

            if (tags.Count == 0) return Reorder(logical);

            string visual = Reorder(bare.ToString());
            var rebuilt = new System.Text.StringBuilder(logical.Length);
            int next = 0;
            for (int t = 0; t < tags.Count; t++)
            {
                int at = tagAt[t] < visual.Length ? tagAt[t] : visual.Length;
                if (at > next) rebuilt.Append(visual, next, at - next);
                rebuilt.Append(tags[t]);
                next = at > next ? at : next;
            }
            if (next < visual.Length) rebuilt.Append(visual, next, visual.Length - next);
            return rebuilt.ToString();
        }

        /// <summary>Longest run of characters accepted as a tag, so a stray '&lt;' cannot swallow a line.</summary>
        private const int MaxTagLength = 40;

        /// <summary>The reordering itself, on text with no markup in it.</summary>
        private static string Reorder(string logical)
        {
            if (string.IsNullOrEmpty(logical) || !HasRtl(logical)) return logical;

            int n = logical.Length;
            var levels = new int[n];

            // --- 1. the strong types take their level directly -------------------------
            //
            // Level 1 is right-to-left, level 2 is a left-to-right island inside it. Digits and
            // letters both sit at 2; what separates them is resolved next.
            for (int i = 0; i < n; i++)
            {
                Kind kind = ClassOf(logical[i]);
                levels[i] = kind == Kind.Rtl ? 1 : kind == Kind.Neutral ? -1 : 2;
            }

            // --- 2. neutrals take a side from their neighbours -------------------------
            //
            // UAX #9's N1 and N2, and the part that is easy to get wrong: A NUMBER COUNTS AS
            // RIGHT-TO-LEFT for the purpose of deciding what the space beside it does. So in
            // "42 world" inside a Hebrew line, the space sits at the paragraph level rather than
            // joining the two — which is exactly what keeps them from being reversed together
            // into "42 world" where it should read "world 42".
            for (int i = 0; i < n; i++)
            {
                if (levels[i] != -1) continue;
                int run = i;
                while (run < n && levels[run] == -1) run++;

                bool beforeIsLatin = i > 0 && ClassOf(logical[i - 1]) == Kind.Ltr
                                     && !char.IsDigit(logical[i - 1]);
                bool afterIsLatin = run < n && ClassOf(logical[run]) == Kind.Ltr
                                    && !char.IsDigit(logical[run]);

                // Only a neutral with letters on BOTH sides joins the left-to-right island.
                // Anything else — a boundary, Hebrew, or a number — leaves it at the paragraph
                // level, where it belongs.
                int resolved = beforeIsLatin && afterIsLatin ? 2 : 1;
                for (int k = i; k < run; k++) levels[k] = resolved;
                i = run - 1;
            }

            // --- 3. reverse by level, highest first (UAX #9 rule L2) -------------------
            var chars = new char[n];
            for (int i = 0; i < n; i++)
                // A paired character inside a right-to-left run points the other way: reversing
                // "(שלום)" leaves the parentheses inside-out unless each is swapped for its
                // partner.
                chars[i] = levels[i] == 1 ? Mirror(logical[i]) : logical[i];

            for (int level = 2; level >= 1; level--)
            {
                int i = 0;
                while (i < n)
                {
                    if (levels[i] < level) { i++; continue; }
                    int end = i;
                    while (end < n && levels[end] >= level) end++;
                    Reverse(chars, i, end - 1);
                    i = end;
                }
            }

            return new string(chars);
        }

        /// <summary>
        /// Several lines, each reordered on its own.
        ///
        /// FOR PRE-WRAPPED TEXT, and it is not optional there. Reordering a whole paragraph and
        /// then letting something break it into lines scrambles it: the break lands at a point
        /// that means nothing in visual order. So the breaking happens first, in logical order,
        /// and each finished line is reordered separately.
        /// </summary>
        public static string VisualLines(string logical)
        {
            if (string.IsNullOrEmpty(logical) || !HasRtl(logical)) return logical;
            string[] lines = logical.Split('\n');
            for (int i = 0; i < lines.Length; i++) lines[i] = Visual(lines[i]);
            return string.Join("\n", lines);
        }

        /// <summary>
        /// Break a paragraph into lines FIRST, then reorder each one.
        ///
        /// THE ORDER OF THE TWO OPERATIONS IS THE WHOLE POINT, and doing them the other way
        /// round is silently wrong rather than obviously wrong. `VisualLines` only splits on
        /// newlines that are already there; a label set to `HorizontalWrapMode.Wrap` is handed a
        /// single reordered line and breaks it wherever it happens to fit, which is a position
        /// that means nothing in logical order. Worked through, with a Hebrew phrase whose words
        /// read A B C D from the right:
        ///
        ///     reorder, then wrap       wrap, then reorder
        ///       D C                      B A
        ///       B A                      D C
        ///
        /// Both look like Hebrew. The left one reads "C D A B" and is gibberish; the right one
        /// reads "A B" then "C D", which is the sentence. Nothing about the first is detectable
        /// by anyone who cannot read the language.
        ///
        /// Lines stay in logical order top to bottom — a right-to-left paragraph still stacks its
        /// first line highest. Only the characters within a line move.
        ///
        /// A NO-OP WITHOUT HEBREW, so English and Russian keep whatever wrapping the component
        /// was already doing and this cannot change a layout that already works.
        ///
        /// <paramref name="maxChars"/> is a budget in characters, because Core cannot measure a
        /// font — it has `noEngineReferences: true` and no access to a glyph table. The caller
        /// estimates it from the rect and the point size and should estimate LOW: too narrow
        /// leaves short lines the component will not break again, while too wide hands it a line
        /// it re-breaks, which is the bug this exists to prevent.
        /// </summary>
        public static string WrapVisual(string logical, int maxChars)
        {
            if (string.IsNullOrEmpty(logical) || !HasRtl(logical)) return logical;
            if (maxChars < 1) return VisualLines(logical);

            var outLines = new System.Collections.Generic.List<string>();
            foreach (string paragraph in logical.Split('\n'))
            {
                // SLICED, NOT SPLIT AND REJOINED. Splitting on spaces and putting single spaces
                // back silently collapses a run of them, and the table uses a double space as a
                // spacing device -- "שלל כפול  (פרסומת)" is 18 characters and came back 17.
                // Every break here consumes exactly one space and puts back exactly one newline,
                // so the length is identical and every other character is untouched.
                if (paragraph.Length == 0) { outLines.Add(string.Empty); continue; }

                int start = 0;
                while (start < paragraph.Length)
                {
                    if (paragraph.Length - start <= maxChars)
                    {
                        outLines.Add(paragraph.Substring(start));
                        break;
                    }

                    int cut = -1;
                    for (int j = start + maxChars; j > start; j--)
                        if (paragraph[j] == ' ') { cut = j; break; }

                    if (cut < 0)
                    {
                        // A word longer than the budget goes on a line of its own rather than
                        // being cut in half. Splitting inside a word and reordering the halves
                        // separately gives two fragments each backwards with respect to the other.
                        int end = start + maxChars;
                        while (end < paragraph.Length && paragraph[end] != ' ') end++;
                        outLines.Add(paragraph.Substring(start, end - start));
                        start = end < paragraph.Length ? end + 1 : end;
                        continue;
                    }

                    outLines.Add(paragraph.Substring(start, cut - start));
                    start = cut + 1;
                }
            }

            for (int i = 0; i < outLines.Count; i++) outLines[i] = Visual(outLines[i]);
            return string.Join("\n", outLines);
        }

        /// <summary>True when the string contains anything that reads right to left.</summary>
        public static bool HasRtl(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            foreach (char c in text)
                if (ClassOf(c) == Kind.Rtl) return true;
            return false;
        }

        private static void Reverse(char[] chars, int from, int to)
        {
            while (from < to)
            {
                char swap = chars[from];
                chars[from] = chars[to];
                chars[to] = swap;
                from++;
                to--;
            }
        }

        private static Kind ClassOf(char c)
        {
            // The Hebrew block, plus the Alphabetic Presentation Forms that hold its ligatures.
            if ((c >= '֐' && c <= '׿') || (c >= 'יִ' && c <= 'ﭏ')) return Kind.Rtl;

            if (char.IsLetterOrDigit(c)) return Kind.Ltr;

            // SIGNS AND SEPARATORS BELONG TO THE NUMBER THEY TOUCH, which is UAX #9's ES and CS
            // classes and the single most important detail in this file. StatFormat produces
            // "+3 Might" and "-12.5% Shield"; treat the leading sign as neutral and the reversal
            // strands it on the wrong end — "3+" — on every stat line in the game.
            if (c == '+' || c == '-' || c == '.' || c == ',' || c == ':' || c == '/'
                || c == '%' || c == ' ') return Kind.Ltr;

            return Kind.Neutral;
        }

        /// <summary>
        /// The mirror image of a paired character, or the character itself.
        ///
        /// Reversing a string turns "(שלום)" into ")םולש(" — the glyphs are now pointing the
        /// wrong way round. A bracket in a right-to-left run has to be swapped for its partner.
        /// </summary>
        private static char Mirror(char c)
        {
            switch (c)
            {
                case '(': return ')';
                case ')': return '(';
                case '[': return ']';
                case ']': return '[';
                case '{': return '}';
                case '}': return '{';
                case '<': return '>';
                case '>': return '<';
                default: return c;
            }
        }
    }
}
