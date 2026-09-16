using System;

namespace BattleRunner.Core.Run
{
    /// <summary>
    /// A champion at the centre of a barricade that spans the whole road — a fight the player
    /// can lose without the run ever stopping, and one they cannot steer around.
    ///
    /// WHY THIS IS NOT A Melee, AND NOT A BossSim. Both already exist and neither fits:
    ///
    ///   * <see cref="Melee"/> fixes its outcome at construction. `Allies` and `Enemies` are
    ///     readonly and `SurvivingAllies` is pure arithmetic, so the whole clash is decided
    ///     before the first frame of it is drawn — which is right for a squad the army simply
    ///     rolls over, and impossible for a fight whose result depends on whether the player
    ///     raises a shield three quarters of a second from now. Editing Melee to carry mutable
    ///     health would put a decision inside a struct whose termination and conservation
    ///     properties are pinned by tests, for the sake of a caller it was never built for.
    ///   * BossSim is the other end of the scale: an act-long HP curve, affixes, archetypes and
    ///     a six-hundred-line state machine, for a thing that is on screen for four seconds.
    ///
    /// So this is a third, small thing that sits BESIDE Melee rather than inside it — the same
    /// separation BossAffixes keeps from BossSim, and for the same reason.
    ///
    /// WHAT AN ELITE IS, AS A RULE. It blocks the road — all three lanes, with the champion in
    /// the middle. While the army is on it, the army grinds it down; while it is alive, it winds
    /// up and swings, and the shield is the only answer. Beat it and it pays.
    ///
    /// *"mini bosses I fight only if they are on my lane, and I want them to be on 3 lanes,
    /// mandatory to fight."*
    ///
    /// THE PRICE HAD TO CHANGE WITH THE RULE, AND BY MORE THAN IT LOOKS. A dodgeable champion
    /// could be priced per swing, because a player who steered paid nothing at all and only the
    /// player who chose to stand there paid anything. Mandatory, the shipped per-swing share of
    /// 0.11 of the *live* army compounds over however many swings the fight happens to be long
    /// enough for — and the fight is LONGEST against the smallest army, because FightLength
    /// shrinks with its square root. Counted out, the shipped numbers made an unavoidable
    /// weight-4 champion cost a hundred-man army 98.3% of itself, and a two-thousand-man army
    /// 82.4%. That is not a difficulty, it is a run ending on an obstacle with one answer.
    ///
    /// So the fight is priced AS A WHOLE and the swings divide it up. <see cref="BarricadeShare"/>
    /// is what the entire barricade takes from an army that shields none of it; the per-swing
    /// share is derived from it and the number of swings the fight has room for, so the total
    /// is the same whether the fight is one swing long or three. The army's size and the frame
    /// rate stop being able to change the bill:
    ///
    ///   weight   whole fight   bounty   tank it   shield it all
    ///        2        -12.0%   +10.0%     -3.2%          +10.0%
    ///        3        -18.0%   +15.0%     -5.7%          +15.0%
    ///        4        -24.0%   +20.0%     -8.8%          +20.0%
    ///
    /// Tanking a barricade costs a few per cent; shielding it is worth a fifth of the army. The
    /// gap between the two — thirty points at weight 4 — IS the skill check, and it is a gap
    /// rather than a cliff.
    /// </summary>
    public struct Elite
    {
        /// <summary>Seconds from the army arriving to the first swing landing.</summary>
        public const float WindUpSeconds = 1.05f;

        /// <summary>Seconds between one swing landing and the next one landing.</summary>
        public const float SwingIntervalSeconds = 1.45f;

        /// <summary>
        /// The shield window, in seconds before a swing lands.
        ///
        /// Wider than the boss's telegraph rather than narrower. A boss fight is the only
        /// thing on screen and the player is looking straight at it; an elite arrives while
        /// the road is still moving, gates are still coming, and the player is steering. The
        /// warning has to survive that.
        /// </summary>
        public const float ReadableWindow = 0.55f;

        /// <summary>
        /// What the WHOLE barricade takes from an army that shields none of it, per point of
        /// weight. Not a per-swing number — see <see cref="SwingCost"/>, which divides this
        /// across however many swings the fight has room for.
        ///
        /// Half of an ambush gate's share (GateMath.AmbushShare = 0.125) rather than several
        /// times it, and the sign of that inversion is the whole change: an ambush gate is
        /// avoidable and so may be expensive, and a barricade is not and so may not.
        /// </summary>
        public const double BarricadeShare = 0.06;

        /// <summary>
        /// What beating one pays back, as a share of the army that killed it, per point of
        /// weight.
        ///
        /// Deliberately just under <see cref="BarricadeShare"/>, so that a player who tanks the
        /// whole fight comes out a few per cent down and a player who shields it comes out a
        /// fifth up. The old 0.16 was set when a champion could be walked around: it had to be
        /// worth stopping for. A wall you cannot walk around does not need a bribe.
        /// </summary>
        public const double BountyShare = 0.05;

