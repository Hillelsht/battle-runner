using System;

namespace BattleRunner.Core.Text
{
    /// <summary>
    /// Every word the player reads, in three languages.
    ///
    /// WHY A TABLE IN CORE. Until this existed, all ~380 user-facing strings were hardcoded
    /// English literals scattered through screen constructors and data tables, and there was no
    /// localization facility of any kind. Core is where it belongs for the same reason the gate
    /// maths and the campaign simulation are here: it has `noEngineReferences`, so the whole
    /// table — completeness, placeholders, plural forms, the numbers baked into talent prose —
    /// is checked by `dotnet test` rather than by opening the game and looking.
    ///
    /// KEYED BY AN ENUM, NOT A STRING. At this count a mistyped string key is a blank label
    /// nobody notices until a player finds it; a mistyped enum member does not compile.
    ///
    /// AUTHORED AS PAIRS, INDEXED AS AN ARRAY. Each language's table is a `(key, text)[]` so the
    /// three can be written in whatever order reads best and a key inserted in the middle cannot
    /// silently shift another language's entries out of alignment. They are folded into a flat
    /// array once, on first use, and every lookup after that is an array index.
    ///
    /// EVERYTHING HERE IS IN LOGICAL ORDER, INCLUDING HEBREW. Reordering Hebrew for a component
    /// that cannot do it itself is a DISPLAY concern and happens at the very last moment, in
    /// <see cref="BiDi"/>. Doing it here would mean `Format` composing already-reversed fragments,
    /// which produces confident garbage.
    /// </summary>
    public static class Loc
    {
        /// <summary>Separates plural variants inside one entry. See <see cref="Count"/>.</summary>
        public const char FormSeparator = '|';

        private static Language _language = Language.English;
        private static string[][] _tables;

        /// <summary>Raised after the language changes, so built screens can repaint.</summary>
        public static event Action Changed;

        public static Language Language => _language;

        public static bool IsRightToLeft => Languages.IsRightToLeft(_language);

        /// <summary>Switch languages. A no-op when it is already the current one.</summary>
        public static void Use(Language language)
        {
            if (_language == language) return;
            _language = language;
            Changed?.Invoke();
        }

        /// <summary>One string, in the current language.</summary>
        public static string Get(LocKey key) => In(_language, key);

        /// <summary>
        /// One string in a named language, which the tests need and the language button uses to
        /// label itself in a language that is not the one on screen.
        /// </summary>
        public static string In(Language language, LocKey key)
        {
            string[][] tables = Tables;
            int index = (int)key;
            if (index < 0 || index >= tables[0].Length) return string.Empty;
            string text = tables[(int)language][index];
            // An untranslated entry falls back to English rather than rendering blank. The tests
            // make this unreachable; it exists so that a table edited in a hurry degrades to a
            // readable screen rather than an empty one.
            return string.IsNullOrEmpty(text) ? tables[0][index] : text;
        }

        /// <summary>One string with its placeholders filled.</summary>
        public static string Format(LocKey key, params object[] args) =>
            string.Format(Get(key), args);

        /// <summary>
        /// A counted string, in the form the count takes.
        ///
        /// The entry holds its variants separated by <see cref="FormSeparator"/> — two for
        /// English and Hebrew, three for Russian — and <paramref name="count"/> is also passed
        /// to the formatter as {0}, because almost every one of these wants to show the number
        /// it is agreeing with.
        /// </summary>
        public static string Count(LocKey key, long count, params object[] args)
        {
            string entry = Get(key);
            string[] forms = entry.Split(FormSeparator);
            int form = Plural.Form(_language, count);
            string chosen = forms[form < forms.Length ? form : forms.Length - 1];

            var all = new object[(args?.Length ?? 0) + 1];
            all[0] = count;
            if (args != null) Array.Copy(args, 0, all, 1, args.Length);
            return string.Format(chosen, all);
        }

        /// <summary>The number of keys, which is also every table's length.</summary>
        public static int KeyCount => Tables[0].Length;

        private static string[][] Tables
        {
            get
            {
                if (_tables == null) _tables = Build();
                return _tables;
            }
        }

        private static string[][] Build()
        {
            int count = 0;
            foreach (LocKey key in Enum.GetValues(typeof(LocKey)))
                if ((int)key + 1 > count) count = (int)key + 1;

            var tables = new string[Languages.Count][];
            tables[(int)Language.English] = Flatten(LocTables.English, count);
            tables[(int)Language.Russian] = Flatten(LocTables.Russian, count);
            tables[(int)Language.Hebrew] = Flatten(LocTables.Hebrew, count);
            return tables;
        }

        private static string[] Flatten((LocKey Key, string Text)[] authored, int count)
        {
            var flat = new string[count];
            foreach ((LocKey key, string text) in authored) flat[(int)key] = text;
            return flat;
        }
    }
}
