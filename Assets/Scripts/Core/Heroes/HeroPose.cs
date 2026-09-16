using System;

namespace BattleRunner.Core.Heroes
{
    /// <summary>What a hero is doing on the select stage.</summary>
    public enum HeroAct
    {
        /// <summary>Standing, breathing, waiting to be looked at. Loops forever.</summary>
        Idle = 0,
        /// <summary>Acknowledging the player. Plays once when the hero is brought forward.</summary>
        Greet = 1,
        /// <summary>One attack. Plays once, on demand, and returns to the idle.</summary>
        Fight = 2
    }

    /// <summary>
    /// One frame of a hero's performance, in degrees and metres.
    ///
    /// EVERY CHANNEL IS A RIGID TRANSFORM, because every one of them has to be. The hero mesh
    /// is a single combined mesh with no skeleton, and the crowd shader that animates the army
    /// reads its walk phase out of the uniform scale — so there is no skinning to borrow and no
    /// spare per-instance channel to invent one with. What there is, is three objects that can
    /// each be moved: the body, the main hand and the off hand. A pose is therefore a small
    /// fixed set of angles and offsets rather than a curve over bones, and the animation has to
    /// be carried by timing and weight rather than by articulation.
    ///
    /// That constraint is less limiting than it sounds. A Diablo select screen is mostly whole-
    /// body: the figure plants, leans, twists, dips and rises, and the weapon travels with it.
    /// Four heroes reading as four different people is a question of HOW they move — how fast
    /// they settle, how far they commit, whether they recover smoothly or in jerks — and none
    /// of that needs a joint.
    /// </summary>
    public readonly struct HeroPose
    {
        /// <summary>Degrees the whole figure leans; positive is forward, toward the player.</summary>
        public readonly float Pitch;
        /// <summary>Degrees the figure turns about its own spine.</summary>
        public readonly float Yaw;
        /// <summary>Degrees the figure tips sideways.</summary>
        public readonly float Roll;
        /// <summary>Metres the figure rises off the ground — a spring, a lunge, a hover.</summary>
        public readonly float Rise;
        /// <summary>Metres the figure steps toward the camera. Negative is a step back.</summary>
        public readonly float Step;
        /// <summary>Degrees the main hand swings, about the grip rather than about the body.</summary>
        public readonly float MainSwing;
        /// <summary>Degrees the main hand lifts away from the body.</summary>
        public readonly float MainLift;
        /// <summary>Degrees the off hand — shield, free hand — raises.</summary>
        public readonly float OffLift;
        /// <summary>Emission multiplier, 1 at rest. The moment of a blow runs hot.</summary>
        public readonly float Heat;

        public HeroPose(float pitch, float yaw, float roll, float rise, float step,
            float mainSwing, float mainLift, float offLift, float heat)
        {
            Pitch = pitch;
            Yaw = yaw;
            Roll = roll;
            Rise = rise;
            Step = step;
            MainSwing = mainSwing;
            MainLift = mainLift;
            OffLift = offLift;
            Heat = heat;
        }

        /// <summary>The figure standing perfectly still, which is what nothing should ever be.</summary>
        public static HeroPose Rest => new HeroPose(0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 1f);
    }

    /// <summary>
    /// How each of the four moves.
    ///
    /// THE REPORT: *"when I choose the main character this page has only cells with text, make
    /// it visuals, make all characters appear so I could see who do I choose with animation of
    /// them how they stay and greet and fight. Main characters should look differently and have
    /// different animation. Like in Diablo when you choose a character."*
    ///
    /// The four already look different — measured, the closest pair of silhouettes differ by 72
    /// pixels sampled across twelve height bands. What they have never had is a way of moving.
    /// This is that, and it is deliberately built out of ONE shape per act driven by four
    /// per-hero numbers, rather than four hand-authored animations:
    ///
    ///   * <see cref="Tempo"/> — how fast this hero does everything. The Warden is slow.
    ///   * <see cref="Commit"/> — how far they throw themselves into a movement.
    ///   * <see cref="Recover"/> — how they come back. High is smooth; low snaps and settles,
    ///     which is what makes the Revenant read as something reassembling itself.
    ///   * <see cref="Sway"/> — how much they move when they are doing nothing at all.
    ///
    /// Four numbers rather than four animations because the acts have to agree with each other:
    /// a greeting and an attack that were authored separately would settle to different resting
    /// poses and the hero would jump between them. Everything here starts and ends at
    /// <see cref="HeroPose.Rest"/>, which is a property rather than a convention — see the
    /// tests.
    /// </summary>
    public static class HeroChoreography
    {
        /// <summary>Seconds one play of an act takes for this hero.</summary>
        public static float ActSeconds(HeroClass hero, HeroAct act)
        {
            float tempo = Tempo(hero);
            switch (act)
            {
                case HeroAct.Greet: return 1.9f / tempo;
                case HeroAct.Fight: return 1.35f / tempo;
                default: return 3.4f / tempo;
            }
        }