        /// <summary>Seconds of grinding needed to kill one, before the army's size is counted.</summary>
        public const float BaseFightSeconds = 3.6f;

        /// <summary>The floor on that, however large the army.</summary>
        public const float MinFightSeconds = 1.6f;

        /// <summary>
        /// The ceiling on it, however small the army — and it is a hard three swings.
        ///
        /// Swings land at 1.05, 2.50 and 3.95 seconds, so 4.4 has room for exactly three and
        /// none for a fourth. The old ceiling of 10.8 left room for SEVEN, all of them against
        /// the smallest army in the game, which is the backwards end of every curve here.
        /// Three is also the number that makes the shield a partial answer rather than an
        /// all-or-nothing one: blocking one swing of three saves a third of the bill.
        /// </summary>
        public const float MaxFightSeconds = 4.4f;

        /// <summary>Health, 1 at full and 0 dead. Mutable — that is the whole point of the type.</summary>
        public float Health { get; private set; }

        /// <summary>Seconds the army has been grinding it down.</summary>
        public float Elapsed { get; private set; }

        /// <summary>Swings it has already landed or thrown.</summary>
        public int SwingsThrown { get; private set; }

        /// <summary>How long this one takes to kill, given the army that met it.</summary>
        public float FightSeconds { get; private set; }

        /// <summary>
        /// How many swings this fight has room for. Fixed when the fight begins, because it is
        /// the DIVISOR the per-swing cost is derived from — if it were re-read from a shrinking
        /// army mid-fight the shares would stop summing to <see cref="BarricadeShare"/>.
        /// </summary>
        public int SwingsExpected { get; private set; }

        public bool Alive => Health > 0f;

        /// <summary>
        /// Begin. A bigger army kills it faster, sub-linearly — the same shape the crowd's
        /// damage against a boss has, so the player learns one rule rather than two.
        /// </summary>
        public static Elite Begin(double army, int weight)
        {
            var e = new Elite
            {
                Health = 1f,
                Elapsed = 0f,
                SwingsThrown = 0,
                FightSeconds = FightLength(army, weight)
            };
            e.SwingsExpected = SwingsIn(e.FightSeconds);
            return e;
        }

        /// <summary>
        /// How many swings land inside a fight of this length.
        ///
        /// Strictly inside: TrackController resolves the grind first and only takes a swing on
        /// a frame the champion survived, so a swing due at exactly the last instant of the
        /// fight does not land. The comparison here is `&lt;` for the same reason.
        ///
        /// A long frame can still swallow a swing — one that steps past both the swing's time
        /// and the end of the fight resolves the death and skips the blow — so the player
        /// occasionally pays for two swings where three were budgeted. It errs downward, in
        /// their favour, and never upward.
        /// </summary>
        public static int SwingsIn(float fightSeconds)
        {
            int n = 0;
            for (float t = WindUpSeconds; t < fightSeconds; t += SwingIntervalSeconds) n++;
            return n < 1 ? 1 : n;
        }

        /// <summary>
        /// Seconds to grind one down. Scales with its weight and shrinks with the square root
        /// of the army, floored so it is never merely a speed bump.
        /// </summary>
        public static float FightLength(double army, int weight)
        {
            int w = weight < 1 ? 1 : weight;
            double a = army < 1.0 ? 1.0 : army;
            // The square root of the army against a reference of a hundred men. Sub-linear, so
            // arriving with a billion men makes an elite quick but never free.
            double scale = Math.Sqrt(100.0 / a);
            double seconds = BaseFightSeconds * w * 0.5 * (0.5 + scale);
            if (seconds < MinFightSeconds) seconds = MinFightSeconds;
            if (seconds > MaxFightSeconds) seconds = MaxFightSeconds;
            return (float)seconds;
        }

        /// <summary>
        /// Advance by <paramref name="deltaSeconds"/> of grinding. Returns true on the single
        /// frame the elite dies, so the caller can pay the bounty exactly once.
        /// </summary>
        public bool Grind(float deltaSeconds)
        {
            if (deltaSeconds <= 0f || Health <= 0f) return false;
            Elapsed += deltaSeconds;
            float span = FightSeconds <= 0f ? MinFightSeconds : FightSeconds;
            float left = 1f - Elapsed / span;
            Health = left < 0f ? 0f : left;
            return Health <= 0f;
        }

        /// <summary>
        /// Seconds until the next swing lands, or a negative number once it is dead.
        ///
        /// Closed form from <see cref="Elapsed"/> rather than a countdown that is reset, so a
        /// dropped frame cannot silently skip a swing or double one.
        /// </summary>
        public float UntilNextSwing()
        {
            if (Health <= 0f) return -1f;
            return NextSwingAt() - Elapsed;
        }

