namespace BattleRunner.Core.Text
{
    /// <summary>
    /// The languages the game speaks.
    ///
    /// Three, and the order is the order the menu button cycles them in. The numeric values are
    /// persisted in PlayerPrefs, so they may be appended to but never reordered.
    /// </summary>
    public enum Language
    {
        English = 0,
        Russian = 1,
        Hebrew = 2
    }

    public static class Languages
    {
        public const int Count = 3;

        /// <summary>
        /// True when this language is written right to left.
        ///
        /// Read by everything that has to mirror: the bidi pass that reorders a string for a
        /// component that cannot do it itself, the anchor flip, and the handful of directional
        /// constants in the menus.
        /// </summary>
        public static bool IsRightToLeft(Language language) => language == Language.Hebrew;

        /// <summary>
        /// Each language's own name, in itself.
        ///
        /// NEVER TRANSLATED, and that is the point: the control that changes language has to be
        /// legible to someone who cannot read the language currently on screen. A Hebrew speaker
        /// who has somehow landed in Russian needs to see the word "עברית", not "иврит".
        /// </summary>
        public static string NativeName(Language language)
        {
            switch (language)
            {
                case Language.Russian: return "РУССКИЙ";
                case Language.Hebrew: return "עברית";
                default: return "ENGLISH";
            }
        }

        /// <summary>The next language in the cycle, wrapping.</summary>
        public static Language Next(Language language) =>
            (Language)(((int)language + 1) % Count);

        /// <summary>A persisted integer back to a language, tolerating anything unexpected.</summary>
        public static Language FromSaved(int value) =>
            value >= 0 && value < Count ? (Language)value : Language.English;
    }
}
