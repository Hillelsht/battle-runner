using System;

namespace BattleRunner.Core.Progression
{
    /// <summary>
    /// What a round IS: which act it belongs to, which world it wears, which boss stalks it,
    /// how long it runs, and whether it ends in a fight or in a threat.
    ///
    /// THE GAME HAD NO STRUCTURE ABOVE "ROUND N". Every round was the same length, drew the
    /// same road under the same sky, and ended in a boss fight — and because the level list
    /// and the boss list were both six long and both indexed by the same number, level three
    /// drew the Grave Warden every single time it came round. A note in v0.12.0 claimed those
    /// were "two coprime-ish cycles"; they were the same cycle, and this file is the fix.
    ///
    /// AN ACT IS THE UNIT OF VARIETY. Three to five rounds share a world, and the act's boss
    /// looms at the finish line of every round in it — nearer each time — before it is finally
    /// fought on the last one. That is the shape that makes a world feel like somewhere rather
    /// than a backdrop, and it is why the arithmetic lives here in Core where it can be pinned
    /// by a test instead of discovered on a phone.
    ///
    /// Engine-free and closed-form: no loops over history, no saved state beyond the round
    /// index the profile already keeps.
    /// </summary>
    public readonly struct RoundPlan
    {
        /// <summary>
        /// The opening act, and ONLY the opening act, is two rounds.
        ///
        /// The tutorial teaches the shield on a boss telegraph, so a long first act would
        /// leave a new player minutes holding a verb they have never been shown. It is a
        /// one-off rather than part of the cycle: folding a 2 into the repeat would bring a
        /// two-round gap back around forever, and the design calls for a boss every three to
        /// five rounds once the game is underway. A test pins exactly that.
        /// </summary>
        public const int OpeningActLength = 2;

        /// <summary>Rounds per act from act one onward, cycled.</summary>
        private static readonly int[] ActLengths = { 3, 4, 5, 4 };

        /// <summary>Rounds in one full turn of the act-length cycle — 16, for the closed form.</summary>
        public static readonly int RoundsPerCycle = Sum(ActLengths);

        /// <summary>Authored worlds. Acts walk these FORWARD.</summary>
        public const int ThemeCount = 8;

        /// <summary>Chunks in the shortest round, before the act bonus.</summary>
        public const int BaseChunks = 12;

        /// <summary>Acts over which a round grows from BaseChunks to its longest.</summary>
        public const int GrowthActs = 8;

        /// <summary>Extra chunks on a boss round — the longest road in the act leads to the fight.</summary>
        public const int BossRoundChunkBonus = 2;

        public int RoundIndex { get; }
        public int ActIndex { get; }
        /// <summary>0-based position inside the act.</summary>
        public int RoundInAct { get; }
        public int ActLength { get; }

        /// <summary>True on the last round of an act — the only round with a real fight.</summary>
        public bool IsBossRound => RoundInAct == ActLength - 1;

        /// <summary>
        /// How near the stalker looms at this round's finish, 0-based. Meaningless on a boss
        /// round, where it stops looming and starts swinging.
        /// </summary>
        public int ThreatStep => RoundInAct;

        /// <summary>Which authored world this act wears.</summary>
        public int ThemeSlot => ThemeSlotFor(ActIndex);

        /// <summary>Which authored world an act wears. Acts walk these FORWARD.</summary>
        public static int ThemeSlotFor(int actIndex) => Mod(actIndex, ThemeCount);

        /// <summary>Drives gate values, pack costs and boss HP. The round index, named for what it does.</summary>
        public int Difficulty => RoundIndex;

        /// <summary>
        /// Chunks of road. Grows with the act and again on a boss round.
        ///
        /// This is the DESIGNED length; content may clamp it while the layout generator is
        /// still limited, because tripling round length before there is anything new to put in
        /// the road makes the sameness worse rather than better.
        /// </summary>
        public int ChunkCount =>
            BaseChunks + Math.Min(GrowthActs, ActIndex) + (IsBossRound ? BossRoundChunkBonus : 0);

        private RoundPlan(int roundIndex, int actIndex, int roundInAct, int actLength)
        {
            RoundIndex = roundIndex;
            ActIndex = actIndex;
            RoundInAct = roundInAct;
            ActLength = actLength;
        }

        /// <summary>
        /// The plan for a round. Closed form: the act-length cycle repeats every
        /// RoundsPerCycle rounds, so the whole of history is one division plus a walk of at
        /// most four steps — no matter how deep the player is.
        /// </summary>
        public static RoundPlan For(int roundIndex)
        {
            int r = Math.Max(0, roundIndex);
            if (r < OpeningActLength) return new RoundPlan(r, 0, r, OpeningActLength);

            int past = r - OpeningActLength;
            int cycles = past / RoundsPerCycle;
            int within = past % RoundsPerCycle;

            int act = 1 + cycles * ActLengths.Length;
            int i = 0;
            while (within >= ActLengths[i])
            {
                within -= ActLengths[i];
                act++;
                i++;
            }

            return new RoundPlan(r, act, within, ActLengths[i]);
        }

        /// <summary>
        /// Which boss stalks an act, given how many the roster holds.
        ///
        /// Acts walk the roster BACKWARD while they walk the worlds forward. Stepping by
        /// count-1 is the same as stepping by -1, and count-1 is coprime to count for every
        /// count — so this visits every boss, in a different order than the themes, without
        /// needing a stride chosen by hand for each roster size. The pairing then repeats only
        /// after lcm(ThemeCount, rosterCount) acts: 24 for eight worlds and six bosses,
        /// against exactly 1 before this existed.
        /// </summary>
        public static int BossSlot(int actIndex, int rosterCount) =>
            rosterCount <= 0 ? 0 : Mod(-actIndex, rosterCount);

        /// <summary>Rounds after which a (world, boss) pairing comes round again.</summary>
        public static int PairingPeriod(int rosterCount)
        {
            if (rosterCount <= 0) return ThemeCount;
            return ThemeCount / Gcd(ThemeCount, rosterCount) * rosterCount;
        }

        private static int Mod(int value, int modulus)
        {
            int m = value % modulus;
            return m < 0 ? m + modulus : m;
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

        private static int Sum(int[] values)
        {
            int total = 0;
            foreach (int v in values) total += v;
            return total;
        }
    }
}
