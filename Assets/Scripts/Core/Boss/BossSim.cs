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
        /// What the army is worth, as a multiplier on the hero's own damage.
        ///
        /// THIS WAS `1 + log10(1 + force)`, AND THAT IS A BROKEN CORE LOOP. The entire game is
        /// about making the crowd bigger, and under a log10 a HUNDREDFOLD army dealt 1.7x the
        /// damage. Across the whole game — sixty men at the first boss, a hundred thousand at
        /// the soft cap — the factor moved 2.79 to 6.00, a total of 2.15x, so the thing the
        /// player spends every second of every run on was worth almost nothing at the only
        /// moment it was ever cashed in.
        ///
        /// TWO SEGMENTS, AND THE SECOND EXPONENT IS WHY. The first attempt at this was a
        /// single power law, `1 + 0.49 * f^0.32`, and the project's existing
        /// diminishing-returns test rejected it: the ratio from a thousand men to a hundred
        /// thousand came out LARGER than the ratio from ten to a thousand. A single power law
        /// has a constant ratio between decades, and adding the `1 +` damps the small end
        /// rather than the large one, so the curve accelerates. The comment written alongside
        /// it claimed the opposite, plausibly and wrongly, and only the test knew.
        ///
        /// With the exponent DROPPING at the knee — 0.40 below two thousand men, 0.18 above —
        /// it diminishes by construction, which is doc 01 R4's requirement that gear stay the
        /// long-term lever.
        ///
        /// The weight is then solved from one anchor: the factor at sixty men, which is what
        /// the first boss faces, is held at 2.80 against the old curve's 2.79, so no early
        /// fight is quietly made easier. What changes is the top: a hundredfold army pays
        /// 3.5x instead of 1.7x, and the factor at the soft cap is 15.8 against the old 6.0.
        ///
        /// It is also half of the fix for the difficulty treadmill, because BossHp is scaled
        /// by this same function at the force the act expects — see there.
        /// </summary>
        public static float CrowdFactor(long force)
        {
            if (force <= 0L) return 1f;
            double t = force <= CrowdKnee
                ? Math.Pow(force, CrowdExponentLow)
                : Math.Pow(CrowdKnee, CrowdExponentLow)
                  * Math.Pow(force / CrowdKnee, CrowdExponentHigh);
            return 1f + (float)(CrowdWeight * t);
        }

        /// <summary>Where the army stops being small. Below it every man counts for more.</summary>
        private const double CrowdKnee = 2000.0;
        private const double CrowdWeight = 0.35;
        private const double CrowdExponentLow = 0.40;
        private const double CrowdExponentHigh = 0.18;

        /// <summary>
        /// Player damage per second against the boss: the hero's Damage stat scaled by crowd
        /// size and the overflow bonus from over-cap gates.
        /// </summary>
        public static float PlayerDps(RunResult result, long softCap)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            float damage = result.HeroStats?.Get(StatIds.Damage) ?? 0f;
            long force = Math.Max(0L, result.FinalForceCount);
            return Math.Max(0f, damage) * CrowdFactor(force) * result.OverflowBonus(softCap);
        }

        /// <summary>
        /// The army a competent player brings to the boss of act <paramref name="actIndex"/>.
        ///
        /// Not a guess about skill: it is what the generator hands out. Gate values scale with
        /// depth and rounds get longer per act, so the force reaching a boss roughly doubles
        /// each act until the soft cap stops it — after which it is FLAT, and that flatness is
        /// the single most important fact about this game's late difficulty.
        /// </summary>
        public static long ExpectedForceAtAct(int actIndex, long softCap)
        {
            if (actIndex < 0) actIndex = 0;
            if (softCap <= 0L) return 0L;
            double f = FirstBossForce * Math.Pow(ForcePerAct, Math.Min(actIndex, ExponentGuard));
            return f >= softCap ? softCap : (long)f;
        }

        private const double FirstBossForce = 60.0;
        private const double ForcePerAct = 2.2;

        /// <summary>
        /// Only an overflow guard, NOT where the army stops growing — the soft cap decides
        /// that, and at these numbers it bites at act 10. Capping the exponent at 9 instead
        /// was a first draft, and it quietly meant the model believed the army kept growing
        /// past a cap it had already hit; a test caught it.
        /// </summary>
        private const int ExponentGuard = 40;

        /// <summary>
        /// Boss HP for the boss of one ACT.
        ///
        /// THE OLD SIGNATURE TOOK A ROUND INDEX AND THAT WAS THE WHOLE BUG. A boss is fought
        /// once per act, but its HP compounded on the ROUND counter — and acts are three to
        /// five rounds long. At the authored 0.25-0.29 that is about 3.0x more health between
        /// one fight and the next, against a player who grows 1.2-1.7x and, once force hits
        /// the soft cap, 1.06x. Modelled out: the first boss died in 9 seconds and the act-10
        /// boss needed two and a half HOURS. The game was simultaneously too easy and
        /// unfinishable, and no amount of tuning the growth number could fix both.
        ///
        /// So the boss is priced against WHAT THE PLAYER PROVABLY HAS at that depth:
        ///
        ///   * the army it will face, through the same CrowdFactor its damage is multiplied
        ///     by, at the force the act expects. When the soft cap flattens the army, it
        ///     flattens the boss too, automatically and for the same reason.
        ///   * the stat points the game has handed out by then, which is arithmetic the
        ///     balance settings already fix.
        ///
        /// GEAR AND TALENTS ARE DELIBERATELY NOT IN THE MODEL. They are the player's edge: a
        /// player who invests in them beats the curve, which is the incentive the entire loop
        /// is built on. Modelling them would price that reward away.
        ///
        /// What is left for the authored number is `pressure` — a few per cent per act, which
        /// means exactly "each fight is a little harder than the one before" and nothing else.
        /// At 0.06 the modelled time-to-kill runs 10-18 seconds across sixteen acts, against
        /// 9 seconds rising to two hours.
        /// </summary>
        public static float BossHp(float baseHp, float pressurePerAct, int actIndex,
            float statDamageAtAct, float statDamageAtFirstAct, long softCap)
        {
            if (baseHp <= 0f) throw new ArgumentOutOfRangeException(nameof(baseHp));
            if (actIndex < 0) throw new ArgumentOutOfRangeException(nameof(actIndex));
            if (statDamageAtFirstAct <= 0f)
                throw new ArgumentOutOfRangeException(nameof(statDamageAtFirstAct));

            float army = CrowdFactor(ExpectedForceAtAct(actIndex, softCap))
                         / CrowdFactor(ExpectedForceAtAct(0, softCap));
            float stats = Math.Max(0f, statDamageAtAct) / statDamageAtFirstAct;
            float screw = (float)Math.Pow(1.0 + Math.Max(0f, pressurePerAct), actIndex);
            return baseHp * army * stats * screw;
        }

        /// <summary>
        /// The hero's damage from STAT POINTS alone at act <paramref name="actIndex"/> — base
        /// plus what the game has handed out for clearing every boss up to and including this
        /// one. Gear and talents are excluded on purpose; see BossHp.
        /// </summary>
        public static float StatDamageAtAct(int actIndex, float baseDamage,
            float damagePerPoint, int pointsPerBoss)
        {
            if (actIndex < 0) actIndex = 0;
            return Math.Max(0f, baseDamage)
                   + Math.Max(0f, damagePerPoint) * Math.Max(0, pointsPerBoss) * (actIndex + 1);
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
        public static long ApplyBossHit(long force, float hitFraction, float heroHealth, bool shieldActive) =>
            ApplyBossHit(force, hitFraction, heroHealth, shieldActive, force);

        /// <summary>
        /// One telegraphed blow, with <paramref name="referenceForce"/> being the army that
        /// WALKED INTO the fight.
        ///
        /// WHY THE OLD RULE COULD NOT KILL ANYONE. A blow took a fixed fraction of whatever is
        /// LEFT, so every blow is smaller than the last and the sequence approaches zero
        /// without reaching it. Measured: 24 unblocked blows to wipe an army of ten thousand,
        /// 30 for a hundred thousand. At a four-second attack interval that is a hundred
        /// seconds of ignoring every single telegraph — in a fight that lasts fifteen. The
        /// shield, the timing game, the whole reason the boss telegraphs at all, could be
        /// ignored completely and the player would still win.
        ///
        /// It was also backwards in shape: a BIGGER army survived more blows, so the reward
        /// for playing the run well was passive safety in the fight as well as damage.
        ///
        /// Blending a tenth of the starting army into the basis costs eight blows instead of
        /// twenty-four, and makes the count nearly independent of army size — the army buys
        /// DAMAGE, and Health and the shield buy SURVIVAL. Health still mitigates exactly as
        /// before, so at 150 Health it is eighteen blows rather than seven.
        ///
        /// With `referenceForce == force` this is algebraically the old formula, which is why
        /// the four-argument overload above delegates here and every existing test is
        /// untouched rather than rewritten.
        /// </summary>
        public static long ApplyBossHit(long force, float hitFraction, float heroHealth,
            bool shieldActive, long referenceForce)
        {
            if (hitFraction < 0f || hitFraction > 1f) throw new ArgumentOutOfRangeException(nameof(hitFraction));
            if (shieldActive || force <= 0) return Math.Max(0L, force);
            if (referenceForce < force) referenceForce = force;

            // Everything in double: float intermediates round differently across
            // runtimes (.NET collapses 1000 * 0.4f to exactly 400f, Mono keeps
            // 400.0000059), which silently changed how much force a hit removed.
            double mitigation = 1.0 / (1.0 + Math.Max(0f, heroHealth) / 100.0);
            double basis = (1.0 - OpeningForceWeight) * force + OpeningForceWeight * referenceForce;
            double raw = basis * (double)hitFraction * mitigation;

            // A float fraction puts an exact result a hair ABOVE itself, so a naive
            // Ceiling turns a clean 40% of 1000 into 401. Nudge down by a relative
            // epsilon so whole results stay whole, while genuine fractions still
            // round up (a hit that lands always costs at least one unit).
            long losses = (long)Math.Ceiling(raw - Math.Abs(raw) * 1e-6);
            return Math.Max(0L, force - losses);
        }

        /// <summary>
        /// How much of a blow is measured against the army that STARTED the fight rather than
        /// the one still standing. See ApplyBossHit; a tenth turns twenty-four unblockable
        /// blows into eight and makes the count independent of army size.
        /// </summary>
        private const double OpeningForceWeight = 0.10;

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
