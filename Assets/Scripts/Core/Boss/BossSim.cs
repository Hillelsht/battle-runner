using System;
using BattleRunner.Core.Run;
using BattleRunner.Core.Stats;

namespace BattleRunner.Core.Boss
{
    /// <summary>
    /// Boss-encounter math, engine-free so the encounter is fully drivable from a
    /// hand-authored RunResult (doc 01, R9). Boss HP scales off the level definition,
    /// not the player's realized force, so gear stays the long-term power lever (R4).
    /// </summary>
    public static class BossSim
    {
        /// <summary>
        /// Player damage per second against the boss: the hero's Damage stat scaled by
        /// crowd size (diminishing, log10) and the overflow bonus from over-cap gates.
        /// </summary>
        public static float PlayerDps(RunResult result, long softCap)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            float damage = result.HeroStats?.Get(StatIds.Damage) ?? 0f;
            long force = Math.Max(0L, result.FinalForceCount);
            float crowdFactor = 1f + (float)Math.Log10(1.0 + force);
            return Math.Max(0f, damage) * crowdFactor * result.OverflowBonus(softCap);
        }

        /// <summary>Boss HP for a level: base HP on a mild exponential curve.</summary>
        public static float BossHp(float baseHp, float perLevelGrowth, int levelIndex)
        {
            if (baseHp <= 0f) throw new ArgumentOutOfRangeException(nameof(baseHp));
            if (levelIndex < 0) throw new ArgumentOutOfRangeException(nameof(levelIndex));
            return baseHp * (float)Math.Pow(1.0 + Math.Max(0f, perLevelGrowth), levelIndex);
        }

        /// <summary>Seconds to defeat the boss at the given dps; infinity when dps is zero.</summary>
        public static float TimeToKill(float bossHp, float dps) =>
            dps <= 0f ? float.PositiveInfinity : bossHp / dps;

        // ================= ARCHETYPE PATTERNS ==================================
        // Every one of these is the identity for the archetype it does not apply to, so
        // adding a pattern cannot change how the archetypes that predate it behave. Slam
        // is the fight the game already had, and it must stay byte-identical to it.

        /// <summary>Blows a Volley lands per telegraph.</summary>
        public const int VolleyBlows = 3;

        /// <summary>How much of its own health a Warded boss hides behind a ward.</summary>
        public const float WardFraction = 0.22f;

        /// <summary>Adds a Summoner calls per cycle.</summary>
        public const int SummonCount = 2;

        /// <summary>How many blows one attack cycle lands.</summary>
        public static int BlowsPerCycle(BossArchetype archetype) =>
            archetype == BossArchetype.Volley ? VolleyBlows : 1;

        /// <summary>
        /// One blow's share of the printed hit.
        ///
        /// A volley is deliberately worth MORE than a slam in total (3 x 0.42 = 1.26) and
        /// less per blow. That is the whole trade: the pattern punishes a player who never
        /// learns the timing and rewards one who does, because the single shield window
        /// covers all three blows and turns the worst attack in the game into the best one
        /// to defend.
        /// </summary>
        public static float BlowFraction(BossArchetype archetype, float hitFraction)
        {
            float f = Math.Max(0f, hitFraction);
            return archetype == BossArchetype.Volley ? f * 0.42f : f;
        }

        /// <summary>Seconds between blows inside one volley.</summary>
        public static float VolleyGapSeconds(float telegraphSeconds) =>
            Math.Max(0.08f, Math.Min(0.30f, telegraphSeconds * 0.22f));

        /// <summary>
        /// Seconds until the next attack. Enrage compresses its cycle as its own health
        /// falls, down to 45% of the printed interval at the moment it dies — so the last
        /// tenth of the fight is the dangerous part, which is where a boss fight should
        /// put its danger.
        /// </summary>
        public static float NextInterval(BossArchetype archetype, float baseInterval,
            float hpFraction)
        {
            if (archetype != BossArchetype.Enrage) return baseInterval;
            float spent = 1f - Math.Min(1f, Math.Max(0f, hpFraction));
            return baseInterval * (1f - 0.55f * spent);
        }

        /// <summary>
        /// Force a Drain boss bleeds off the crowd over one tick. A raised shield stops it
        /// completely, which is the only reason the shield is worth holding in that fight.
        ///
        /// Proportional to the crowd, so it never trivially wipes a small army nor tickles
        /// a large one, and rounded UP only when it would otherwise round to nothing — a
        /// drain that shows as zero for a whole second reads as a broken mechanic.
        /// </summary>
        public static long DrainTick(BossArchetype archetype, long force, float hitFraction,
            float seconds, bool shieldActive)
        {
            if (archetype != BossArchetype.Drain || shieldActive || force <= 0 || seconds <= 0f)
                return 0L;
            double rate = Math.Max(0f, hitFraction) * 0.20;   // per second, of current force
            double loss = force * rate * seconds;
            if (loss <= 0.0) return 0L;
            return Math.Max(1L, (long)Math.Floor(loss));
        }

        /// <summary>The ward a Warded boss raises, in HP.</summary>
        public static float WardPool(BossArchetype archetype, float bossHpMax) =>
            archetype == BossArchetype.Warded ? Math.Max(0f, bossHpMax) * WardFraction : 0f;

