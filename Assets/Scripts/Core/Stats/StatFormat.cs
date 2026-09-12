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
            || statId == StatIds.Fortune
            // The deep-tree mechanics. Every one of these is a probability or a fraction
            // of something except Magnetism, which is metres of lane window.
            || statId == StatIds.GateCrit
            || statId == StatIds.OverflowBank
            || statId == StatIds.ChainMultiply
            || statId == StatIds.PackShatter
            || statId == StatIds.SecondWind
            || statId == StatIds.Execute
            || statId == StatIds.SpellEcho
            || statId == StatIds.ShieldReflect;

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
            if (statId == StatIds.SpellCharges) return "Spell Charges";
            if (statId == StatIds.ShieldCharges) return "Shield Charges";
            if (statId == StatIds.Fortune) return "Fortune";
            if (statId == StatIds.GateCrit) return "Gate Crit";
            if (statId == StatIds.OverflowBank) return "Overflow";
            if (statId == StatIds.ChainMultiply) return "Chain";
            if (statId == StatIds.Magnetism) return "Magnetism";
            if (statId == StatIds.PackShatter) return "Shatter";
            if (statId == StatIds.SecondWind) return "Second Wind";
            if (statId == StatIds.Execute) return "Execute";
            if (statId == StatIds.SpellEcho) return "Echo";
            if (statId == StatIds.ShieldReflect) return "Reflect";
            return statId ?? "?";
        }

        /// <summary>
        /// An item's bonus line: "+2 Might", "+5% Might", "+1% Focus".
        ///
        /// There are TWO independent reasons to print a percentage, and getting only one
        /// of them right is how both shipped bugs happened:
        ///
        ///   1. the STAT is stored as a fraction of 1 — a flat 0.01 on Cooldown is 1%
        ///   2. the MODIFIER is <see cref="ModifierKind.Percent"/>, which scales the
        ///      resolved total (StatSheet: <c>final = (base + flat) * (1 + pct)</c>) and
        ///      is therefore a percentage whatever units the stat itself carries
        ///
        /// A plain number is correct only when BOTH are absent. Formatting by kind alone
        /// printed "+0.01 Focus"; formatting by stat alone printed "+0.05 Might" for the
        /// seven items whose affix is Percent on an absolute stat. The kind is required
        /// rather than defaulted so neither half can be forgotten at a call site.
        /// </summary>
        public static string Affix(string statId, ModifierKind kind, float value)
        {
            string name = DisplayName(statId);

            if (kind == ModifierKind.Percent || IsFraction(statId))
            {
                // A Percent modifier is already a fraction of the total; a fraction-valued
                // stat is already a fraction of 1. Either way, x100 is the display.
                float percent = Snap(value * 100f, 0.05f);
                return $"{(percent < 0f ? "-" : "+")}{Math.Abs(percent):0.#}% {name}";
            }

            float flat = Snap(value, 0.005f);
            return $"{(flat < 0f ? "-" : "+")}{Math.Abs(flat):0.##} {name}";
        }

        /// <summary>Convenience overload — the modifier already carries both halves.</summary>
        public static string Affix(StatModifier modifier) =>
            Affix(modifier.StatId, modifier.Kind, modifier.Value);

        /// <summary>
        /// True when a value renders as something other than zero.
        ///
        /// Shares Snap's epsilon so a caller's visibility test can never drift from what
        /// <see cref="Total"/> actually prints — a hand-written gate of 0.0001f would let
        /// a GateYield of 0.0002 through to render as "Gates 0%".
        /// </summary>
        public static bool ShowsAsNonZero(string statId, float value) =>
            Math.Abs(IsFraction(statId) ? value * 100f : value) >= 0.05f;

        /// <summary>A resolved total: "Might 10", "Focus 12%".</summary>
        public static string Total(string statId, float value)
        {
            string name = DisplayName(statId);
            return IsFraction(statId)
                ? $"{name} {Snap(value * 100f, 0.05f):0.#}%"
                : $"{name} {Snap(value, 0.05f):0.#}";
        }

        /// <summary>
        /// The suffixes an army is counted in once it stops fitting on a phone.
        ///
        /// The campaign is continuous now, so the army spans five men to twenty trillion
        /// over sixteen acts and "20,094,331,776,412" is not a number anybody reads. Three
        /// significant figures and a suffix is, and it is the same convention every idle
        /// game on the store uses, so it needs no teaching.
        ///
        /// The table runs to Decillion because a player at perfect lane choice reaches
        /// rank 153 — about 10^46 — over sixty-two rounds, and a label that fell off the
        /// end of its own table would print a bare number exactly where it is least
        /// readable. Beyond the table it falls back to scientific notation rather than
        /// lying about the magnitude.
        /// </summary>
        private static readonly string[] Suffixes =
        {
            "", "K", "M", "B", "T", "Qa", "Qi", "Sx", "Sp", "Oc", "No", "Dc"
        };

        /// <summary>
        /// An army, as a player should read it: 5, 847, 12.4K, 3.91B, 20.1T.
        ///
        /// Exact below a thousand, because the opening of the game is played at counts a
        /// player can see individually and rounding "5" to "5.00" would be absurd. Three
        /// significant figures above it, which is the most anyone tracks and the least
        /// that still shows a gate doing something.
        /// </summary>
        public static string Army(double force)
        {
            if (double.IsNaN(force)) return "0";
            double v = force < 0.0 ? 0.0 : force;
            if (v < 1000.0) return ((long)Math.Floor(v)).ToString(Culture);

            int tier = 0;
            while (v >= 1000.0 && tier < Suffixes.Length - 1)
            {
                v /= 1000.0;
                tier++;
            }
            if (v >= 1000.0) return force.ToString("0.00E+0", Culture);

            // Three significant figures: 9.99K, 99.9K, 999K — never "1000K", which would be
            // a tier the loop has already passed and reads as a bug.
            string shape = v < 10.0 ? "0.00" : v < 100.0 ? "0.0" : "0";
            return v.ToString(shape, Culture) + Suffixes[tier];
        }

        /// <summary>
        /// A signed headcount for a gate sign: "+340", "-1.3K".
        ///
        /// The sign is carried explicitly rather than by the number, because a gate showing
        /// "0" and a gate showing "+0" mean different things to a player and only one of
        /// them is ever true here.
        /// </summary>
        public static string Headcount(long men)
        {
            if (men == 0L) return "0";
            string magnitude = Army(Math.Abs((double)men));
            return (men < 0L ? "-" : "+") + magnitude;
        }

        private static readonly System.Globalization.CultureInfo Culture =
            System.Globalization.CultureInfo.InvariantCulture;

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