        /// <summary>
        /// The pose at <paramref name="seconds"/> into an act.
        ///
        /// Idle loops; greet and fight play once and hold at rest past their end, so a caller
        /// that keeps ticking after an act finishes gets a hero standing still rather than a
        /// hero starting again.
        /// </summary>
        public static HeroPose At(HeroClass hero, HeroAct act, float seconds)
        {
            float span = ActSeconds(hero, act);
            if (span <= 0f) return HeroPose.Rest;
            float t = seconds <= 0f ? 0f : seconds;
            if (act == HeroAct.Idle) t %= span;
            else if (t >= span) return HeroPose.Rest;

            float u = t / span;
            switch (act)
            {
                case HeroAct.Greet: return Greet(hero, u);
                case HeroAct.Fight: return Fight(hero, u);
                default: return Idle(hero, u);
            }
        }

        /// <summary>
        /// Standing: a slow breath with a faster weight-shift over it, so the figure settles on
        /// one foot and then the other rather than bobbing like a buoy.
        ///
        /// EVERY FREQUENCY HERE IS A WHOLE NUMBER, and that is not decoration. The first
        /// version used a golden-ratio frequency so the loop would never visibly repeat — and
        /// a ratio that never repeats is a ratio that does not close, so the last frame of the
        /// loop sat 3.7 degrees of yaw away from the first and the idle ticked once per cycle.
        /// Harmonics give variety that still lands back where it started; a seam in the one
        /// animation that plays forever is worse than a shape that rhymes.
        ///
        /// All three start at zero, so the idle begins at rest exactly as the other two acts
        /// do — which is what lets the screen cut between them with no blend.
        /// </summary>
        private static HeroPose Idle(HeroClass hero, float u)
        {
            float sway = Sway(hero);
            double a = u * 2.0 * Math.PI;
            float breathe = (float)Math.Sin(a);
            float shift = (float)(Math.Sin(a * 2.0) * 0.72 + Math.Sin(a * 3.0) * 0.28);
            return new HeroPose(
                pitch: breathe * 1.5f * sway,
                yaw: shift * 3.2f * sway,
                roll: shift * 1.1f * sway,
                rise: (breathe > 0f ? breathe : 0f) * 0.012f * sway,
                step: shift * 0.02f * sway,
                mainSwing: breathe * 2.6f * sway,
                mainLift: shift * 2.0f * sway,
                offLift: -breathe * 1.8f * sway,
                heat: 1f + breathe * 0.06f * sway);
        }

        /// <summary>
        /// The greeting: turn, raise what you are holding, hold it, and come back down.
        ///
        /// Three phases with different easings rather than one curve, because the hold is the
        /// whole thing — a salute that arrives and immediately leaves reads as a twitch. The
        /// raise is fast and the return is slow, which is how a heavy object is actually lifted
        /// and lowered.
        /// </summary>
        private static HeroPose Greet(HeroClass hero, float u)
        {
            float commit = Commit(hero);
            float recover = Recover(hero);
            // Up over the first third, held to two thirds, down over the last third.
            float raise;
            if (u < 0.33f) raise = EaseOut(u / 0.33f);
            else if (u < 0.66f) raise = 1f;
            else raise = 1f - Settle((u - 0.66f) / 0.34f, recover);

            float turn = (float)Math.Sin(u * Math.PI);
            return new HeroPose(
                pitch: raise * 7f * commit,
                yaw: turn * 13f * commit,
                roll: -raise * 3f * commit,
                // FLOORED AT ZERO. Settle overshoots on a springy hero, so `raise` dips
                // negative on the way down — which is right for everything that rotates and
                // wrong for the one channel that is a height: a rigid figure has no knees, so
                // a negative rise is feet through the plinth rather than a crouch.
                rise: Lift(raise * 0.05f * commit),
                step: raise * 0.10f * commit,
                mainSwing: -raise * 22f * commit,
                mainLift: raise * 58f * commit,
                offLift: raise * 26f * commit,
                heat: 1f + raise * 0.55f * commit);
        }

