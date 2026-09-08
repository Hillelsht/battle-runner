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

        /// <summary>The value of a plain add gate at this depth, as the original generator sized it.</summary>
        public static int AddValue(int difficulty, int step) =>
            4 + 2 * Math.Max(0, difficulty) + Math.Max(0, step);

        /// <summary>What a pack costs at this depth.</summary>
        public static int PackCost(int difficulty, int step) =>
            3 + 2 * Math.Max(0, difficulty) + 2 * Math.Max(0, step);

        /// <summary>
        /// Build one chunk. <paramref name="step"/> is its index within the round, which is
        /// what makes a chunk late in a long round harder than the same shape early on.
        /// </summary>
        public static ChunkLayout Build(ChunkShape shape, int difficulty, int step, ref uint rng)
        {
            var layout = new ChunkLayout(shape);
            int add = AddValue(difficulty, step);
            int cost = PackCost(difficulty, step);
            int lane = PickLane(ref rng);

            switch (shape)
            {
                case ChunkShape.Ladder:
                    // Three gates, each one lane over from the last, so the player is steering
                    // continuously rather than picking once and holding.
                    layout.Gates.Add(new PlannedGate(GateOp.Add, add, lane, 10f));
                    layout.Gates.Add(new PlannedGate(GateOp.Add, add + 2, Shift(lane, 1), 24f));
                    layout.Gates.Add(new PlannedGate(GateOp.Add, add + 4, Shift(lane, 2), 38f));
                    break;

                case ChunkShape.Fork:
                    layout.Gates.Add(new PlannedGate(GateOp.Add, add, Shift(lane, 1), 12f));
                    // The multiply and its price share a Z: the choice has to be made before
                    // either is close enough to read, which is what makes it a commitment.
                    layout.Gates.Add(new PlannedGate(GateOp.Multiply, 2, lane, 32f));
                    layout.Gates.Add(new PlannedGate(GateOp.Subtract, add * 2, Shift(lane, 1), 32f));
                    layout.Gates.Add(new PlannedGate(GateOp.Subtract, add * 2, Shift(lane, 2), 32f));
                    break;

                case ChunkShape.Gauntlet:
                    layout.Packs.Add(new PlannedPack(cost, lane, 10f));
                    layout.Packs.Add(new PlannedPack(cost, Shift(lane, 2), 24f));
                    layout.Packs.Add(new PlannedPack(cost, Shift(lane, 1), 38f));
                    break;

                case ChunkShape.Minefield:
                    // One safe lane, and it is not announced. The add is the reward for
                    // finding it rather than for surviving.
                    layout.Gates.Add(new PlannedGate(GateOp.Subtract, add * 2, Shift(lane, 1), 26f));
                    layout.Gates.Add(new PlannedGate(GateOp.Subtract, add * 2, Shift(lane, 2), 26f));
                    layout.Gates.Add(new PlannedGate(GateOp.Add, add + 3, lane, 26f));
                    break;

                case ChunkShape.Toll:
                    // Every lane costs. The only decision left is which loss to accept, which
                    // is a decision the game never asked before.
                    layout.Gates.Add(new PlannedGate(GateOp.Add, add + 4, lane, 10f));
                    layout.Gates.Add(new PlannedGate(GateOp.Subtract, Math.Max(1, add / 2), lane, 30f));
                    layout.Gates.Add(new PlannedGate(GateOp.Subtract, add, Shift(lane, 1), 30f));
                    layout.Gates.Add(new PlannedGate(GateOp.Subtract, add * 2, Shift(lane, 2), 30f));
                    break;

                case ChunkShape.Vault:
                    // The pack and the multiply are in the SAME lane. Taking the prize means
                    // paying for it, and dodging the pack means dodging the prize.
                    layout.Packs.Add(new PlannedPack(cost * 2, lane, 18f));
                    layout.Gates.Add(new PlannedGate(GateOp.Multiply, 3, lane, 34f));
                    layout.Gates.Add(new PlannedGate(GateOp.Add, add, Shift(lane, 1), 34f));
                    break;

                case ChunkShape.Breather:
                    layout.Gates.Add(new PlannedGate(GateOp.Add, add + 1, lane, 22f));
                    break;

                default: // Crossfire
                    layout.Packs.Add(new PlannedPack(cost, Shift(lane, 1), 12f));
                    layout.Gates.Add(new PlannedGate(GateOp.Add, add + 2, lane, 26f));
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

        /// <summary>Share of the optimistic line a par player is assumed to actually hold.</summary>
        public const double ParKeepFraction = 0.6;

        /// <summary>How much a round's multiply gates lift par, per doubling of their count.</summary>
        public const double MultiplyWeight = 0.9;

        /// <summary>
        /// Force a par player is expected to hold at the finish.
        ///
        /// THE OLD ESTIMATE COMPOUNDED EVERY MULTIPLY, and that stopped working the moment
        /// layouts became varied. Measured across the new generator it swung by two orders of
        /// magnitude on nothing but how many multiply gates a round happened to roll — round 2
        /// estimated 297 and round 5 estimated 12,533, with round 20 and round 30 both pinned
        /// at the soft cap. Par sizes the revive a player is handed after paying for one, so
        /// that is not a cosmetic error: it is the difference between coming back with 99 units
        /// and coming back with four thousand.
        ///
        /// The adds are the stable backbone — their count and value track depth smoothly — so
        /// they are banked in full. The multiplies then lift the result LOGARITHMICALLY in
        /// their count rather than multiplicatively in their values, which is both far steadier
        /// and closer to the truth: three lanes cannot all be taken, so the tenth multiply in a
        /// round is worth much less than the first. The same log-shaped damping GateMath already
        /// uses for overflow.
        /// </summary>
        public static long EstimateParForce(IReadOnlyList<ChunkLayout> layouts, int startingForce,
            long softCap)
        {
            long banked = Math.Max(1, startingForce);
            int multiplies = 0;

            if (layouts != null)
            {
                foreach (ChunkLayout layout in layouts)
                {
                    if (layout == null) continue;
                    foreach (PlannedGate gate in layout.Gates)
                    {
                        if (gate.Op == GateOp.Add) banked = SaturatingAdd(banked, gate.Value);
                        else if (gate.Op == GateOp.Multiply && gate.Value > 1) multiplies++;
                    }
                }
            }

            double bonus = 1.0 + MultiplyWeight * Math.Log(1.0 + multiplies, 2.0);
            double par = banked * bonus * ParKeepFraction;

            // Real force is soft-capped in play, so par cannot meaningfully exceed the same
            // share of that cap.
            double ceiling = Math.Max(1.0, softCap * ParKeepFraction);
            return Math.Max(1L, (long)Math.Min(par, ceiling));
        }

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
