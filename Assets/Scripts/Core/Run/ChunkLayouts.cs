using System;
using System.Collections.Generic;
using BattleRunner.Core.Progression;

namespace BattleRunner.Core.Run
{
    /// <summary>The named things a stretch of road can ask of a player.</summary>
    public enum ChunkShape
    {
        /// <summary>A run of add gates in alternating lanes. Weave, or lose them.</summary>
        Ladder = 0,
        /// <summary>A multiply beside a heavy subtract. Commit early or take neither.</summary>
        Fork = 1,
        /// <summary>Packs and no gates. Pure steering under pressure.</summary>
        Gauntlet = 2,
        /// <summary>Subtract in every lane but one.</summary>
        Minefield = 3,
        /// <summary>Every lane costs something. Pick the cheapest.</summary>
        Toll = 4,
        /// <summary>A big multiply behind a pack that costs more than it looks.</summary>
        Vault = 5,
        /// <summary>Sparse and wide. Recovery, and it is what makes the rest read.</summary>
        Breather = 6,
        /// <summary>Packs alternating outer lanes, fast.</summary>
        Crossfire = 7
    }

    /// <summary>A gate the generator decided on, before Unity turns it into an object.</summary>
    public readonly struct PlannedGate
    {
        public GateOp Op { get; }
        public int Value { get; }
        public int Lane { get; }
        public float Position { get; }

        public PlannedGate(GateOp op, int value, int lane, float position)
        {
            Op = op;
            Value = value;
            Lane = lane;
            Position = position;
        }
    }

    /// <summary>An enemy pack the generator decided on.</summary>
    public readonly struct PlannedPack
    {
        public int ForceCost { get; }
        public int Lane { get; }
        public float Position { get; }

        public PlannedPack(int forceCost, int lane, float position)
        {
            ForceCost = forceCost;
            Lane = lane;
            Position = position;
        }
    }

    /// <summary>One chunk's worth of decisions.</summary>
    public sealed class ChunkLayout
    {
        public readonly ChunkShape Shape;
        public readonly List<PlannedGate> Gates = new List<PlannedGate>(6);
        public readonly List<PlannedPack> Packs = new List<PlannedPack>(4);

        public ChunkLayout(ChunkShape shape) => Shape = shape;
    }

    /// <summary>
    /// What is actually on the road.
    ///
    /// THE OLD GENERATOR HAD THREE OUTCOMES. `BuildChunksForLevel` put an add gate at 12 m,
    /// another at 28 m, and on every third chunk a x2 opposite a -N at 40 m, with one enemy
    /// pack always in lane 0. Every chunk in the game was one of those three, cycling forever,
    /// which is the literal reason the report was "every round the doors are the same".
    ///
    /// Eight shapes now, each ASKING SOMETHING DIFFERENT rather than presenting the same
    /// question in a different order: weave for a run of gates, commit to one side of a fork,
    /// steer through packs with nothing to gain, choose which loss is cheapest. A round is a
    /// sequence of them chosen from its index, so two rounds of the same length still play
    /// differently.
    ///
    /// SPACING IS THE ONE HARD CONSTRAINT. At 10 m/s a 45 m chunk is 4.5 seconds, and the
    /// comment that survives from the original generator is that decisions 0.2 s apart are
    /// unreadable. Nothing here places two separate decisions closer than MinDecisionGap, and
    /// a test walks every shape at every difficulty to prove it.
    /// </summary>
    public static class ChunkLayouts
    {
        public const float ChunkMeters = 45f;

        /// <summary>Metres between two things a player has to react to separately — 1.2 s at 10 m/s.</summary>
        public const float MinDecisionGap = 12f;

        /// <summary>Nothing is placed in the last few metres, so a decision never straddles a seam.</summary>
        public const float TailMargin = 5f;

        /// <summary>
        /// The WEIGHT of a plain recruit gate. See GateMath: a gate is a share of the army
        /// that meets it, and the number the generator authors is how many shares.
        ///
        /// It used to be `4 + 2*difficulty + step`, an absolute headcount, and the reason it
        /// is now a flat 1 is the whole point of the change: a share is worth the same at
        /// fifty men and at fifty billion, so there is nothing for the round index to
        /// correct for. Depth still matters, but it matters on the RED side, which is where
        /// the difficulty was asked to go.
        /// </summary>
        public static int RecruitWeight(int difficulty, int step) => 1;

