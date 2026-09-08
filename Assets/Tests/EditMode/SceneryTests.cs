using System.Collections.Generic;
using BattleRunner.Core.Art;
using BattleRunner.Core.World;
using NUnit.Framework;

namespace BattleRunner.Tests
{
    /// <summary>
    /// The imported scenery, checked without an engine and without the binary.
    ///
    /// `tooling/fetch_scenery.py` generates `SceneryPieces` alongside the pack, so the names
    /// a palette or a landmark reference can be proved to exist here rather than discovered
    /// as an invisible hole in the world on a device. A stale name is the failure mode this
    /// whole file exists for: it compiles, it draws nothing, and nothing says why.
    /// </summary>
    [TestFixture]
    public class SceneryTests
    {
        private static readonly HashSet<string> Known = new HashSet<string>(SceneryPieces.Names);

        [Test]
        public void ThePieceTableIsInternallyConsistent()
        {
            Assert.Greater(SceneryPieces.Count, 0, "no pieces were baked");
            Assert.AreEqual(SceneryPieces.Count, SceneryPieces.Zones.Length);
            Assert.AreEqual(SceneryPieces.Count, SceneryPieces.Heights.Length);
            CollectionAssert.AllItemsAreUnique(SceneryPieces.Names);
            for (int i = 0; i < SceneryPieces.Count; i++)
            {
                Assert.AreEqual(i, SceneryPieces.IndexOf(SceneryPieces.Names[i]));
                Assert.Greater(SceneryPieces.Heights[i], 0f, SceneryPieces.Names[i]);
            }
            Assert.AreEqual(-1, SceneryPieces.IndexOf("no-such-piece"));
        }

        [Test]
        public void EveryZoneHasSomethingInIt()
        {
            int verge = 0, field = 0, mark = 0;
            foreach (SceneryZone z in SceneryPieces.Zones)
            {
                if (z == SceneryZone.Verge) verge++;
                else if (z == SceneryZone.Field) field++;
                else mark++;
            }
            Assert.Greater(verge, 8, "not enough verge pieces to avoid obvious repetition");
            Assert.Greater(field, 8, "not enough field pieces");
            Assert.Greater(mark, 8, "not enough landmark pieces");
        }

        [Test]
        public void EveryLandmarkIsBuiltFromPiecesThatExist()
        {
            Assert.Greater(Landmarks.All.Length, 0);
            foreach (Landmark l in Landmarks.All)
            {
                Assert.Greater(l.Parts.Length, 0, $"{l.Name} has no parts");
                Assert.Greater(l.Radius, 0f, $"{l.Name} has no footprint");
                foreach (LandmarkPart part in l.Parts)
                    Assert.IsTrue(Known.Contains(part.Piece),
                        $"{l.Name} references '{part.Piece}', which is not in the pack");
            }
        }

        [Test]
        public void LandmarkNamesAreUniqueAndLookUpBothWays()
        {
            var names = new HashSet<string>();
            foreach (Landmark l in Landmarks.All)
            {
                Assert.IsTrue(names.Add(l.Name), $"two landmarks are called {l.Name}");
                Assert.AreSame(l, Landmarks.ByName(l.Name));
            }
            Assert.IsNull(Landmarks.ByName("nothing"));
        }

        [Test]
        public void NoLandmarkPartStandsWhereItsOwnFootprintSaysItDoesNot()
        {
            // Radius is what the field uses to keep a landmark clear of the road. A part
            // outside it would put a castle wall through the lane the player is running in,
            // and the first anyone would know is a screenshot of a wall across the track.
            foreach (Landmark l in Landmarks.All)
            foreach (LandmarkPart part in l.Parts)
            {
                float reach = System.Math.Max(System.Math.Abs(part.X), System.Math.Abs(part.Z))
                              + 0.5f * part.Scale;
                Assert.LessOrEqual(reach, l.Radius + 1e-3f,
                    $"{l.Name}: '{part.Piece}' reaches {reach:0.00} past a radius of {l.Radius:0.00}");
            }
        }

        [Test]
        public void LandmarkPartsAreStackedOnEachOtherRatherThanFloating()
        {
            // A tower is three modules stacked by hand from measured heights. If a course is
            // placed at the wrong y it either floats or intersects, and both read as a bug
            // rather than as a building.
            foreach (Landmark l in Landmarks.All)
            foreach (LandmarkPart part in l.Parts)
            {
                Assert.GreaterOrEqual(part.Y, 0f, $"{l.Name}: '{part.Piece}' is underground");
                Assert.Greater(part.Scale, 0f, $"{l.Name}: '{part.Piece}' has no scale");
                if (part.Y <= 0f) continue;

                // Anything raised must be sitting on something: some other part of this
                // landmark has to reach at least as high as this one's base.
                float support = 0f;
                foreach (LandmarkPart other in l.Parts)
                {
                    if (other.Y >= part.Y) continue;
                    float top = other.Y + SceneryPieces.Heights[SceneryPieces.IndexOf(other.Piece)]
                                * other.Scale;
                    if (top > support) support = top;
                }
                Assert.GreaterOrEqual(support, part.Y - 0.05f,
                    $"{l.Name}: '{part.Piece}' at y={part.Y:0.00} floats above a top of {support:0.00}");
            }
        }

