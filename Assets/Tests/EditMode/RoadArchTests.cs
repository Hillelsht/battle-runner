using System;
using System.Collections.Generic;
using BattleRunner.Core.Crowd;
using BattleRunner.Core.World;
using NUnit.Framework;

namespace BattleRunner.Tests
{
    /// <summary>
    /// The arches, and the one property that matters about them.
    ///
    /// An arch is the ONLY thing the scenery system puts over the road. Everything else is
    /// held to the side by a clearance rule, so nothing else can hurt the player by being in
    /// the wrong place; an arch is placed on the centreline on purpose and a leg half a metre
    /// too far in is a pillar standing in the middle lane.
    /// </summary>
    [TestFixture]
    public class RoadArchTests
    {
        private const float LaneWidth = 2.2f;

        [Test]
        public void TheDuplicatedRoadWidthAgreesWithTheOneTheGameUses()
        {
            // RoadArches keeps its own copy of the road half-width because a test needs a
            // constant to compare against and CrowdMath's version takes a lane width. A
            // duplicated constant that nothing checks is a constant that drifts.
            Assert.AreEqual(CrowdMath.RoadHalfWidth(LaneWidth), RoadArches.RoadHalfWidth, 1e-4f);
        }

        [Test]
        public void NoArchPutsAnythingOverTheRoadThePlayerRunsOn()
        {
            foreach (ArchStyle style in Enum.GetValues(typeof(ArchStyle)))
            {
                ArchShape a = RoadArches.For(style);
                Assert.GreaterOrEqual(a.LegInner, RoadArches.RoadHalfWidth + RoadArches.LegMargin,
                    $"{style}: a leg stands {a.LegInner:0.00} from the centreline and the road "
                    + $"reaches {RoadArches.RoadHalfWidth:0.00}");
                Assert.Greater(a.LegOuter, a.LegInner, $"{style}: a leg with no thickness");
            }
        }

        [Test]
        public void EverySpanClearsTheArmyAndTheCameraLine()
        {
            // 4.5 m is not about the army — a soldier is 1.5 and the hero is drawn at 1.35x.
            // It is about the CAMERA, which sits 5.5 m up and looks down the road: a span at
            // two metres would cut across the middle of the frame and hide the gates the
            // player is reading.
            foreach (ArchStyle style in Enum.GetValues(typeof(ArchStyle)))
            {
                ArchShape a = RoadArches.For(style);
                Assert.GreaterOrEqual(a.Clearance, RoadArches.MinClearance,
                    $"{style}: a span at {a.Clearance:0.00} m would sit across the play area");
                Assert.Greater(a.Crown, a.Clearance,
                    $"{style}: nothing rides on top of the span, so it has no silhouette");
                Assert.Greater(a.Depth, 0.2f, $"{style}: an arch this thin flickers past");
            }
        }

        [Test]
        public void EveryWorldSaysWhatCrossesOverItsRoad()
        {
            // The report was that worlds differ "pretty much only [in] colour", and the reason
            // the scenery could not answer it is structural: all three bands are beside the
            // road. A world with nothing overhead has no way to change the frame's outline.
            foreach (WorldTheme t in WorldThemes.All)
            {
                Assert.AreNotEqual(ArchStyle.None, t.Scenery.Arch,
                    $"{t.DisplayName} has nothing over its road, so its skyline is the same "
                    + "shape as every other world's");
                Assert.Greater(t.Scenery.ArchSpacing, 40f,
                    $"{t.DisplayName}: arches this close together are a tunnel");
                Assert.Less(t.Scenery.ArchSpacing, 260f,
                    $"{t.DisplayName}: at this spacing a 400 m round might hold one");
            }
        }

        [Test]
        public void NeighbouringWorldsDoNotShareASkyline()
        {
            // Sharing a style is fine and unavoidable with five styles over eight worlds —
            // but two worlds the player meets back to back must differ, or the change of act
            // shows them the same arch again at the same spacing.
            for (int i = 0; i < WorldThemes.Count; i++)
            {
                SceneryPalette a = WorldThemes.At(i).Scenery;
                SceneryPalette b = WorldThemes.At((i + 1) % WorldThemes.Count).Scenery;
                Assert.IsTrue(a.Arch != b.Arch || Math.Abs(a.ArchSpacing - b.ArchSpacing) > 20f,
                    $"{WorldThemes.At(i).DisplayName} and "
                    + $"{WorldThemes.At((i + 1) % WorldThemes.Count).DisplayName} run under the "
                    + "same arch at the same spacing");
            }
        }

        [Test]
        public void ARoundHoldsAFewArchesRatherThanNoneOrACorridor()
        {
            // 400 m is the authored round length. Two to five is the window: one is an
            // accident, six is architecture the player stops noticing.
            const float RoundMetres = 400f;
            foreach (WorldTheme t in WorldThemes.All)
            {
                int n = RoadArches.CountOver(RoundMetres, t.Scenery.ArchSpacing);
                Assert.GreaterOrEqual(n, 2, $"{t.DisplayName} holds {n} arches in a round");
                Assert.LessOrEqual(n, 5, $"{t.DisplayName} holds {n} arches in a round");
            }
        }

        [Test]
        public void ArchesAreSpacedEvenlyAndNeverOnTheStartLine()
        {
            // Offset by half a stride: an arch at z = 0 is the first thing on screen when a
            // round begins, which reads as a loading gate rather than as something you run
            // under.
            var seen = new List<float>();
            for (int i = 0; i < 4; i++) seen.Add(RoadArches.PositionOf(i, 100f));
            Assert.AreEqual(50f, seen[0], 1e-4f);
            for (int i = 1; i < seen.Count; i++)
                Assert.AreEqual(100f, seen[i] - seen[i - 1], 1e-4f);
        }

        [Test]
        public void AnAbsurdSpacingYieldsNoArchesRatherThanThrowing()
        {
            // The field walks this every time it dresses a round, and a world authored with a
            // zero would otherwise divide by it.
            Assert.AreEqual(0, RoadArches.CountOver(400f, 0f));
            Assert.AreEqual(0, RoadArches.CountOver(400f, -10f));
            Assert.AreEqual(0, RoadArches.CountOver(0f, 100f));
        }
    }
}