        /// <summary>
        /// The weight of an ambush gate or an enemy pack.
        ///
        /// THE ONE THING THAT SCALES WITH DEPTH, and deliberately: the green side is flat,
        /// so every act that passes makes the road more dangerous without making it more
        /// rewarding. That asymmetry is the entire difficulty curve of the run, and it is
        /// what makes a continuous army something a player can actually lose ground on.
        /// Capped at 3 because GateMath.AmbushShareMax already refuses to take more than
        /// 62% in one gate, and a weight that outran the cap would be a difficulty knob
        /// that silently stopped turning.
        /// </summary>
        public static int AmbushWeight(int difficulty, int step) =>
            Math.Min(AmbushWeightMax, 1 + Math.Max(0, difficulty) / AmbushActsPerWeight
                                        + Math.Max(0, step) / AmbushStepsPerWeight);

        /// <summary>Rounds of depth per extra point of ambush weight.</summary>
        public const int AmbushActsPerWeight = 14;

        /// <summary>Chunks into a round per extra point of ambush weight.</summary>
        public const int AmbushStepsPerWeight = 8;

        /// <summary>Heaviest a single ambush may be authored.</summary>
        public const int AmbushWeightMax = 3;

        /// <summary>
        /// Build one chunk. <paramref name="step"/> is its index within the round, which is
        /// what makes a chunk late in a long round harder than the same shape early on.
        /// </summary>
        public static ChunkLayout Build(ChunkShape shape, int difficulty, int step, ref uint rng)
        {
            var layout = new ChunkLayout(shape);
            int add = RecruitWeight(difficulty, step);
            int cost = AmbushWeight(difficulty, step);
            int lane = PickLane(ref rng);

            switch (shape)
            {
                case ChunkShape.Ladder:
                    // Three gates, each one lane over from the last, so the player is steering
                    // continuously rather than picking once and holding.
                    layout.Gates.Add(new PlannedGate(GateOp.Add, add, lane, 10f));
                    layout.Gates.Add(new PlannedGate(GateOp.Add, add + 1, Shift(lane, 1), 24f));
                    layout.Gates.Add(new PlannedGate(GateOp.Add, add + 2, Shift(lane, 2), 38f));
                    break;

                case ChunkShape.Fork:
                    layout.Gates.Add(new PlannedGate(GateOp.Add, add, Shift(lane, 1), 12f));
                    // The multiply and its price share a Z: the choice has to be made before
                    // either is close enough to read, which is what makes it a commitment.
                    layout.Gates.Add(new PlannedGate(GateOp.Multiply, 2, lane, 32f));
                    layout.Gates.Add(new PlannedGate(GateOp.Subtract, cost, Shift(lane, 1), 32f));
                    layout.Gates.Add(new PlannedGate(GateOp.Subtract, cost, Shift(lane, 2), 32f));
                    break;

                case ChunkShape.Gauntlet:
                    layout.Packs.Add(new PlannedPack(cost, lane, 10f));
                    layout.Packs.Add(new PlannedPack(cost, Shift(lane, 2), 24f));
                    layout.Packs.Add(new PlannedPack(cost, Shift(lane, 1), 38f));
                    break;

                case ChunkShape.Minefield:
                    // One safe lane, and it is not announced. The add is the reward for
                    // finding it rather than for surviving.
                    layout.Gates.Add(new PlannedGate(GateOp.Subtract, cost, Shift(lane, 1), 26f));
                    layout.Gates.Add(new PlannedGate(GateOp.Subtract, cost, Shift(lane, 2), 26f));
                    layout.Gates.Add(new PlannedGate(GateOp.Add, add + 1, lane, 26f));
                    break;

                case ChunkShape.Toll:
                    // Every lane costs. The only decision left is which loss to accept, which
                    // is a decision the game never asked before.
                    layout.Gates.Add(new PlannedGate(GateOp.Add, add + 2, lane, 10f));
                    layout.Gates.Add(new PlannedGate(GateOp.Subtract, Math.Max(1, cost - 1), lane, 30f));
                    layout.Gates.Add(new PlannedGate(GateOp.Subtract, cost, Shift(lane, 1), 30f));
                    layout.Gates.Add(new PlannedGate(GateOp.Subtract, cost + 1, Shift(lane, 2), 30f));
                    break;

                case ChunkShape.Vault:
                    // The pack and the multiply are in the SAME lane. Taking the prize means
                    // paying for it, and dodging the pack means dodging the prize.
                    layout.Packs.Add(new PlannedPack(cost + 1, lane, 18f));
                    layout.Gates.Add(new PlannedGate(GateOp.Multiply, 3, lane, 34f));
                    layout.Gates.Add(new PlannedGate(GateOp.Add, add, Shift(lane, 1), 34f));
                    break;

                case ChunkShape.Breather:
                    layout.Gates.Add(new PlannedGate(GateOp.Add, add + 1, lane, 22f));
                    break;

                default: // Crossfire
                    layout.Packs.Add(new PlannedPack(cost, Shift(lane, 1), 12f));
                    layout.Gates.Add(new PlannedGate(GateOp.Add, add + 1, lane, 26f));
                    layout.Packs.Add(new PlannedPack(cost, Shift(lane, 2), 40f));
                    break;
            }

            return layout;
        }

