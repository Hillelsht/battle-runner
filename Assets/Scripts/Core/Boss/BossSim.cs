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

        /// <summary>
        /// One boss attack against the crowd. A raised shield negates it entirely;
        /// otherwise the attack removes a fraction of current force, cushioned by the
        /// hero's Health stat (100 Health halves losses).
        /// </summary>
        public static long ApplyBossHit(long force, float hitFraction, float heroHealth, bool shieldActive)
        {
            if (hitFraction < 0f || hitFraction > 1f) throw new ArgumentOutOfRangeException(nameof(hitFraction));
            if (shieldActive || force <= 0) return Math.Max(0L, force);
            float mitigation = 1f / (1f + Math.Max(0f, heroHealth) / 100f);
            long losses = (long)Math.Ceiling(force * hitFraction * mitigation);
            return Math.Max(0L, force - losses);
        }
    }
}
