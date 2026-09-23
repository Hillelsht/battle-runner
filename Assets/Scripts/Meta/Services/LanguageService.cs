using BattleRunner.Core.Text;
using UnityEngine;

namespace BattleRunner.Meta.Services
{
    /// <summary>
    /// Which language the game is in, remembered between runs.
    ///
    /// IN PlayerPrefs, NOT IN THE SAVE FILE, and the argument is AudioDirector's own, only
    /// stronger. `PlayerProfile` is per save SLOT and `GameContext.SaveProfile` refuses to write
    /// while no slot is active — so a preference stored there is unwritable on exactly the
    /// screens that offer the control. For the mute toggle that is awkward; for language it is
    /// fatal, because the very first screen a new player sees is slot select, which runs before
    /// any slot exists. A language that could not be changed there would strand anyone whose
    /// device language the game guessed wrong.
    ///
    /// It also means no save-schema bump and no migration: this is not part of a save at all.
    /// </summary>
    public static class LanguageService
    {
        public const string Key = "ui.language";

        /// <summary>
        /// Load the stored choice, or guess from the device the first time.
        ///
        /// Application.systemLanguage rather than the culture name, because Unity has already
        /// done the awkward part: Android reports Hebrew with the legacy ISO code "iw" that was
        /// deprecated in 1989 and never went away, and Unity maps it to SystemLanguage.Hebrew
        /// before we see it.
        ///
        /// Anything that is not Russian or Hebrew gets English, which is the only honest default
        /// — a Portuguese speaker is better served by a language they may have some of than by
        /// one of the two they almost certainly do not.
        /// </summary>
        public static void Load()
        {
            if (PlayerPrefs.HasKey(Key))
            {
                Loc.Use(Languages.FromSaved(PlayerPrefs.GetInt(Key)));
                return;
            }
            Loc.Use(FromDevice(Application.systemLanguage));
        }

        /// <summary>The device's language, mapped to one of the three. Pure, so it is testable.</summary>
        public static Language FromDevice(SystemLanguage device)
        {
            switch (device)
            {
                case SystemLanguage.Russian: return Language.Russian;
                case SystemLanguage.Hebrew: return Language.Hebrew;
                default: return Language.English;
            }
        }

        /// <summary>Change language and remember it.</summary>
        public static void Use(Language language)
        {
            Loc.Use(language);
            PlayerPrefs.SetInt(Key, (int)language);
            PlayerPrefs.Save();
        }

        /// <summary>Step to the next language in the cycle.</summary>
        public static void Next() => Use(Languages.Next(Loc.Language));
    }
}