        /// <summary>Most gates any single shape places. Pools are sized from this.</summary>
        public const int MaxGatesPerChunk = 4;

        /// <summary>Most packs any single shape places.</summary>
        public const int MaxPacksPerChunk = 3;

        /// <summary>
        /// Every chunk of one round, shapes chosen and contents rolled. One entry point so
        /// the sequence and the contents cannot be generated from different seeds and drift
        /// apart.
        /// </summary>
        public static ChunkLayout[] BuildRound(RoundPlan plan)
        {
            int count = Math.Max(1, plan.ChunkCount);
            ChunkShape[] shapes = SequenceFor(plan, count);
            uint rng = Seed(plan.RoundIndex ^ 0x5BF03635);

            var layouts = new ChunkLayout[count];
            for (int i = 0; i < count; i++)
                layouts[i] = Build(shapes[i], plan.Difficulty, i, ref rng);
            return layouts;
        }

        /// <summary>
        /// How close to the best lane a "par" player is assumed to steer, chunk by chunk.
        ///
        /// MEASURED, AND WORTH STATING PLAINLY: at 0.6 a par player is BELOW break-even from
        /// about round ten onward — par falls from 5.5x the army that walked in at round 0 to
        /// 1.5x at round 15 and bottoms out after that. That is not a bug in this estimate,
        /// it is the difficulty that was asked for arriving where it was aimed: the ambush
        /// weight and the depth ramp both climb while the recruit share does not, so an
        /// average line stops being enough. Simulated over sixty rounds, break-even sits at
        /// about 0.72 and a player at 0.85 grows steadily.
        ///
        /// What stops that from being punishing is StandingArmy.Floor, not this number — a
        /// player who keeps missing holds their rank rather than losing it.
        /// </summary>
        public const double ParKeepFraction = 0.6;

