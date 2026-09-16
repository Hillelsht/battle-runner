using System;

namespace BattleRunner.Core.Run
{
    /// <summary>
    /// The ONE number a gate or a pack commits to, the moment it first comes into view.
    ///
    /// THE REPORT: *"I like that the crowds blue or red become bigger the moment I become
    /// bigger. The problem is that it becomes bigger the moment I become bigger and I see it
    /// immediately that the next crowd turns bigger, that is strange that it changes right in
    /// front of my eyes. I am not against the concept, I am against seeing it right away, let
    /// the next one become bigger, not the one right in front of my eyes."*
    ///
    /// A gate is a SHARE of the army, which is what makes the whole late game work — the same
    /// recruit gate is eleven men in front of four hundred and eleven million in front of four
    /// hundred million. But a share has to be turned into men to be drawn, and the code turned
    /// it into men again every time the army moved. So the crowd twenty metres up the road grew
    /// while the player watched, which is the complaint: not that it scales, but that it scales
    /// in front of them.
    ///
    /// THE RULE IS ONE COMMIT AND NO SECOND THOUGHTS. A thing resolves its headcount exactly
    /// once, when it crosses the reveal line, and nothing about it changes afterwards. Nothing
    /// is drawn before it has committed, so nothing appears at the wrong size first.
    ///
    /// THE HARD PART IS THAT THE SIGN MUST EQUAL THE EFFECT. Latching what is written on a gate
    /// while still applying a share of the live army would reintroduce, exactly, the bug that
    /// shipped last round — a "+1" that adds nothing — only upside down and harder to see. So
    /// the latched headcount is not a label: it IS the delta. Everything downstream reads this
    /// number, which is why it is a type rather than a long on two behaviours.
    ///
    /// A RALLY IS NOT LATCHED AND DOES NOT NEED TO BE. Its sign is "x2.00", which is a factor
    /// rather than a headcount and so cannot pop however the army moves; its effect stays a
    /// share of the live army, which is what that sign promises. The pop was only ever in the
    /// two gates that count men.
    /// </summary>
    public readonly struct Reveal
    {
        /// <summary>False until the thing has crossed the reveal line.</summary>
        public readonly bool Committed;

        /// <summary>
        /// Signed men: positive for a gate that hands them over, negative for one that takes
        /// them. The sign on the gate, the bodies in the road and the delta applied on contact
        /// are all this one number.
        /// </summary>
        public readonly long Men;

        private Reveal(long men)
        {
            Committed = true;
            Men = men;
        }

        /// <summary>Not yet in view. Nothing draws and nothing applies.</summary>
        public static Reveal Hidden => default;

        /// <summary>
        /// Commit, against the army standing in front of it right now.
        ///
        /// Through <see cref="GateMath.Headcount"/>, which is derived from
        /// <see cref="GateMath.ApplyGate"/> rather than computed alongside it — so the
        /// weight floor that makes a weight-1 gate move a five-man army by a man rather than
        /// by 0.13 of one is inherited here rather than reimplemented.
        /// </summary>
        public static Reveal At(double army, GateOp op, int weight, int depth) =>
            new Reveal(GateMath.Headcount(army, op, weight, depth));

        /// <summary>How many bodies stand in the road for this, at most <paramref name="cap"/>.</summary>
        public int Bodies(int cap)
        {
            if (!Committed || cap <= 0) return 0;
            long men = Men < 0L ? -Men : Men;
            return men >= cap ? cap : (int)men;
        }

        /// <summary>
        /// What a revealed recruit gate hands over, after the hero's gate-yield.
        ///
        /// Yield scales the GAIN rather than the factor, which is the rule
        /// <see cref="GateMath.ApplyGateWithYield"/> already keeps: a recruit worth 30 men
        /// with 20% yield gives 36. Here the gain is simply the number on the sign.
        /// </summary>
        public double Gain(double gateYield)
        {
            if (!Committed || Men <= 0L) return 0.0;
            double yield = gateYield > 0.0 ? gateYield : 0.0;
            return Men * (1.0 + yield);
        }

        /// <summary>
        /// What a revealed ambush — a red gate or an enemy pack — takes, after resist.
        ///
        /// Two clamps, both of which matter and neither of which is cosmetic. It can never
        /// take more than the army has, because the army may have shrunk since this committed
        /// and a latched number that outran it would be a negative army. And it never rounds
        /// away to nothing: zero is reserved for a SHATTERED pack, which is the whole fantasy
        /// of that talent, and a pack that silently cost nothing because resist rounded it out
        /// would teach the player the wrong rule about which of their stats did it.
        /// </summary>
        public double Loss(double force, float resist, bool shattered)
        {
            if (!Committed || shattered || Men >= 0L) return 0.0;
            double from = force > 0.0 ? force : 0.0;
            if (from <= 0.0) return 0.0;

            double kept = 1.0 - (resist > 0.85f ? 0.85 : (resist < 0f ? 0.0 : resist));
            double bite = -(double)Men * kept;
            if (bite < 1.0) bite = 1.0;
            return bite > from ? from : bite;
        }

        /// <summary>
        /// How far ahead of the army a thing commits, in metres.
        ///
        /// THE SAME NUMBER AS THE SIGN'S, deliberately: `TrackController.LabelVisibleMeters`
        /// already decided when a sign appears, and one constant governing the sign, the
        /// bodies and the commit is what stops the three drifting apart. A further-out line
        /// was considered and rejected — fog starts between 35 m and 100 m depending on the
        /// world, so no distance is universally hidden by weather, and this is the smallest
        /// window that fixes the complaint.
        /// </summary>
        public const float LineMeters = 34f;

        /// <summary>
        /// Seconds of grow-in once a thing has committed, so nothing snaps into existence at
        /// the reveal line. Short: the point is that the player does not see the size CHANGE,
        /// and a slow grow-in is a size change with better manners.
        /// </summary>
        public const float GrowSeconds = 0.35f;

        /// <summary>The grow-in curve, 0 at the line and 1 once it is fully out.</summary>
        public static float Grow(float sinceRevealed)
        {
            if (sinceRevealed >= GrowSeconds) return 1f;
            if (sinceRevealed <= 0f) return 0f;
            float t = sinceRevealed / GrowSeconds;
            // Eased out, so the last of it is slow and the arrival has no edge on it.
            return 1f - (1f - t) * (1f - t);
        }
    }
}
