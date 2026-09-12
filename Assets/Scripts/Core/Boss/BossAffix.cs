using System;
using BattleRunner.Core.World;

namespace BattleRunner.Core.Boss
{
    /// <summary>
    /// What makes one Bone Colossus different from the last one.
    ///
    /// Six archetypes is six fights, and a player who reaches act ten has seen each of them
    /// twice. Diablo's own answer to that problem is not more monsters, it is AFFIXES: the
    /// same creature, modified, announced by a prefix on its name and an aura you can read
    /// before it swings. Five of these against six archetypes is thirty distinct encounters
    /// for a fraction of the cost of five new creatures — and unlike new creatures they
    /// compose, so a Frenzied Gore Hound and a Frenzied Grave Warden are different problems.
    /// </summary>
    public enum BossAffix
    {
        /// <summary>An ordinary boss. The first two acts are all of these.</summary>
        None = 0,
        /// <summary>Swings faster. Less time to read the telegraph.</summary>
        Frenzied = 1,
        /// <summary>Carries a ward whatever it is. Break it before you can hurt it.</summary>
        Armoured = 2,
        /// <summary>Heals off every blow it lands. Blocking is the only way to starve it.</summary>
        Vampiric = 3,
        /// <summary>Calls adds whatever it is, and never stops.</summary>
        Haunted = 4,
        /// <summary>Bigger, slower, and each blow hurts far more.</summary>
        Colossal = 5
    }

    /// <summary>
    /// The numbers behind the affixes, and which one an act gets.
    ///
    /// These MODULATE BossSim rather than living inside it. Every archetype function in
    /// BossSim is pure in its archetype and pinned by tests; threading an affix parameter
    /// through all of them would churn that suite for no gain. So the affix contributes
    /// multipliers and addends, the encounter composes them, and the composition itself is
    /// tested here — including the clamps, because ApplyBossHit throws outside [0,1] and a
    /// Colossal boss on an already-heavy archetype is exactly how that would happen.
    /// </summary>
    public static class BossAffixes
    {
        /// <summary>
        /// Acts before affixes start. The first two teach the six base fights; modifying a
        /// creature a player has not yet learned is just noise.
        /// </summary>
        public const int FirstAffixAct = 2;

        /// <summary>
        /// The rotation. SEVEN entries against six bosses, so a (boss, affix) pairing repeats
        /// only every 42 acts — the same reason the theme and boss cycles were given different
        /// periods. Two of the seven are None, because a roster where every single boss is a
        /// champion has no contrast: the affix has to be able to be absent for its presence to
        /// mean anything.
        /// </summary>
        private static readonly BossAffix[] Cycle =
        {
            BossAffix.Frenzied, BossAffix.Armoured, BossAffix.None, BossAffix.Vampiric,
            BossAffix.Haunted, BossAffix.None, BossAffix.Colossal
        };

        public static int CycleLength => Cycle.Length;

        /// <summary>The affix an act's boss carries.</summary>
        public static BossAffix For(int actIndex)
        {
            if (actIndex < FirstAffixAct) return BossAffix.None;
            int i = (actIndex - FirstAffixAct) % Cycle.Length;
            if (i < 0) i += Cycle.Length;
            return Cycle[i];
        }

        /// <summary>
        /// Acts before a specific CHAMPION pairing — one boss carrying one named affix —
        /// comes round again.
        ///
        /// Champion only, and that qualifier is measured rather than convenient: None appears
        /// twice in the cycle, so it is not injective, and an ordinary boss recurs after 18
        /// acts. That carries no information, because None is the ABSENCE of an affix. Every
        /// non-None entry appears exactly once per turn of the cycle, so those pair with the
        /// roster on lcm(7, 6) = 42 — which is 6 x 5 = 30 distinct champion encounters.
        /// </summary>
        public static int PairingPeriod(int rosterCount)
        {
            if (rosterCount <= 0) return Cycle.Length;
            return Cycle.Length / Gcd(Cycle.Length, rosterCount) * rosterCount;
        }

        /// <summary>The word in front of the boss's name. Empty for an ordinary one.</summary>
        public static string Prefix(BossAffix affix) => affix switch
        {
            BossAffix.Frenzied => "Frenzied",
            BossAffix.Armoured => "Armoured",
            BossAffix.Vampiric => "Vampiric",
            BossAffix.Haunted => "Haunted",
            BossAffix.Colossal => "Colossal",
            _ => string.Empty
        };

        /// <summary>The boss's full name, prefix included.</summary>
        public static string Decorate(BossAffix affix, string bossName)
        {
            string prefix = Prefix(affix);
            if (string.IsNullOrEmpty(prefix)) return bossName ?? string.Empty;
            return string.IsNullOrEmpty(bossName) ? prefix : prefix + " " + bossName;
        }

        /// <summary>
        /// The aura. Read before the fight starts, on the threat round as well as the fight,
        /// so a player can see what is coming while there is still time to care.
        /// </summary>
        public static Rgb Tint(BossAffix affix) => affix switch
        {
            BossAffix.Frenzied => new Rgb(1.70f, 0.30f, 0.14f),
            BossAffix.Armoured => new Rgb(0.62f, 0.86f, 1.60f),
            BossAffix.Vampiric => new Rgb(1.40f, 0.10f, 0.42f),
            BossAffix.Haunted => new Rgb(0.75f, 1.45f, 0.55f),
            BossAffix.Colossal => new Rgb(1.35f, 0.95f, 0.30f),
            _ => new Rgb(0f, 0f, 0f)
        };

