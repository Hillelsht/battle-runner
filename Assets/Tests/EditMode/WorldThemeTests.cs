using System.Collections.Generic;
using BattleRunner.Core.Progression;
using BattleRunner.Core.World;
using NUnit.Framework;

namespace BattleRunner.Tests
{
    /// <summary>
    /// The eight worlds. None of this can be seen from CI, so what can be checked is checked:
    /// that the baseline still reproduces the one palette that has actually been judged on a
    /// screen, that no world breaks the couplings the render stack depends on, and that the
    /// eight are genuinely different from each other rather than eight names for one look.
    /// </summary>
    [TestFixture]
    public class WorldThemeTests
    {
        private static IReadOnlyList<WorldTheme> All => WorldThemes.All;

        [Test]
        public void ThereIsExactlyOneWorldPerThemeSlot()
        {
            // RoundPlan hands out slots modulo ThemeCount. If the table were shorter, a slot
            // would wrap onto a world the act arithmetic thinks is distinct.
            Assert.AreEqual(RoundPlan.ThemeCount, WorldThemes.Count);
        }

        [Test]
        public void TheBaselineStillReproducesTheShippedLook()
        {
            // Slot 0 is the palette from the device screenshots. It is the only one that has
            // been seen and judged, so it is the anchor the other seven are pushed away from
            // and it must not drift by accident.
            WorldTheme t = WorldThemes.At(0);
            Assert.AreEqual("The Ashen Road", t.DisplayName);
            Assert.AreEqual(0.310f, t.RoadStone.R, 1e-4f);
            Assert.AreEqual(1.60f, t.RoadTiling, 1e-4f);
            Assert.AreEqual(0.55f, t.RoadWetness, 1e-4f);
            Assert.AreEqual(0.022f, t.SkyZenith.R, 1e-4f);
            Assert.AreEqual(0.125f, t.SkyHorizon.B, 1e-4f);
            Assert.AreEqual(1.6f, t.SkyStars, 1e-4f);
            Assert.AreEqual(0.440f, t.Fog.R, 1e-4f);
            Assert.AreEqual(70f, t.FogStart, 1e-4f);
            Assert.AreEqual(170f, t.FogEnd, 1e-4f);
            Assert.AreEqual(0.950f, t.LightColor.B, 1e-4f);
            Assert.AreEqual(250f, t.LightYaw, 1e-4f);
        }

        [Test]
        public void NoWorldsFogWandersFarFromItsOwnSky()
        {
            // docs/10-look.md: fog tracks the sky's horizon plus its ember glow. Red and green
            // fit a single factor to within 0.005 on the shipped world; blue deliberately does
            // not. A world that broke the red/green relationship would draw a visible seam
            // where the road meets the sky.
            foreach (WorldTheme t in All)
                Assert.Less(t.FogDrift, 0.02f,
                    $"{t.DisplayName}: fog {t.Fog} is too far from sky-derived {t.FogFromSky}");
        }

        [Test]
        public void TheBaselinesFogDriftIsPractricallyZero()
        {
            Assert.Less(WorldThemes.At(0).FogDrift, 0.006f,
                "the shipped values are what the 0.70 factor was solved from");
        }

        [Test]
        public void NoWorldFogsPastTheEndOfTheRoad()
        {
            // The ground strip is built to run past MaxFogEnd and the camera's far clip past
            // that again. A world whose fog ended beyond the road would show the player the
            // edge of the world instead of hiding it.
            foreach (WorldTheme t in All)
            {
                Assert.Greater(t.FogStart, 0f, t.DisplayName);
                Assert.Greater(t.FogEnd, t.FogStart, t.DisplayName);
                Assert.LessOrEqual(t.FogEnd, WorldThemes.MaxFogEnd, t.DisplayName);
                Assert.GreaterOrEqual(t.FogEnd - t.FogStart, 40f,
                    $"{t.DisplayName}: a fog band this narrow reads as a wall, not weather");
            }
        }

        [Test]
        public void EveryWorldIsDressed()
        {
            foreach (WorldTheme t in All)
            {
                Assert.IsNotNull(t.DisplayName);
                Assert.IsNotEmpty(t.DisplayName, "a world the player is told the name of");
                Assert.GreaterOrEqual(t.Props.Length, 3,
                    $"{t.DisplayName}: fewer than three prop kinds and the verge repeats visibly");
                Assert.Greater(t.PropDensity, 0f, t.DisplayName);
            }
        }