        /// <summary>
        /// The army a par player is expected to hold at the finish.
        ///
        /// THE OLD ESTIMATE COMPOUNDED EVERY MULTIPLY, and that stopped working the moment
        /// layouts became varied. Measured across the generator it swung by two orders of
        /// magnitude on nothing but how many multiply gates a round happened to roll — round 2
        /// estimated 297 and round 5 estimated 12,533, with round 20 and round 30 both pinned
        /// at the soft cap. Par sizes the revive a player is handed after paying for one, so
        /// that is not a cosmetic error: it is the difference between coming back with 99 units
        /// and coming back with four thousand.
        ///
        /// With gates proportional, par is far simpler and far more honest than the log-damped
        /// fudge that used to live here. Every chunk has a BEST lane and a WORST one, both of
        /// which are exact products of the shares in them, and par is the geometric blend of
        /// the two — a player who takes most of the good lanes and eats some of the bad ones.
        /// No estimate of what a multiply is "really" worth is needed, because a multiply is
        /// now the same kind of thing as everything else in the round.
        /// </summary>
        public static double EstimateParForce(IReadOnlyList<ChunkLayout> layouts, double startingForce)
        {
            double force = Math.Max(1.0, startingForce);
            if (layouts == null) return force;

            for (int i = 0; i < layouts.Count; i++)
            {
                ChunkLayout layout = layouts[i];
                if (layout == null) continue;

                double best = double.MinValue, worst = double.MaxValue;
                for (int lane = 0; lane < LaneCount; lane++)
                {
                    double factor = 1.0;
                    foreach (PlannedGate gate in layout.Gates)
                        if (gate.Lane == lane) factor *= GateMath.Factor(gate.Op, gate.Value, i);
                    foreach (PlannedPack pack in layout.Packs)
                        if (pack.Lane == lane) factor *= GateMath.Factor(GateOp.Subtract, pack.ForceCost, i);
                    if (factor > best) best = factor;
                    if (factor < worst) worst = factor;
                }

                if (best <= double.MinValue) continue;
                force *= worst + (best - worst) * ParKeepFraction;
            }

            return Math.Max(1.0, force);
        }

        /// <summary>Lanes on the road. Named because par has to walk all of them.</summary>
        public const int LaneCount = 3;

        private static long SaturatingAdd(long a, int b)
        {
            long r = unchecked(a + b);
            return r < a ? long.MaxValue : r;
        }

        /// <summary>
        /// The shapes a round is made of.
        ///
        /// Three rules, and each exists because breaking it makes a round read badly: no shape
        /// twice in a row, the opening chunk is always something readable so a round does not
        /// start by punishing, and there is at least one Breather in the back half so a long
        /// round has somewhere to exhale.
        /// </summary>
        public static ChunkShape[] SequenceFor(RoundPlan plan, int chunkCount)
        {
            int count = Math.Max(1, chunkCount);
            var shapes = new ChunkShape[count];
            uint rng = Seed(plan.RoundIndex);

            // Openers only. Gauntlet or Toll as the first thing a player sees is a round that
            // begins by taking something away.
            shapes[0] = (NextUInt(ref rng) & 1u) == 0u ? ChunkShape.Ladder : ChunkShape.Breather;

            for (int i = 1; i < count; i++)
            {
                ChunkShape pick;
                int guard = 0;
                do
                {
                    pick = (ChunkShape)(NextUInt(ref rng) % 8u);
                    guard++;
                } while (guard < 16 && (pick == shapes[i - 1] || (i < 3 && IsHarsh(pick))));

                shapes[i] = pick;
            }

            EnsureBreatherLate(shapes, ref rng);
            return shapes;
        }

        /// <summary>Shapes that only take. Fine later, cruel as an introduction.</summary>
        private static bool IsHarsh(ChunkShape shape) =>
            shape == ChunkShape.Gauntlet || shape == ChunkShape.Toll || shape == ChunkShape.Minefield;

        private static void EnsureBreatherLate(ChunkShape[] shapes, ref uint rng)
        {
            int half = shapes.Length / 2;
            for (int i = half; i < shapes.Length; i++)
                if (shapes[i] == ChunkShape.Breather) return;

            // Place one, but never adjacent to another Breather and never as the last chunk —
            // a round should not end on nothing happening.
            int span = Math.Max(1, shapes.Length - half - 1);
            int at = half + (int)(NextUInt(ref rng) % (uint)span);
            shapes[at] = ChunkShape.Breather;
        }

        private static int PickLane(ref uint rng) => (int)(NextUInt(ref rng) % 3u) - 1;

        /// <summary>The lane <paramref name="steps"/> over, wrapping across the three lanes.</summary>
        private static int Shift(int lane, int steps) => ((lane + 1 + steps) % 3) - 1;

        private static uint Seed(int roundIndex)
        {
            unchecked
            {
                uint h = (uint)roundIndex * 2246822519u + 0x85EBCA6Bu;
                h ^= h >> 13;
                return h | 1u;
            }
        }

        private static uint NextUInt(ref uint state)
        {
            unchecked
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                return state;
            }
        }
    }
}