        // --- modulation ------------------------------------------------------

        /// <summary>Multiplies the boss's health.</summary>
        public static float HpScale(BossAffix affix) => affix == BossAffix.Colossal ? 1.40f : 1f;

        /// <summary>
        /// Multiplies the seconds between attacks. Frenzied compresses, Colossal stretches —
        /// a boss that hit harder AND faster would not be a trade, it would just be worse.
        /// </summary>
        public static float IntervalScale(BossAffix affix) => affix switch
        {
            BossAffix.Frenzied => 0.72f,
            BossAffix.Colossal => 1.18f,
            _ => 1f
        };

        /// <summary>Multiplies how much of the crowd one blow takes.</summary>
        public static float BlowScale(BossAffix affix) => affix == BossAffix.Colossal ? 1.25f : 1f;

        /// <summary>Ward as a share of max health, on top of whatever the archetype carries.</summary>
        public static float ExtraWardFraction(BossAffix affix) =>
            affix == BossAffix.Armoured ? 0.16f : 0f;

        /// <summary>Adds called per cycle, on top of the archetype's own.</summary>
        public static int ExtraAdds(BossAffix affix) => affix == BossAffix.Haunted ? 1 : 0;

        /// <summary>
        /// Health a Vampiric boss regains per blow it LANDS, as a share of its maximum.
        ///
        /// Per landed blow rather than proportional to the force taken: it makes blocking the
        /// counter-play, which is the point, and it does not quietly scale into absurdity
        /// against a huge crowd.
        /// </summary>
        public static float LifeSteal(BossAffix affix) => affix == BossAffix.Vampiric ? 0.045f : 0f;

        /// <summary>How much larger it stands. Colossal has to LOOK colossal.</summary>
        public static float ScaleBoost(BossAffix affix) => affix == BossAffix.Colossal ? 1.22f : 1f;

        // --- composition, with the clamps that keep BossSim from throwing -----

        /// <summary>Boss health for a round, archetype curve and affix together.</summary>
        /// <summary>
        /// Boss HP for one ACT, with the affix applied.
        ///
        /// The parameter is an ACT index, not a round index. It used to be a round index, and
        /// that was the difficulty treadmill: a boss is fought once per act but its health
        /// compounded per round. See BossSim.BossHp for the measurement and the fix.
        /// </summary>
        public static float BossHp(float baseHp, float pressurePerAct, int actIndex,
            float statDamageAtAct, float statDamageAtFirstAct, double armyAtFight, BossAffix affix) =>
            BossSim.BossHp(baseHp, pressurePerAct, actIndex,
                statDamageAtAct, statDamageAtFirstAct, armyAtFight) * HpScale(affix);

        /// <summary>
        /// One blow's share of the crowd, CLAMPED to [0,1].
        ///
        /// BossSim.ApplyBossHit throws outside that range, and a Colossal multiplier on an
        /// archetype already authored near the top of it is precisely how a boss fight would
        /// end in an exception instead of a death. The clamp lives here so it is impossible to
        /// compose an invalid blow, and a test walks every archetype against every affix at a
        /// printed fraction of 1.0 to prove it.
        /// </summary>
        public static float BlowFraction(BossArchetype archetype, BossAffix affix, float hitFraction)
        {
            float blow = BossSim.BlowFraction(archetype, hitFraction) * BlowScale(affix);
            return blow < 0f ? 0f : blow > 1f ? 1f : blow;
        }

        /// <summary>Seconds to the next attack, archetype and affix together.</summary>
        public static float NextInterval(BossArchetype archetype, BossAffix affix,
            float baseInterval, float hpFraction) =>
            Math.Max(0.35f, BossSim.NextInterval(archetype, baseInterval, hpFraction)
                            * IntervalScale(affix));

        /// <summary>The ward a boss raises: whatever its archetype gives, plus its affix.</summary>
        public static float WardPool(BossArchetype archetype, BossAffix affix, float bossHpMax) =>
            BossSim.WardPool(archetype, bossHpMax)
            + Math.Max(0f, bossHpMax) * ExtraWardFraction(affix);

        /// <summary>Adds a cycle calls: the archetype's, plus the affix's.</summary>
        public static int AddsPerCycle(BossArchetype archetype, BossAffix affix) =>
            BossSim.AddsPerCycle(archetype) + ExtraAdds(affix);

        /// <summary>Health regained by a blow that landed. Zero if it was blocked.</summary>
        public static float HealOnHit(BossAffix affix, float bossHpMax, bool blocked)
        {
            if (blocked || bossHpMax <= 0f) return 0f;
            return bossHpMax * LifeSteal(affix);
        }

        private static int Gcd(int a, int b)
        {
            a = Math.Abs(a);
            b = Math.Abs(b);
            while (b != 0)
            {
                int t = b;
                b = a % b;
                a = t;
            }
            return a == 0 ? 1 : a;
        }
    }
}
