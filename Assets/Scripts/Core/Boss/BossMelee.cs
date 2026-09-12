using System;

namespace BattleRunner.Core.Boss
{
    /// <summary>Where one skirmisher is standing this frame, in army-local metres.</summary>
    public readonly struct SkirmishPose
    {
        /// <summary>0 at the army's line, 1 at the boss's feet.</summary>
        public readonly float Toward;
        /// <summary>Sideways spread, in metres, so the line is a line and not a column.</summary>
        public readonly float Across;
        /// <summary>Degrees of body lean. The weapon arc, free inside the instance matrix.</summary>
        public readonly float Lean;
        /// <summary>Yaw, so a fighter faces roughly where it is going.</summary>
        public readonly float Yaw;
        /// <summary>1 while standing, falling to 0 as a body the boss killed goes down.</summary>
        public readonly float Standing;

        public SkirmishPose(float toward, float across, float lean, float yaw, float standing)
        {
            Toward = toward;
            Across = across;
            Lean = lean;
            Yaw = yaw;
            Standing = standing;
        }
    }

    /// <summary>
    /// The army's half of a boss fight: a line of soldiers running in, trading blows, and
    /// falling back, continuously, for as long as the fight lasts.
    ///
    /// WHAT WAS THERE. The army's damage to a boss was `dps * dt`, applied sixty times a
    /// second, writing nothing but the HUD bar. The only fight geometry in the entire
    /// encounter was the boss's own one or two swings per cycle. The player could see a bar
    /// move and a boss lean; there was nothing to see of the thing actually killing it. "No
    /// visual fighting with the boss" is an exact description, and so is "I want to see I
    /// smack him, he smack me back not only using spells but every second".
    ///
    /// WHY THIS IS NOT `Melee`. `Melee` is PRE-RESOLVED: it is constructed knowing both
    /// sides' totals and the outcome, and interpolates between the start and that outcome. A
    /// boss fight has no such outcome at the moment it starts — it depends on what the player
    /// does with the shield and the spell for the next thirty seconds. Reusing `Melee` here
    /// would mean either inventing a fake outcome to animate against, or letting the animation
    /// contradict the real force count. So `Melee` keeps doing the one thing it is right for,
    /// and this does the other.
    ///
    /// WHAT IS SHARED IS THE RENDERER. `SquadRenderer` already owns an instanced fighter
    /// bucket that is empty during a boss fight (there are no squads on a boss round), so a
    /// boss skirmish fills that same array and costs ZERO extra draw calls.
    ///
    /// Engine-free, because all of it is arithmetic and because the two properties that
    /// actually matter — that the volleys deal exactly the damage per second the continuous
    /// grind dealt, and that the line is never in unison — are things a test can check and a
    /// screenshot cannot.
    /// </summary>
    public static class BossMelee
    {
        /// <summary>
        /// How long one fighter's run-in / trade / fall-back cycle takes.
        ///
        /// Long enough that a body is legible as one body doing one thing, short enough that
        /// something is always happening. At 2.8 s with the phase spread below, a fight of
        /// twenty seconds is roughly seven waves.
        /// </summary>
        public const float CycleSeconds = 2.8f;

        /// <summary>
        /// The beat the army's damage is delivered on.
        ///
        /// The grind was arithmetically invisible: `dps * dt` sixty times a second cannot be
        /// seen, heard or felt. The same total, delivered on a beat, is a fight. 0.55 s is
        /// about two blows a second across a whole line of men at their own phases, which is
        /// what "every second" asks for without turning the audio into a machine gun.
        /// </summary>
        public const float SwingSeconds = 0.55f;

        /// <summary>
        /// The most bodies in the skirmish line, whatever the army's size.
        ///
        /// Same reasoning as Melee.MaxFighters and a little larger because a boss is a larger
        /// target and the line has to read across its whole front.
        /// </summary>
        public const int MaxFighters = 30;

        /// <summary>
        /// How far apart in their cycles two neighbouring fighters are.
        ///
        /// The golden ratio, so the phases of any run of fighters are spread about as evenly
        /// as a sequence can spread them, without ever repeating at a short period the way a
        /// simple `index / count` does when the count changes every second.
        /// </summary>
        private const float PhaseStep = 0.618034f;

        /// <summary>
        /// How much of a cycle the spread covers.
        ///
        /// NOT 1. At a full spread every part of the cycle is occupied at every instant and
        /// the line becomes a uniform shimmer — statistically busy, visually static, which is
        /// the same failure as unison from the other end. At 0.35 the line moves in recognisable
        /// VOLLEYS with stragglers: at any instant roughly 30% are closing, 45% trading and
        /// 25% falling back.
        /// </summary>
        private const float PhaseSpread = 0.35f;

        /// <summary>Fraction of the cycle spent running in, then trading; the rest falls back.</summary>
        private const float CloseFraction = 0.30f;
        private const float TradeFraction = 0.45f;