        [Test]
        public void LaneMarkingsStayBrighterThanTheStoneTheySitOn()
        {
            // A real regression once: the markings were overcorrected to ~0.9x the road and
            // the lanes stopped reading on the dark early stretch. Whatever a world does to
            // its stone, the lines have to stay above it.
            foreach (WorldTheme t in All)
            {
                float stone = Luma(t.RoadStone);
                float marking = Luma(t.MarkingEmission);
                Assert.Greater(marking, stone * 1.25f,
                    $"{t.DisplayName}: markings {t.MarkingEmission} vs stone {t.RoadStone}");
            }
        }

        [Test]
        public void NoWorldPaintsItsRoadAlmostBlack()
        {
            // The Bone Colossus rendered as a #3a3a3a cutout because its albedo landed at 5%
            // reflectance. The road is the largest surface on screen and the same trap applies.
            foreach (WorldTheme t in All)
                Assert.Greater(Luma(t.RoadStone), 0.12f,
                    $"{t.DisplayName}: road stone {t.RoadStone} is too dark to light");
        }

        [Test]
        public void EveryColourIsPositive()
        {
            foreach (WorldTheme t in All)
            foreach (Rgb c in new[]
                     {
                         t.RoadStone, t.RoadMortar, t.RoadDamp, t.SkyZenith, t.SkyHorizon,
                         t.SkyGlow, t.Fog, t.LightColor, t.PropStone, t.Accent,
                         t.SkyGround, t.RailBase, t.RailEmission, t.MarkingBase,
                         t.MarkingEmission, t.AmbientSky, t.AmbientEquator, t.AmbientGround
                     })
            {
                Assert.GreaterOrEqual(c.R, 0f, t.DisplayName);
                Assert.GreaterOrEqual(c.G, 0f, t.DisplayName);
                Assert.GreaterOrEqual(c.B, 0f, t.DisplayName);
            }
        }

        [Test]
        public void TheEightWorldsAreActuallyDifferentFromEachOther()
        {
            // The point of the whole exercise. Two worlds that differ only in name would be
            // the shipped bug with extra steps.
            for (int a = 0; a < WorldThemes.Count; a++)
            for (int b = a + 1; b < WorldThemes.Count; b++)
            {
                WorldTheme x = WorldThemes.At(a);
                WorldTheme y = WorldThemes.At(b);
                float separation = Distance(x.RoadStone, y.RoadStone)
                                   + Distance(x.SkyGlow, y.SkyGlow)
                                   + Distance(x.Fog, y.Fog);
                Assert.Greater(separation, 0.25f,
                    $"{x.DisplayName} and {y.DisplayName} look like the same place");
            }
        }

        [Test]
        public void WeatherVariesAcrossTheRoster()
        {
            // Not every world may be a clear night. Somewhere has to be thick and somewhere
            // has to be wide open, or the fog stops carrying information.
            float thinnest = float.MinValue, thickest = float.MaxValue;
            foreach (WorldTheme t in All)
            {
                if (t.FogEnd > thinnest) thinnest = t.FogEnd;
                if (t.FogEnd < thickest) thickest = t.FogEnd;
            }
            Assert.Greater(thinnest - thickest, 60f, "every world has the same visibility");
        }

        [Test]
        public void SlotLookupWrapsAndNeverThrows()
        {
            Assert.AreSame(WorldThemes.At(0), WorldThemes.At(WorldThemes.Count));
            Assert.AreSame(WorldThemes.At(1), WorldThemes.At(-WorldThemes.Count + 1));
            Assert.IsNotNull(WorldThemes.At(-1));
            Assert.IsNotNull(WorldThemes.At(int.MaxValue));
        }

        [Test]
        public void AnActWearsOneWorldAndTheNextActWearsAnother()
        {
            WorldTheme first = WorldThemes.For(RoundPlan.For(0));
            WorldTheme sameAct = WorldThemes.For(RoundPlan.For(1));
            WorldTheme nextAct = WorldThemes.For(RoundPlan.For(2));

            Assert.AreSame(first, sameAct, "an act keeps its world so arriving somewhere matters");
            Assert.AreNotSame(first, nextAct, "and a new act is somewhere new");
        }

        private static float Luma(Rgb c) => 0.2126f * c.R + 0.7152f * c.G + 0.0722f * c.B;

        private static float Distance(Rgb a, Rgb b)
        {
            float dr = a.R - b.R, dg = a.G - b.G, db = a.B - b.B;
            return (float)System.Math.Sqrt(dr * dr + dg * dg + db * db);
        }
    }
}
