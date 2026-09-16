using System;
using BattleRunner.Core.Run;
using NUnit.Framework;

namespace BattleRunner.Tests
{
    /// <summary>
    /// The reveal line. The property that matters most here is not that a number is latched —
    /// that is one line — but that the SIGN AND THE EFFECT ARE THE SAME NUMBER. Latching what
    /// is written on a gate while still applying a share of the live army is how last round's
    /// "+1 that adds nothing" was built, and it would be invisible until someone played it.
    /// </summary>
    [TestFixture]
    public class RevealTests
    {
        [Test]
        public void NothingHappensBeforeItHasCommitted()
        {
            Reveal hidden = Reveal.Hidden;
            Assert.IsFalse(hidden.Committed);
            Assert.AreEqual(0, hidden.Bodies(28), "an uncommitted crowd draws nobody");
            Assert.AreEqual(0.0, hidden.Gain(0.5));
            Assert.AreEqual(0.0, hidden.Loss(1000.0, 0f, false));
        }

        [Test]
        public void WhatIsWrittenOnTheSignIsWhatTheArmyMovesBy()
        {
            // THE TEST THE WHOLE FEATURE TURNS ON. The sign is StatFormat of Reveal.Men and
            // the delta is Gain or Loss of the same struct, so this pins that they agree at
            // every scale including the one where the weight floor is doing the work.
            foreach (double army in new[] { 5.0, 39.0, 400.0, 1e6, 1e12 })
                for (int w = 1; w <= 3; w++)
                {
                    Reveal add = Reveal.At(army, GateOp.Add, w, 0);
                    Assert.AreEqual(add.Men, add.Gain(0.0), 1e-9,
                        $"a +{add.Men} gate handed over something else at {army:N0} men");

                    Reveal sub = Reveal.At(army, GateOp.Subtract, w, 0);
                    Assert.AreEqual(-sub.Men, sub.Loss(army, 0f, false), 1e-9,
                        $"a {sub.Men} gate took something else at {army:N0} men");
                }
        }

        [Test]
        public void ItCommitsAgainstTheArmyThatSawItAndNotTheOneThatMeetsIt()
        {
            // The complaint, stated as arithmetic. A gate revealed to a thousand men gives the
            // same number of men to the ten thousand that arrive — that is what "let the next
            // one become bigger, not the one right in front of my eyes" asks for.
            Reveal seen = Reveal.At(1_000.0, GateOp.Add, 2, 0);
            Assert.AreEqual(seen.Men, seen.Gain(0.0), 1e-9);
            Assert.AreNotEqual(seen.Men, Reveal.At(10_000.0, GateOp.Add, 2, 0).Men,
                "if a tenfold army revealed the same number, there was nothing to latch");
        }

        [Test]
        public void AGateAlwaysMovesAnArmyItTouches()
        {
            // Inherited from GateMath.ApplyGate's weight floor rather than reimplemented,
            // which is the point of building Reveal on Headcount. From the seed of five, a
            // weight-1 recruit gate must hand over a man rather than 0.13 of one.
            for (int w = 1; w <= 3; w++)
            {
                Reveal add = Reveal.At(StandingArmy.Seed, GateOp.Add, w, 0);
                Assert.GreaterOrEqual(add.Men, w, $"a weight-{w} recruit gate added nothing");
                Reveal sub = Reveal.At(StandingArmy.Seed, GateOp.Subtract, w, 0);
                Assert.LessOrEqual(sub.Men, -1L, $"a weight-{w} ambush took nothing");
            }
        }

        [Test]
        public void ALatchedLossCanNeverOutrunTheArmyItFinallyMeets()
        {
            // The clamp that a latch makes necessary and a live share did not. The army can
            // SHRINK between the reveal and the contact — several ambushes in a row, a
            // champion's swings — and a number committed against the larger one would take
            // more men than exist.
            Reveal committed = Reveal.At(1e6, GateOp.Subtract, 3, 0);
            Assert.Greater(-committed.Men, 100.0, "the fixture is not testing what it claims");
            Assert.AreEqual(100.0, committed.Loss(100.0, 0f, false), 1e-9,
                "a latched ambush took more men than the army had left");
            Assert.AreEqual(0.0, committed.Loss(0.0, 0f, false));
        }

