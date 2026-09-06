using System;

namespace BattleRunner.Core.Stats
{
    /// <summary>
    /// The one place that knows how a stat should READ.
    ///
    /// This exists to kill a specific class of bug: <b>percent-ness is a property of the
    /// STAT, not of the ModifierKind.</b> ModifierKind.Flat vs Percent describes how a
    /// modifier COMPOSES — added into the base, or multiplied over the total — and says
    /// nothing about units. Cooldown, GateYield, RunSpeed, EnemyResist, SpellPower and
    /// Fortune are all stored as fractions of 1, so a <i>flat</i> modifier of 0.01 on
    /// Cooldown means one percent. Formatting by the kind printed it as "+0.01 Focus" on
    /// the loot card, which reads as a broken game.
    ///
    /// It also removes a hard-coded minus sign in the menu summary that turned a base
    /// Cooldown of zero into "Focus -0 %".
    ///
    /// Engine-free on purpose: display rules are exactly the kind of thing that rots
    /// silently, and here they are pinned by unit tests that run under plain dotnet test.
    /// </summary>
    public static class StatFormat
    {
        /// <summary>
        /// True for stats stored as a fraction of 1 and shown as a percentage.
        /// The XML docs on <see cref="StatIds"/> are the source of truth: anything
        /// described as a "fraction of" belongs here; "extra seconds" and raw amounts
        /// do not.
        /// </summary>
        public static bool IsFraction(string statId) =>
            statId == StatIds.Cooldown
            || statId == StatIds.GateYield
            || statId == StatIds.RunSpeed
            || statId == StatIds.EnemyResist
            || statId == StatIds.SpellPower
            || statId == StatIds.Fortune;

        /// <summary>The name a player sees. Falls back to the raw id rather than throwing.</summary>
        public static string DisplayName(string statId)
        {
            if (statId == StatIds.Damage) return "Might";
            if (statId == StatIds.Health) return "Vigor";
            if (statId == StatIds.Cooldown) return "Focus";
            if (statId == StatIds.SpellPower) return "Spell";
            if (statId == StatIds.GateYield) return "Gates";
            if (statId == StatIds.RunSpeed) return "Speed";
            if (statId == StatIds.EnemyResist) return "Resist";
            if (statId == StatIds.ShieldDuration) return "Shield";
            if (statId == StatIds.Fortune) return "Fortune";
            return statId ?? "?";
        }

        /// <summary>An item's bonus line: "+2 Might", "+1% Focus".</summary>
        public static string Affix(string statId, float value)
        {
            string name = DisplayName(statId);
            if (!IsFraction(statId))
            {
                float flat = Snap(value, 0.005f);
                return $"{(flat < 0f ? "-" : "+")}{Math.Abs(flat):0.##} {name}";
            }

            float percent = Snap(value * 100f, 0.05f);
            return $"{(percent < 0f ? "-" : "+")}{Math.Abs(percent):0.#}% {name}";
        }

        /// <summary>A resolved total: "Might 10", "Focus 12%".</summary>
        public static string Total(string statId, float value)
        {
            string name = DisplayName(statId);
            return IsFraction(statId)
                ? $"{name} {Snap(value * 100f, 0.05f):0.#}%"
                : $"{name} {Snap(value, 0.05f):0.#}";
        }

        /// <summary>
        /// Collapse a value that will round to zero onto POSITIVE zero.
        ///
        /// Without this, .NET formats negative zero as "-0": a base Cooldown of 0 printed
        /// as "Focus -0 %" on the main menu. Anything below the rounding threshold is
        /// already displaying as zero, so it may as well carry zero's sign.
        /// </summary>
        private static float Snap(float value, float epsilon) =>
            Math.Abs(value) < epsilon ? 0f : value;
    }
}
