using System;
using System.Collections.Generic;
using BattleRunner.Core.World;
using NUnit.Framework;

namespace BattleRunner.Tests
{
    /// <summary>
    /// The ground textures, and the arithmetic that decides how big they are.
    ///
    /// Nothing here can be looked at from CI, so what is pinned is what would go wrong
    /// SILENTLY: a world with no surface, two worlds paved with the same one, a tile scale
    /// that either aliases into moire or shows its repeat, and a road that is the same
    /// texture as the land it runs through — which is the "every round looks the same"
    /// complaint expressed in the one part of the frame that fills the bottom half of it.
    /// </summary>
    [TestFixture]
    public class SurfaceTests
    {
        /// <summary>
        /// A tile repeat closer than this and the sampler starts aliasing on a surface seen
        /// almost edge-on; further and the repeat itself becomes the pattern you see.
        /// </summary>
        private const float MinTilePeriodMetres = 1.5f;
        private const float MaxTilePeriodMetres = 12f;

        [Test]
        public void EveryWorldIsPavedAndEveryWorldHasLand()
        {
            foreach (WorldTheme t in WorldThemes.All)
            {
                Assert.IsNotNull(t.Surface, $"{t.DisplayName} has no road surface");
                Assert.IsNotNull(t.GroundSurface, $"{t.DisplayName} has no ground surface");
                CollectionAssert.Contains(RoadSurfaces.All, t.Surface, t.DisplayName);
                CollectionAssert.Contains(RoadSurfaces.All, t.GroundSurface, t.DisplayName);
            }
        }

        [Test]
        public void NoTwoWorldsArePavedWithTheSameSurface()
        {
            // This is the complaint, stated as an assertion. Eight worlds sharing one road
            // texture is eight worlds with a colour filter over them, which is exactly what
            // shipped and exactly what was called out.
            var seen = new Dictionary<string, string>();
            foreach (WorldTheme t in WorldThemes.All)
            {
                Assert.IsFalse(seen.ContainsKey(t.Surface.Name),
                    $"{t.DisplayName} and {(seen.ContainsKey(t.Surface.Name) ? seen[t.Surface.Name] : "")} "
                    + $"are both paved with {t.Surface.Name}");
                seen[t.Surface.Name] = t.DisplayName;
            }
            Assert.AreEqual(RoadSurfaces.All.Length, seen.Count,
                "a generated surface exists that no world uses");
        }

        [Test]
        public void TheRoadIsNeverMadeOfTheSameStuffAsTheLand()
        {
            // The kerb is the one edge in the frame the eye is guaranteed to find, and the
            // same texture either side of it erases the road. The colours differ, but colour
            // alone is what "only the colours change" already meant.
            foreach (WorldTheme t in WorldThemes.All)
                Assert.AreNotSame(t.Surface, t.GroundSurface,
                    $"{t.DisplayName}: the road and the verge are both {t.Surface.Name}, "
                    + "so the kerb has nothing to read against");
        }

        [Test]
        public void EveryTileRepeatsAtAScaleThatIsNeitherMoireNorWallpaper()
        {
            // The conversion from authored features-per-metre to tile-repeats-per-metre is
            // the one piece of arithmetic between a tuned number and what is sampled, and
            // getting it wrong produces no error at all — just the wrong scale. Four worlds
            // failed this on the first pass, because their RoadTiling had been authored
            // against a procedural cobble grid where a "feature" was always a cobble.
            foreach (WorldTheme t in WorldThemes.All)
            {
                float roadPeriod = 1f / t.Surface.TileRepeatsPerMetre(t.RoadTiling);
                Assert.GreaterOrEqual(roadPeriod, MinTilePeriodMetres,
                    $"{t.DisplayName}: the road tile repeats every {roadPeriod:F2} m and would alias");
                Assert.LessOrEqual(roadPeriod, MaxTilePeriodMetres,
                    $"{t.DisplayName}: the road tile is {roadPeriod:F2} m across and would read as wallpaper");

                float groundPeriod = 1f / t.GroundSurfaceTiling;
                Assert.GreaterOrEqual(groundPeriod, MinTilePeriodMetres, t.DisplayName);
                Assert.LessOrEqual(groundPeriod, MaxTilePeriodMetres, t.DisplayName);
            }
        }