        [Test]
        public void ResistShavesTheBiteAndShatterRemovesItButNothingRoundsItAway()
        {
            Reveal r = Reveal.At(10_000.0, GateOp.Subtract, 2, 0);
            double full = r.Loss(10_000.0, 0f, false);
            Assert.AreEqual(full * 0.5, r.Loss(10_000.0, 0.5f, false), 1e-6);
            Assert.AreEqual(full * 0.15, r.Loss(10_000.0, 0.99f, false), full * 1e-6,
                "resist caps at 85%");
            Assert.AreEqual(0.0, r.Loss(10_000.0, 0f, true), "a shattered pack costs nothing");

            // Zero is reserved for the shatter. A pack that silently cost nothing because
            // resist rounded it out would teach the player the wrong rule about which of
            // their stats did it.
            Reveal tiny = Reveal.At(6.0, GateOp.Subtract, 1, 0);
            Assert.GreaterOrEqual(tiny.Loss(6.0, 0.85f, false), 1.0);
        }

        [Test]
        public void YieldScalesTheGainAndNeverTheLoss()
        {
            Reveal add = Reveal.At(1_000.0, GateOp.Add, 2, 0);
            Assert.AreEqual(add.Men * 1.2, add.Gain(0.2), 1e-9);
            Assert.AreEqual(add.Men * 1.0, add.Gain(-5.0), 1e-9, "a negative yield is not a tax");

            Reveal sub = Reveal.At(1_000.0, GateOp.Subtract, 2, 0);
            Assert.AreEqual(0.0, sub.Gain(5.0), "an ambush gate cannot be farmed with yield");
            Assert.AreEqual(0.0, add.Loss(1_000.0, 0f, false), "a recruit gate cannot bite");
        }

        [Test]
        public void TheCrowdFillsInAndNeverFlickers()
        {
            // The grow-in is by BODY COUNT, not by scale, and that is forced rather than
            // chosen: uniform scale is the only per-instance channel the crowd shader has and
            // it decodes the walk phase out of it. So this is the curve the count follows,
            // and the one property that matters is that it never goes backwards and never
            // returns to nothing once a thing is out.
            Assert.AreEqual(0f, Reveal.Grow(0f));
            Assert.AreEqual(1f, Reveal.Grow(Reveal.GrowSeconds));
            Assert.AreEqual(1f, Reveal.Grow(60f), "the grow-in has to finish");
            float previous = -1f;
            for (int i = 0; i <= 100; i++)
            {
                float g = Reveal.Grow(Reveal.GrowSeconds * i / 100f);
                Assert.GreaterOrEqual(g, previous, "the crowd shrank on its way in");
                Assert.LessOrEqual(g, 1f);
                previous = g;
            }
        }

        [Test]
        public void TheRevealLineIsInsideTheRoadThePlayerCanSee()
        {
            // 34 m, and the constant is shared with the sign's visibility distance so that one
            // number governs the sign, the bodies and the commit. It has to be shorter than a
            // chunk, or a thing would commit before the previous chunk's decisions were made
            // and the lag would span two rounds of steering rather than one.
            Assert.Less(Reveal.LineMeters, ChunkLayouts.ChunkMeters,
                "a reveal line longer than a chunk commits across chunk boundaries");
            Assert.Greater(Reveal.LineMeters, 20f, "too close to read before it arrives");
            Assert.Greater(Reveal.GrowSeconds, 0f);
            Assert.Less(Reveal.GrowSeconds, 1f, "a slow grow-in is a size change with manners");
        }
    }
}