        /// <summary>
        /// One blow: gather, sweep, land, recover.
        ///
        /// THE THREE PHASES ARE DIFFERENT LENGTHS AND THAT IS THE WHOLE THING. Weight comes
        /// from the asymmetry between gathering and releasing: 42% of the act is the wind-up,
        /// 16% is the swing itself, and the remaining 42% is coming back. A symmetric arc reads
        /// as the figure politely returning a serve.
        ///
        /// THE SWEEP EXISTS BECAUSE THE FIRST VERSION DID NOT HAVE ONE. It ran the wind-up to
        /// -1 and then started the recovery from +1, so the weapon crossed the entire arc
        /// between two frames — 38 degrees of body pitch in one step, which is a teleport
        /// rather than a strike. A blow has to be seen travelling or there is nothing to
        /// flinch from.
        /// </summary>
        private static HeroPose Fight(HeroClass hero, float u)
        {
            float commit = Commit(hero);
            float recover = Recover(hero);
            const float Gather = 0.42f;
            const float Land = 0.58f;

            float travel;   // -1 fully wound back, +1 fully followed through
            float impact;   // 1 exactly on the blow
            if (u < Gather)
            {
                // Eased IN, so the gather is slow and the last of it is quick.
                travel = -EaseIn(u / Gather);
                impact = 0f;
            }
            else if (u < Land)
            {
                // The swing. Eased OUT: fastest as it leaves the wind-up and arriving at the
                // target rather than accelerating into it, which is where a swing's speed
                // actually is.
                travel = -1f + 2f * EaseOut((u - Gather) / (Land - Gather));
                impact = 0f;
            }
            else
            {
                float f = (u - Land) / (1f - Land);
                travel = 1f - Settle(f, recover);
                // A short, sharp flash rather than a fade, so the blow has a frame.
                impact = f < 0.28f ? 1f - f / 0.28f : 0f;
            }

            // THE RISE FALLS AS THE BLOW LANDS. The figure gathers its weight upward through
            // the wind-up and drops it into the strike, which is where a heavy blow's force
            // comes from. It also has to be continuous: holding the rise through the gather
            // and zeroing it at the strike put the entire 4 cm into one frame — the figure
            // snapped to the floor on the frame the mace moved.
            float rise = u < Gather
                ? EaseIn(u / Gather) * 0.04f
                : (u < Land ? 0.04f * (1f - (u - Gather) / (Land - Gather)) : 0f);

            return new HeroPose(
                pitch: travel * 16f * commit,
                yaw: -travel * 9f * commit,
                roll: travel * 6f * commit,
                rise: Lift(rise * commit),
                step: travel * 0.16f * commit,
                // ASYMMETRIC, and it is the same asymmetry as the timing: you draw back less
                // than you follow through. Equal reach either side of rest reads as a pendulum
                // rather than as someone hitting something.
                mainSwing: (travel < 0f ? travel * 74f : travel * 96f) * commit,
                mainLift: (travel < 0f ? -travel * 40f : travel * 12f) * commit,
                offLift: -travel * 14f * commit,
                heat: 1f + impact * 1.30f * commit);
        }

        // --- what makes the four different -------------------------------------------

        /// <summary>Speed. The Warden is armoured and slow; the Ashcaller is quick.</summary>
        public static float Tempo(HeroClass hero)
        {
            switch (hero)
            {
                case HeroClass.Warden: return 0.80f;
                case HeroClass.Ashcaller: return 1.25f;
                case HeroClass.Houndmaster: return 1.15f;
                default: return 0.95f;
            }
        }

        /// <summary>
        /// How far they throw themselves in. The Warden commits hardest — a mace is swung with
        /// the whole body — and the Ashcaller least, because a staff is aimed rather than
        /// heaved and a wizard who lunges is a wizard with no staff.
        /// </summary>
        public static float Commit(HeroClass hero)
        {
            switch (hero)
            {
                case HeroClass.Warden: return 1.20f;
                case HeroClass.Ashcaller: return 0.70f;
                case HeroClass.Houndmaster: return 1.05f;
                default: return 0.90f;
            }
        }

        /// <summary>
        /// How the recovery comes back. Above 1 it overshoots and springs; below 1 it drags.
        /// The Revenant is lowest, so it arrives in steps rather than in a curve, which is what
        /// makes a thing that came back from the dead move like one.
        /// </summary>
        public static float Recover(HeroClass hero)
        {
            switch (hero)
            {
                case HeroClass.Warden: return 0.85f;
                case HeroClass.Ashcaller: return 1.35f;
                case HeroClass.Houndmaster: return 1.55f;
                default: return 0.45f;
            }
        }

        /// <summary>How much they move standing still. A crouched hunter never settles.</summary>
        public static float Sway(HeroClass hero)
        {
            switch (hero)
            {
                case HeroClass.Warden: return 0.70f;
                case HeroClass.Ashcaller: return 1.30f;
                case HeroClass.Houndmaster: return 1.55f;
                default: return 0.95f;
            }
        }

        // --- easings -----------------------------------------------------------------

        private static float Clamp01(float t) => t < 0f ? 0f : (t > 1f ? 1f : t);

        /// <summary>A height, which may never be negative. See the note in Greet.</summary>
        private static float Lift(float metres) => metres < 0f ? 0f : metres;

        private static float EaseIn(float t)
        {
            t = Clamp01(t);
            return t * t;
        }

        private static float EaseOut(float t)
        {
            t = Clamp01(t);
            float i = 1f - t;
            return 1f - i * i * i;
        }

        /// <summary>
        /// A return to rest whose CHARACTER is the hero's, running 0 at the start to exactly 1
        /// at the end.
        ///
        /// A damped oscillation, with <paramref name="recover"/> setting how many wobbles fit
        /// in the window. It is forced to land on 1 by an envelope that reaches zero rather
        /// than by clamping, so a springy hero and a dragging one both finish at rest — which
        /// is the property the acts need in order to be interchangeable.
        /// </summary>
        private static float Settle(float t, float recover)
        {
            t = Clamp01(t);
            if (t >= 1f) return 1f;
            float r = recover < 0.05f ? 0.05f : recover;
            double envelope = Math.Pow(1.0 - t, 2.0 + r);
            double wobble = Math.Cos(t * Math.PI * (0.5 + r * 1.6));
            return 1f - (float)(envelope * wobble);
        }
    }
}