        [Test]
        public void TheJointPatternIsNotIdenticalInEveryWorld()
        {
            // WorldTheme.RoadMortarWidth was DECLARED and never once overridden: all eight
            // worlds sat on the same 0.075, so the joint pattern was byte-identical
            // everywhere. That is a measurable part of why the pavement "doesn't change".
            var widths = new HashSet<float>();
            foreach (WorldTheme t in WorldThemes.All)
            {
                Assert.Greater(t.RoadMortarWidth, 0f, t.DisplayName);
                // Past the shader's own Range(0.01, 0.3), and past the point where the
                // smoothstep window swallows the whole face mask and the road is all mortar.
                Assert.LessOrEqual(t.RoadMortarWidth, 0.3f, t.DisplayName);
                widths.Add(t.RoadMortarWidth);
            }
            Assert.GreaterOrEqual(widths.Count, 6,
                "the joints are cut the same in nearly every world");
        }

        [Test]
        public void TheJointIsSeparatedInValueFromTheSurfaceItCutsInto()
        {
            // NOT "the joint is darker". Throne of Dust deliberately puts gold in its seams,
            // and a bright joint on dark tesserae is a real look. What is never a look is a
            // joint the same VALUE as the stone: the pattern then exists only in hue, the
            // luminance structure the eye actually reads is flat, and the road measures as a
            // slab no matter how much detail the texture carries. Throne of Dust shipped at
            // stone 0.153 against mortar 0.212 — a 28% separation that, combined with a mask
            // putting a third of the road in the joint, measured WORSE than the untextured
            // road it replaced.
            foreach (WorldTheme t in WorldThemes.All)
            {
                float stone = Luma(t.RoadStone);
                float mortar = Luma(t.RoadMortar);
                float separation = Math.Abs(stone - mortar) / Math.Max(stone, mortar);
                Assert.Greater(separation, 0.30f,
                    $"{t.DisplayName}: stone {t.RoadStone} and mortar {t.RoadMortar} are "
                    + $"{separation:P0} apart in value, so the joint pattern does not read");
            }
        }

        /// <summary>Rec. 709 luma. The same weighting the rest of the theme suite uses.</summary>
        private static float Luma(Rgb c) => 0.2126f * c.R + 0.7152f * c.G + 0.0722f * c.B;

        [Test]
        public void SurfacePathsAreLoadableResourcePaths()
        {
            // Resources.Load takes a path with no extension and no leading slash. A name with
            // either loads null at runtime, and the road silently falls back to a flat slab.
            foreach (string path in RoadSurfaces.AllResourcePaths())
            {
                Assert.IsFalse(path.Contains("."), $"'{path}' carries an extension");
                Assert.IsFalse(path.StartsWith("/"), $"'{path}' starts with a slash");
                Assert.IsFalse(path.Contains(" "), $"'{path}' has a space in it");
                StringAssert.StartsWith(RoadSurfaces.ResourceFolder, path);
            }
            Assert.AreEqual(RoadSurfaces.All.Length * 2, RoadSurfaces.AllResourcePaths().Length);
        }

        [Test]
        public void EverySurfaceIsShadedInsideWhatTheShaderAccepts()
        {
            // _NormalStrength and _Cavity are both Range(0, 1) in the shader. A value outside
            // that is clamped on assignment rather than rejected, so it fails as a look that
            // is quietly not the authored one.
            var names = new HashSet<string>();
            foreach (RoadSurface s in RoadSurfaces.All)
            {
                Assert.IsTrue(names.Add(s.Name), $"two surfaces are both called {s.Name}");
                Assert.Greater(s.FeaturesPerTile, 0f, s.Name);
                Assert.GreaterOrEqual(s.NormalStrength, 0f, s.Name);
                Assert.LessOrEqual(s.NormalStrength, 1f, s.Name);
                Assert.GreaterOrEqual(s.Cavity, 0f, s.Name);
                Assert.LessOrEqual(s.Cavity, 1f, s.Name);
            }
        }

