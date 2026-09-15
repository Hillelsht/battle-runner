using System;

namespace BattleRunner.Core.Run
{
    /// <summary>
    /// A champion standing in one lane of the road — a fight the player can lose without the
    /// run ever stopping.
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
    /// WHAT AN ELITE IS, AS A RULE. It blocks a lane. While the army is on it, the army grinds
    /// it down; while it is alive, it winds up and swings, and that swing is answerable — by a
    /// shield, or by simply not being in its lane when it lands. Beat it and it pays. Eat the
    /// blow and it costs considerably more than a pack would.
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
        /// How much of the army an elite's landed swing takes, as a share.
        ///
        /// Several times what an ambush gate costs, because it is answerable twice over —
        /// shield it, or leave its lane — and a threat with two answers that costs the same as
        /// one with none is not a threat, it is a chore.
        /// </summary>
        public const double SwingShare = 0.11;

        /// <summary>What beating one pays back, as a share of the army that killed it.</summary>
        public const double BountyShare = 0.16;

        /// <summary>Seconds of grinding needed to kill one, before the army's size is counted.</summary>
        public const float BaseFightSeconds = 3.6f;

        /// <summary>The floor on that, however large the army.</summary>
        public const float MinFightSeconds = 1.6f;

        /// <summary>Health, 1 at full and 0 dead. Mutable — that is the whole point of the type.</summary>
        public float Health { get; private set; }

        /// <summary>Seconds the army has been grinding it down.</summary>
        public float Elapsed { get; private set; }

        /// <summary>Swings it has already landed or thrown.</summary>
        public int SwingsThrown { get; private set; }

        /// <summary>How long this one takes to kill, given the army that met it.</summary>
        public float FightSeconds { get; private set; }

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
            return e;
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
            if (seconds > BaseFightSeconds * 3.0) seconds = BaseFightSeconds * 3.0;
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
        /// What a landed swing costs the army. Zero when blocked or dodged — and zero is the
        /// right answer for both, because an answer the player found should feel like an
        /// answer rather than like a discount.
        /// </summary>
        public static double SwingCost(double army, int weight, bool blocked, bool dodged)
        {
            if (blocked || dodged) return 0.0;
            double a = army < 0.0 ? 0.0 : army;
            int w = weight < 1 ? 1 : weight;
            double cost = a * SwingShare * w;
            // At least one man, and never the whole army: an elite is a fight to survive, not
            // an execution. Both clamps matter at opposite ends of the campaign.
            if (cost < 1.0) cost = 1.0;
            double ceiling = a * 0.75;
            return cost > ceiling ? ceiling : cost;
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
