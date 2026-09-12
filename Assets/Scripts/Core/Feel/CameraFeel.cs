using System;
using BattleRunner.Core.Run;

namespace BattleRunner.Core.Feel
{
    /// <summary>
    /// One camera impulse: how hard an event should hit.
    ///
    /// Plain floats only — BattleRunner.Core sets noEngineReferences, so nothing here may
    /// touch UnityEngine. That is also what makes the curve below testable under plain
    /// dotnet test, which matters because "how much shake" is exactly the kind of number
    /// that gets tuned by feel and then silently drifts.
    ///
    /// The load-bearing decision is that magnitude is the RATIO, never the difference.
    /// Force is unbounded — GateMath soft-caps but does not clamp — so a +5 gate is
    /// enormous at 10 units and meaningless at 1000. Scaling on the difference would make
    /// late-game gates shake the screen off its hinges while early ones did nothing.
    /// </summary>
    public readonly struct CameraFeel
    {
        /// <summary>0..1, added to the shake pool. Amplitude is its square.</summary>
        public readonly float Trauma;

        /// <summary>Signed vertical kick in metres. Up for gain, down for loss.</summary>
        public readonly float KickY;

        public CameraFeel(float trauma, float kickY)
        {
            Trauma = trauma;
            KickY = kickY;
        }

        public static readonly CameraFeel Blocked = new CameraFeel(0.20f, 0f);
        public static readonly CameraFeel Spell = new CameraFeel(0.18f, 0.05f);
        public static readonly CameraFeel Shield = new CameraFeel(0.10f, 0f);
        public static readonly CameraFeel Finish = new CameraFeel(0.15f, 0.08f);
        public static readonly CameraFeel BossDefeated = new CameraFeel(0.55f, 0.12f);
        public static readonly CameraFeel Revive = new CameraFeel(0.30f, 0.10f);

        /// <summary>
        /// How big a change felt, 0..1, measured in octaves: one doubling or halving is
        /// half strength, two is full. 10 to 20 therefore lands exactly as hard as
        /// 1000 to 2000, which is the whole point.
        /// </summary>
        private static float Ratio(double before, double after)
        {
            if (before <= 0.0) return after > 0.0 ? 1f : 0f;
            double r = after / before;
            if (r <= 0.0) return 1f;
            double octaves = Math.Abs(Math.Log(r, 2.0));
            return (float)Math.Min(1.0, octaves / 2.0);
        }

        public static CameraFeel ForGate(GateOp op, double before, double after)
        {
            float m = Ratio(before, after);
            // Multiply gets slightly more punch than Add at the same ratio: it is the
            // gate the player is actually steering for, so it should feel like the prize.
            float bias = op == GateOp.Multiply ? 0.05f : 0f;
            return after >= before
                ? new CameraFeel(0.10f + bias + 0.25f * m, 0.05f + 0.10f * m)
                : new CameraFeel(0.15f + 0.45f * m, -0.06f - 0.12f * m);
        }

        /// <summary>An enemy pack biting into the army.</summary>
        public static CameraFeel ForLoss(double before, double after)
        {
            float m = Ratio(before, after);
            return new CameraFeel(0.18f + 0.42f * m, -0.05f - 0.10f * m);
        }

        /// <summary>A boss blow. A blocked one is a fixed, lighter thud.</summary>
        public static CameraFeel ForBossStrike(double before, double after, bool blocked)
        {
            if (blocked) return Blocked;
            float m = Ratio(before, after);
            return new CameraFeel(0.30f + 0.50f * m, -0.08f - 0.14f * m);
        }
    }
}