        [Test]
        public void TheTileScaleClampsRatherThanProducingNonsense()
        {
            // A theme with a mistyped tiling must land on a rail, not produce a UV multiplier
            // of zero (one texel stretched across the level) or of a thousand (pure moire).
            RoadSurface cobble = RoadSurfaces.Cobble;
            Assert.AreEqual(0.01f, cobble.TileRepeatsPerMetre(0f), 1e-6f);
            Assert.AreEqual(0.01f, cobble.TileRepeatsPerMetre(-5f), 1e-6f);
            Assert.AreEqual(4f, cobble.TileRepeatsPerMetre(10000f), 1e-6f);
            // And that it is a plain division in between.
            Assert.AreEqual(1.6f / 9f, cobble.TileRepeatsPerMetre(1.6f), 1e-6f);
        }

        [Test]
        public void EveryRoadFeatureIsBigEnoughToSurviveTheTripToTheEye()
        {
            // THE TEST THAT WOULD HAVE CAUGHT THE LAST TWO ROAD PASSES.
            //
            // A feature on the ground is 1 / RoadTiling metres across. One screen pixel
            // covers about 15 cm of road at 25 m, on a 60-degree portrait camera 5.5 m up —
            // so a world authored at RoadTiling 9 has 11 cm gravel, which is SMALLER than a
            // pixel in the middle distance and can only average to flat no matter what the
            // texture contains. Three worlds shipped that way, and their generated surfaces
            // measured beautifully as files while arriving on screen as grey slabs.
            //
            // Two pixels is the floor: below that there is nothing for the eye to lock onto,
            // and the whole per-stone half of the surface is wasted instructions.
            const float PixelMetresAt25M = 0.15f;
            foreach (WorldTheme t in WorldThemes.All)
            {
                float feature = 1f / t.RoadTiling;
                Assert.GreaterOrEqual(feature, PixelMetresAt25M * 2f,
                    $"{t.DisplayName}: RoadTiling {t.RoadTiling} puts a feature at "
                    + $"{feature * 100f:F0} cm, under two pixels of road at 25 m");
                // And the other end: a 3 m "cobble" is a slab with a crack in it.
                Assert.LessOrEqual(feature, 1.5f,
                    $"{t.DisplayName}: RoadTiling {t.RoadTiling} puts a feature at "
                    + $"{feature:F2} m, which is not a paving unit");
            }
        }

        [Test]
        public void TheGrimeContrastIsInsideTheShaderRangeAndActuallyVaries()
        {
            // The largest single term in the road's on-screen contrast: modelled through the
            // camera, restoring the old hard-coded lerp(0.72, 1.12) costs 34% of the
            // luminance range. It was fixed in the shader, so all eight worlds had the same
            // one, and it is the term that SURVIVES minification — the per-stone tone does
            // not. Range(0, 0.6) in Road.shader, and Unity clamps silently rather than
            // complaining, which is how three surfaces once shipped with a normal strength
            // of 1.15.
            var seen = new HashSet<float>();
            foreach (WorldTheme t in WorldThemes.All)
            {
                Assert.GreaterOrEqual(t.RoadGrimeContrast, 0f, t.DisplayName);
                Assert.LessOrEqual(t.RoadGrimeContrast, 0.6f,
                    $"{t.DisplayName}: {t.RoadGrimeContrast} is past Road.shader's "
                    + "Range(0, 0.6) and would be clamped with nothing said");
                seen.Add(t.RoadGrimeContrast);
            }
            Assert.GreaterOrEqual(seen.Count, 6,
                "the grime reads the same in nearly every world, which is what it did when "
                + "it was a constant in the shader");
        }

        [Test]
        public void ASurfaceCannotBeBuiltWithNoFeaturesInIt()
        {
            // FeaturesPerTile is a divisor. Zero is not a look, it is a crash or an infinity.
            Assert.Throws<ArgumentOutOfRangeException>(() => new RoadSurface("x", 0f, 1f, 0.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RoadSurface("x", -3f, 1f, 0.5f));
            Assert.Throws<ArgumentException>(() => new RoadSurface("", 9f, 1f, 0.5f));
            // The shader declares both blend weights Range(0, 1) and Unity CLAMPS rather than
            // rejecting, so 1.15 would have shipped as 1.0 with nothing said. Three of the
            // eight surfaces were authored that way on the first pass.
            Assert.Throws<ArgumentOutOfRangeException>(() => new RoadSurface("x", 9f, 1.15f, 0.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RoadSurface("x", 9f, -0.1f, 0.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RoadSurface("x", 9f, 1f, 1.4f));
        }
    }
}