        /// <summary>
        /// How many soldiers break off to fight the boss, for an army of this size.
        ///
        /// Scaled, so a hundred men look like more of a fight than ten do, but sub-linearly
        /// and capped — the formation the whole game is about must not dissolve because a boss
        /// appeared. Always at least one while the army exists: a boss fight with nobody
        /// visibly fighting is the bug this class removes.
        /// </summary>
        public static int Fighters(double force)
        {
            if (force <= 0) return 0;
            double want = 4.0 + Math.Sqrt(force) * 1.15;
            if (want > MaxFighters) want = MaxFighters;
            if (want > force) want = force;
            return want < 1 ? 1 : (int)want;
        }

        /// <summary>
        /// This fighter's phase through its own cycle, 0..1.
        ///
        /// Public because the volley timing and the pose have to agree about it, and because
        /// "no two fighters share a phase" is the property most worth testing.
        /// </summary>
        public static float Phase(int index, float time)
        {
            float offset = (index * PhaseStep) % 1f * PhaseSpread;
            float t = (time / CycleSeconds + offset) % 1f;
            return t < 0f ? t + 1f : t;
        }

        /// <summary>
        /// Where fighter <paramref name="index"/> of <paramref name="count"/> is at
        /// <paramref name="time"/> seconds into the fight.
        ///
        /// <paramref name="downAt"/> is when the boss last killed this fighter — negative if
        /// it has not. A body the boss's blow actually landed on falls over instead of
        /// continuing to helpfully swing at it, which is the difference between a blow having
        /// a victim and a blow having a number.
        /// </summary>
        public static SkirmishPose Pose(int index, int count, float time, float downAt)
        {
            if (count < 1) count = 1;
            float p = Phase(index, time);

            // The lateral slot. Spread across the line by index, jittered by the same
            // golden-ratio walk so the rank is not a comb.
            float lane = count == 1 ? 0f : index / (float)(count - 1) - 0.5f;
            float wobble = ((index * 0.7548777f) % 1f) - 0.5f;
            float across = lane * 3.4f + wobble * 0.45f;

            float toward;
            float lean;
            if (p < CloseFraction)
            {
                // CLOSING. Eased, so a fighter leaves the line quickly and arrives slowing.
                float k = p / CloseFraction;
                k = k * k * (3f - 2f * k);
                toward = k * 0.88f;
                lean = 6f * k;
            }
            else if (p < CloseFraction + TradeFraction)
            {
                // TRADING. Small, fast, asymmetric swings about the contact point. The
                // asymmetry is the whole read: a symmetric oscillation is a metronome, and a
                // fast chop with a slow recovery is a man hitting something.
                float k = (p - CloseFraction) / TradeFraction;
                float swing = (float)Math.Sin(k * Math.PI * 6.0);
                float chop = swing > 0f ? swing * swing : -swing * swing * 0.45f;
                toward = 0.88f + chop * 0.10f;
                lean = 8f + chop * 26f;
            }
            else
            {
                // FALLING BACK. Slower than the run in, and leaning away from the boss.
                float k = (p - CloseFraction - TradeFraction) / (1f - CloseFraction - TradeFraction);
                toward = 0.88f * (1f - k * k);
                lean = -10f * k * (1f - k) * 4f;
            }

            float yaw = (float)Math.Sin(p * Math.PI * 2.0) * 14f + wobble * 18f;

            // GOING DOWN. Half a second from standing to flat, and it does not get back up
            // inside this fight — the renderer stops drawing it once Standing reaches 0.
            float standing = 1f;
            if (downAt >= 0f && time >= downAt)
            {
                float fall = (time - downAt) / 0.5f;
                standing = fall >= 1f ? 0f : 1f - fall;
                lean = lean * standing - 80f * (1f - standing);
                toward *= 0.6f + 0.4f * standing;
            }

            return new SkirmishPose(toward, across, lean, yaw, standing);
        }

        /// <summary>
        /// How many swing beats have completed by <paramref name="time"/> seconds.
        ///
        /// The encounter calls this every frame and applies the difference, so the total
        /// damage delivered over any interval is EXACTLY `dps * elapsed` — the same number the
        /// per-frame grind produced, rearranged in time and nowhere changed in size. That
        /// identity is the point: this is a presentation change, and a presentation change
        /// that quietly alters the balance is a balance change wearing a disguise.
        /// </summary>
        public static int SwingsBy(float time)
        {
            if (time <= 0f) return 0;
            return (int)(time / SwingSeconds);
        }

        /// <summary>
        /// The damage owed for swings <paramref name="from"/>..<paramref name="to"/> at this
        /// damage per second. Summed over a whole fight this is `dps * SwingSeconds * swings`,
        /// which is `dps * time` to within one unfinished beat.
        /// </summary>
        public static float DamageForSwings(int from, int to, float dps)
        {
            if (to <= from || dps <= 0f) return 0f;
            return (to - from) * SwingSeconds * dps;
        }
    }
}