        /// <summary>
        /// Damage against a warded boss: the ward soaks first and only the remainder
        /// reaches its health.
        ///
        /// <paramref name="wardMultiplier"/> is what a spell brings — it strips a ward
        /// several times faster than the crowd's grind does, so "break the ward" is an
        /// action the player takes rather than something that merely happens to them.
        /// </summary>
        public static float ThroughWard(float amount, float ward, float wardMultiplier,
            out float wardLeft)
        {
            float incoming = Math.Max(0f, amount);
            wardLeft = Math.Max(0f, ward);
            if (wardLeft <= 0f) return incoming;

            float againstWard = incoming * Math.Max(1f, wardMultiplier);
            if (againstWard < wardLeft)
            {
                wardLeft -= againstWard;
                return 0f;
            }

            // The overkill crosses back at the ordinary rate, or a single big spell would
            // shatter the ward AND land its whole multiplied value on the health beneath.
            float spent = wardLeft / Math.Max(1f, wardMultiplier);
            wardLeft = 0f;
            return Math.Max(0f, incoming - spent);
        }

        /// <summary>Adds called this cycle.</summary>
        public static int AddsPerCycle(BossArchetype archetype) =>
            archetype == BossArchetype.Summoner ? SummonCount : 0;

        /// <summary>
        /// What un-cleared adds cost the crowd when the next cycle comes round.
        ///
        /// Proportional per add, with a floor of one unit each: a flat cost would be
        /// meaningless to a crowd of four hundred and lethal to a crowd of twenty, and the
        /// same fight has to work at both ends. The floor is what stops "ignore the adds"
        /// from becoming correct against a small army that has already rounded the
        /// percentage away to nothing.
        /// </summary>
        public static long AddBite(int adds, long force)
        {
            if (adds <= 0 || force <= 0) return 0L;
            long bite = adds * (long)Math.Ceiling(force * 0.06);
            return Math.Min(force, Math.Max(adds, bite));
        }

        /// <summary>
        /// One boss attack against the crowd. A raised shield negates it entirely;
        /// otherwise the attack removes a fraction of current force, cushioned by the
        /// hero's Health stat (100 Health halves losses).
        /// </summary>
        public static long ApplyBossHit(long force, float hitFraction, float heroHealth, bool shieldActive)
        {
            if (hitFraction < 0f || hitFraction > 1f) throw new ArgumentOutOfRangeException(nameof(hitFraction));
            if (shieldActive || force <= 0) return Math.Max(0L, force);

            // Everything in double: float intermediates round differently across
            // runtimes (.NET collapses 1000 * 0.4f to exactly 400f, Mono keeps
            // 400.0000059), which silently changed how much force a hit removed.
            double mitigation = 1.0 / (1.0 + Math.Max(0f, heroHealth) / 100.0);
            double raw = force * (double)hitFraction * mitigation;

            // A float fraction puts an exact result a hair ABOVE itself, so a naive
            // Ceiling turns a clean 40% of 1000 into 401. Nudge down by a relative
            // epsilon so whole results stay whole, while genuine fractions still
            // round up (a hit that lands always costs at least one unit).
            long losses = (long)Math.Ceiling(raw - Math.Abs(raw) * 1e-6);
            return Math.Max(0L, force - losses);
        }

        /// <summary>
        /// How many men the boss takes off the army in one swat of the constant melee.
        ///
        /// NOT `ApplyBossHit`. That models the telegraphed blow: it is answerable by a shield,
        /// it is the thing the whole timing game is built around, and it is tuned to take a
        /// large fraction at once. This is the attrition of standing next to something that
        /// large, delivered on the same beat as the army's own swings, and it differs on every
        /// axis that matters:
        ///
        ///   - UNBLOCKABLE, by design. A shield that answered this too would make the shield's
        ///     real job — the telegraphed blow — unreadable, because the player could no
        ///     longer tell which of the two things their shield just did.
        ///   - Tiny per beat, so it can colour a fight without ever deciding one.
        ///   - Scaled by Health identically, so the stat means the same thing in both places.
        ///
        /// Rounds DOWN, with a floor of one whenever the army is large enough to lose one.
        /// Rounding up, as a real blow does, would make a 0.14% bite cost an army of five the
        /// same as an army of five thousand and quietly wipe small crowds.
        /// </summary>
        public static long MaulBite(long force, float fraction, float heroHealth)
        {
            if (force <= 0L || fraction <= 0f) return 0L;
            if (fraction > 1f) fraction = 1f;

            double mitigation = 1.0 / (1.0 + Math.Max(0f, heroHealth) / 100.0);
            double raw = force * (double)fraction * mitigation;

            // A relative epsilon UPWARD, the mirror of the nudge ApplyBossHit applies
            // downward and for the same reason: a float fraction puts an exact result a hair
            // BELOW itself, so a naive floor turns a clean 0.14% of 10 000 into 13 rather
            // than 14. Genuine fractions still round down, which is the rule this wants.
            long bite = (long)(raw + Math.Abs(raw) * 1e-6);
            // At least one, but never the last man: the maul is attrition, and a fight lost to
            // attrition alone is a fight the player was given no way to answer.
            if (bite < 1L && raw > 0.0 && force > 1L) bite = 1L;
            if (bite >= force) bite = force - 1L;
            return bite < 0L ? 0L : bite;
        }
    }
}