        [Test]
        public void EveryWorldIsFullyDressed()
        {
            foreach (WorldTheme t in WorldThemes.All)
            {
                SceneryPalette s = t.Scenery;
                Assert.IsTrue(s.IsComplete, $"{t.DisplayName} has an empty scenery band");
                Assert.GreaterOrEqual(s.Verge.Length, 5,
                    $"{t.DisplayName} verge repeats too soon");
                Assert.GreaterOrEqual(s.Field.Length, 5, $"{t.DisplayName} field repeats too soon");
                Assert.GreaterOrEqual(s.Landmarks.Length, 2,
                    $"{t.DisplayName} would show the same landmark all round");

                foreach (string piece in s.Verge)
                    Assert.IsTrue(Known.Contains(piece), $"{t.DisplayName} verge: '{piece}'");
                foreach (string piece in s.Field)
                    Assert.IsTrue(Known.Contains(piece), $"{t.DisplayName} field: '{piece}'");
                foreach (string name in s.Landmarks)
                    Assert.IsNotNull(Landmarks.ByName(name),
                        $"{t.DisplayName} wants a landmark called '{name}'");
            }
        }

        [Test]
        public void PiecesAreDrawnInTheBandTheyWereBakedFor()
        {
            // The zones are a budget, not a label: a castle tower in the verge would be a wall
            // two metres from the player's shoulder, and a gravestone at 80 m is invisible.
            foreach (WorldTheme t in WorldThemes.All)
            {
                foreach (string piece in t.Scenery.Verge)
                    Assert.AreEqual(SceneryZone.Verge,
                        SceneryPieces.Zones[SceneryPieces.IndexOf(piece)],
                        $"{t.DisplayName} puts '{piece}' on the verge");
                foreach (string piece in t.Scenery.Field)
                    Assert.AreEqual(SceneryZone.Field,
                        SceneryPieces.Zones[SceneryPieces.IndexOf(piece)],
                        $"{t.DisplayName} puts '{piece}' in the field");
            }
        }

        [Test]
        public void NoTwoWorldsAreDressedTheSameWay()
        {
            // The complaint this whole increment answers. Two worlds drawing the same pieces
            // in the same bands are the shipped bug with new file names.
            for (int a = 0; a < WorldThemes.Count; a++)
            for (int b = a + 1; b < WorldThemes.Count; b++)
            {
                WorldTheme x = WorldThemes.At(a), y = WorldThemes.At(b);
                Assert.Less(Overlap(x.Scenery.Verge, y.Scenery.Verge), 0.75f,
                    $"{x.DisplayName} and {y.DisplayName} share almost all their verge");
                Assert.Less(Overlap(x.Scenery.Field, y.Scenery.Field), 0.75f,
                    $"{x.DisplayName} and {y.DisplayName} share almost all their field");
                Assert.Less(Overlap(x.Scenery.Landmarks, y.Scenery.Landmarks), 1f,
                    $"{x.DisplayName} and {y.DisplayName} have identical landmarks");
            }
        }

        [Test]
        public void TheVergeIsAlwaysGrimmerThanTheHorizon()
        {
            // "Bright landmarks, dark verge", as a checked invariant rather than a hope. The
            // road you actually look down stays dark fantasy; the thing on the skyline is the
            // colour in the frame.
            foreach (WorldTheme t in WorldThemes.All)
            {
                SceneryPalette s = t.Scenery;
                Assert.Greater(s.VergeTint, s.FieldTint, $"{t.DisplayName} verge is not grimmer than its field");
                Assert.Greater(s.FieldTint, s.LandmarkTint, $"{t.DisplayName} field is not grimmer than its landmarks");
                Assert.Greater(s.VergeTint, 0.5f, $"{t.DisplayName} roadside keeps too much Kenney colour");
                Assert.Less(s.LandmarkTint, 0.25f, $"{t.DisplayName} drains its own landmarks");
            }
        }

        [Test]
        public void ScaleTurnsKenneyModulesIntoBuildings()
        {
            // Kenney authors at roughly one unit per module and this game's road is 6.6 m
            // across. Without the scale every castle would be knee-high; with too much, a
            // gravestone would be taller than the army.
            foreach (WorldTheme t in WorldThemes.All)
            {
                SceneryPalette s = t.Scenery;
                Assert.Less(s.VergeScale, s.FieldScale, $"{t.DisplayName} verge outgrows its field");
                Assert.Less(s.FieldScale, s.LandmarkScale, $"{t.DisplayName} field outgrows its landmarks");

                foreach (string piece in s.Verge)
                {
                    float metres = SceneryPieces.Heights[SceneryPieces.IndexOf(piece)] * s.VergeScale;
                    Assert.Less(metres, 4f, $"{t.DisplayName}: '{piece}' looms over the road at {metres:0.0} m");
                }
                foreach (string name in s.Landmarks)
                {
                    Landmark l = Landmarks.ByName(name);
                    float tallest = 0f;
                    foreach (LandmarkPart part in l.Parts)
                    {
                        float top = (part.Y + SceneryPieces.Heights[SceneryPieces.IndexOf(part.Piece)]
                                     * part.Scale) * s.LandmarkScale;
                        if (top > tallest) tallest = top;
                    }
                    Assert.Greater(tallest, 6f,
                        $"{t.DisplayName}: {name} tops out at {tallest:0.0} m and is not a landmark");
                    // And an upper bound, which the first pass needed: at a landmark scale of
                    // 5 the castle keep stood 38 m and filled the sky from 30 m away.
                    Assert.Less(tallest, 34f,
                        $"{t.DisplayName}: {name} is {tallest:0.0} m and blots out the world");
                }
            }
        }

        private static float Overlap(string[] a, string[] b)
        {
            if (a.Length == 0 || b.Length == 0) return 0f;
            var set = new HashSet<string>(b);
            int shared = 0;
            foreach (string s in a) if (set.Contains(s)) shared++;
            return shared / (float)System.Math.Min(a.Length, b.Length);
        }
    }
}