        /// <summary>When the next swing lands, measured from the start of the fight.</summary>
        public float NextSwingAt() => WindUpSeconds + SwingIntervalSeconds * SwingsThrown;

        /// <summary>
        /// True while the player can still see a swing coming and answer it.
        ///
        /// The whole point of naming this rather than comparing inline: the wind-up and the
        /// readable window are two different numbers, and a warning that starts when the blow
        /// starts is not a warning.
        /// </summary>
        public bool IsTelegraphing()
        {
            float until = UntilNextSwing();
            return until >= 0f && until <= ReadableWindow;
        }

        /// <summary>
        /// How far into the telegraph, 0 at the first warning and 1 as the blow lands. Drives
        /// the body's wind-up and the emission heat.
        /// </summary>
        public float TelegraphPhase()
        {
            float until = UntilNextSwing();
            if (until < 0f || until > ReadableWindow) return 0f;
            return 1f - until / ReadableWindow;
        }

        /// <summary>
        /// Consume a swing if one is due. Returns true exactly once per swing, so the caller
        /// can resolve it against the shield and the lane.
        /// </summary>
        public bool TakeSwingIfDue()
        {
            if (Health <= 0f) return false;
            if (Elapsed < NextSwingAt()) return false;
            SwingsThrown++;
            return true;
        }

        /// <summary>
        /// What ONE landed swing costs the army, as men. Zero when blocked — and zero is the
        /// right answer, because an answer the player found should feel like an answer rather
        /// than like a discount. Zero when dodged too, which a barricade never is; the argument
        /// survives so that an elite authored WITHOUT a barricade is still a lane decision.
        ///
        /// <paramref name="swingsInFight"/> is <see cref="SwingsExpected"/> from the champion
        /// doing the swinging, and it is what makes this a divided bill rather than a running
        /// one. Each swing takes the share that leaves exactly `BarricadeShare * weight` of the
        /// army gone once all of them have landed:
        ///
        ///     (1 - perSwing)^n  =  1 - whole
        ///
        /// so one swing takes 12% and three take 4.2% each. The army is re-read between swings
        /// — the cost is a share of what is left, not of what there was — which is why this
        /// compounds and why the root, rather than a division, is the right way to split it.
        /// </summary>
        public static double SwingCost(double army, int weight, int swingsInFight,
            bool blocked, bool dodged)
        {
            if (blocked || dodged) return 0.0;
            double a = army < 0.0 ? 0.0 : army;
            double share = SwingShare(weight, swingsInFight);
            double cost = a * share;
            // At least one man, and never the whole army: an elite is a fight to survive, not
            // an execution. Both clamps matter at opposite ends of the campaign.
            if (cost < 1.0) cost = 1.0;
            double ceiling = a * 0.75;
            return cost > ceiling ? ceiling : cost;
        }

        /// <summary>What one swing of a fight this long takes, as a share of the army left.</summary>
        public static double SwingShare(int weight, int swingsInFight)
        {
            int n = swingsInFight < 1 ? 1 : swingsInFight;
            double whole = WholeFightShare(weight);
            return 1.0 - Math.Pow(1.0 - whole, 1.0 / n);
        }

        /// <summary>
        /// What the whole barricade takes from an unshielded army, as a share. Clamped well
        /// short of the army so that no authored weight can be an execution.
        /// </summary>
        public static double WholeFightShare(int weight)
        {
            int w = weight < 1 ? 1 : weight;
            double whole = BarricadeShare * w;
            return whole > 0.6 ? 0.6 : whole;
        }

        /// <summary>
        /// What a whole barricade does to the army of a player who fights it and does not
        /// shield it: it takes its share, then pays its bounty on what is left.
        ///
        /// THIS IS THE NUMBER PAR IS ESTIMATED FROM. `ChunkLayouts.EstimateParForce` walks all
        /// three lanes and multiplies each by the factors standing in it; a barricade stands in
        /// all three, and charging it as if it were an ambush gate of the same weight — which
        /// is what the pack branch does — would price a weight-4 champion at 62% of the army in
        /// every lane instead of 24% once and 20% back.
        /// </summary>
        public static double UnshieldedFactor(int weight)
        {
            int w = weight < 1 ? 1 : weight;
            return (1.0 - WholeFightShare(weight)) * (1.0 + BountyShare * w);
        }

        /// <summary>What killing one pays, as men.</summary>
        public static double Bounty(double army, int weight)
        {
            double a = army < 0.0 ? 0.0 : army;
            int w = weight < 1 ? 1 : weight;
            double paid = a * BountyShare * w;
            return paid < 1.0 ? 1.0 : paid;
        }
    }
}
