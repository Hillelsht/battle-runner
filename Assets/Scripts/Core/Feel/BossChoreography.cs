using System;

namespace BattleRunner.Core.Feel
{
    /// <summary>How the boss is standing this frame. All angles in degrees, offsets in metres.</summary>
    public readonly struct BossPose
    {
        /// <summary>Pitch. Negative rears back, positive drives forward over the strike.</summary>
        public readonly float Lean;
        /// <summary>Along Z. Negative closes the gap toward the army.</summary>
        public readonly float Surge;
        /// <summary>Yaw. The idle weight-shift, and the twist into a blow.</summary>
        public readonly float Twist;
        /// <summary>Multiplies the body scale — the wind-up gather and the hit flinch.</summary>
        public readonly float Scale;

        public BossPose(float lean, float surge, float twist, float scale)
        {
            Lean = lean;
            Surge = surge;
            Twist = twist;
            Scale = scale;
        }
    }

    /// <summary>
    /// The boss's whole body language, as arithmetic.
    ///
    /// WHAT THE FIGHT WAS. The boss was pinned at a fixed point, its rotation was written once
    /// at construction and never again, the army never moved, and the only thing that ever
    /// crossed the eleven-metre gap between them was a bolt the PLAYER fired. Its own attack
    /// produced no geometry at its end at all — every effect was centred on the crowd. Two
    /// objects facing each other, one of them changing colour. "it's just standing one close to
    /// another without animation" is an exact description.
    ///
    /// WHAT THIS IS NOT. It is not a skeletal rig. The boss meshes are built from contiguous
    /// box and prism blocks and could be sliced into limbs, but a limb rig is a much larger
    /// change than the fight needs to stop being a tableau: a creature that rears back, gathers,
    /// drives forward through a strike, recoils when hit and collapses when killed reads as
    /// fighting, and all of that is the body transform. The limbs are honest future work.
    ///
    /// Here in Core because a pose is arithmetic, and because the one thing that genuinely must
    /// not go wrong — the wind-up peaking BEFORE the blow rather than after it, which would
    /// make the only warning the player gets arrive too late to use — is a property a test can
    /// check.
    /// </summary>
    public static class BossChoreography
    {
        /// <summary>How far the boss rears back at a full wind-up.</summary>
        public const float MaxRear = 14f;
        /// <summary>And how far it drives through on the strike itself.</summary>
        public const float MaxDrive = 26f;
        /// <summary>How long the strike takes, from the blow landing.</summary>
        public const float StrikeSeconds = 0.55f;
        /// <summary>How long a recoil from taking a hit lasts.</summary>
        public const float RecoilSeconds = 0.34f;
        /// <summary>Metres the boss closes at the peak of a lunge.</summary>
        public const float LungeMetres = 2.4f;

        /// <summary>
        /// The most the army ever presses forward from its mark, in metres.
        ///
        /// Named rather than inline because it is now half of a PAIR: the encounter spawns
        /// the boss at a fixed offset from the crowd's mark, and the arena that the skirmish
        /// line has to span is the difference between the two. Changing one without the other
        /// either puts the army inside the boss or opens a gap nobody can cross.
        /// </summary>
        public const float MaxPress = 4.5f;

        /// <summary>
        /// The pose.
        ///
        /// <paramref name="telegraph"/> is the existing 0..1 wind-up the encounter already
        /// drives; <paramref name="sinceBlow"/> and <paramref name="sinceHit"/> are seconds,
        /// negative when the event has not happened.
        /// </summary>
        public static BossPose Pose(float telegraph, float sinceBlow, float sinceHit, float time)
        {
            float wind = telegraph < 0f ? 0f : (telegraph > 1f ? 1f : telegraph);

            // IDLE. Never zero, even at rest: a boss that is perfectly still between attacks
            // is a prop, and the difference between a prop and a creature is mostly that the
            // creature is always doing something small.
            float lean = (float)Math.Sin(time * 0.9) * 1.6f;
            float twist = (float)Math.Sin(time * 0.62) * 3.4f;
            float surge = (float)Math.Sin(time * 0.75) * 0.10f;
            float scale = 1f + (float)Math.Sin(time * 1.35) * 0.012f;

            // WIND-UP. Rears back and gathers — pulling away from the player is what makes the
            // release read as coming AT them.
            lean -= MaxRear * wind;
            surge += 0.55f * wind;
            scale *= 1f - 0.05f * wind;
            twist += 6f * wind * (float)Math.Sin(time * 18.0);

            // THE STRIKE. A fast drive forward and a slow settle back, because an attack that
            // returns as fast as it arrives has no weight.
            if (sinceBlow >= 0f && sinceBlow < StrikeSeconds)
            {
                float t = sinceBlow / StrikeSeconds;
                // Peaks at about a fifth of the way through and eases out over the rest.
                float drive = t < 0.2f
                    ? t / 0.2f
                    : 1f - (t - 0.2f) / 0.8f;
                drive *= drive * (3f - 2f * drive);
                lean += MaxDrive * drive;
                surge -= LungeMetres * drive;
                scale *= 1f + 0.07f * drive;
            }

            // RECOIL. Snapped back and shrunk, recovering fast. This is the player's
            // confirmation that their spell landed on something with mass.
            if (sinceHit >= 0f && sinceHit < RecoilSeconds)
            {
                float t = 1f - sinceHit / RecoilSeconds;
                lean -= 9f * t * t;
                surge += 0.8f * t * t;
                twist += 7f * t * t;
                scale *= 1f - 0.09f * t * t;
            }

            return new BossPose(lean, surge, twist, scale);
        }

        /// <summary>
        /// How far the ARMY stands from its usual mark during a fight, in metres — positive
        /// is toward the boss.
        ///
        /// The army never moved during a boss fight, which is half of why the encounter read
        /// as two objects near each other rather than as a battle. It presses forward as the
        /// fight goes on, flinches back from a blow, and is driven back hard by one that is
        /// not blocked.
        /// </summary>
        public static float ArmyAdvance(float fightSeconds, float sinceBlow, bool blocked)
        {
            float press = fightSeconds <= 0f ? 0f : fightSeconds / (fightSeconds + 6f);
            // 4.5, up from 3.2, against a boss that now stands at +11 m rather than +16.
            // Together that is a gap closing from thirteen metres to about seven over a
            // fight, which is a distance a line of men can be seen to cross. At the old
            // numbers the two sides never got within ten metres of one another and there was
            // physically nowhere for a melee to happen.
            float advance = press * MaxPress;
            if (sinceBlow >= 0f && sinceBlow < StrikeSeconds)
            {
                float t = 1f - sinceBlow / StrikeSeconds;
                advance -= (blocked ? 0.9f : 2.3f) * t * t;
            }
            return advance;
        }
    }
}
