using System;
using BattleRunner.Core.Art;
using NUnit.Framework;

namespace BattleRunner.Tests
{
    /// <summary>
    /// The imported-scenery format.
    ///
    /// Nothing about a mesh can be seen from CI, so what is checked is the thing that would
    /// fail silently and expensively: a misread buffer does not throw, it scatters triangles
    /// across the level and looks like a physics bug. Every one of these runs without an
    /// engine, which is the entire reason the reader lives in Core.
    /// </summary>
    [TestFixture]
    public class MeshPackTests
    {
        /// <summary>A unit tetrahedron with a distinct colour per vertex.</summary>
        private static void Sample(out float[] pos, out float[] nrm, out byte[] col, out int[] idx)
        {
            pos = new[] { 0f, 0f, 0f, 1f, 0f, 0f, 0f, 2f, 0f, 0f, 0f, 3f };
            nrm = new[] { 0f, 1f, 0f, 1f, 0f, 0f, 0f, 0f, 1f, -1f, 0f, 0f };
            col = new byte[] { 10, 20, 30, 255, 40, 50, 60, 255,
                               70, 80, 90, 255, 100, 110, 120, 255 };
            idx = new[] { 0, 1, 2, 0, 2, 3, 0, 3, 1, 1, 3, 2 };
        }

        private static MeshPack RoundTrip(out float[] pos, out float[] nrm,
            out byte[] col, out int[] idx)
        {
            Sample(out pos, out nrm, out col, out idx);
            byte[] bytes = new MeshPackWriter().Add("tetra", pos, nrm, col, idx).ToBytes();
            return MeshPack.Read(bytes);
        }

        [Test]
        public void APieceSurvivesAWriteAndARead()
        {
            MeshPack pack = RoundTrip(out float[] pos, out _, out _, out int[] idx);
            Assert.AreEqual(1, pack.Count);
            MeshPiece p = pack.PieceAt(0);
            Assert.AreEqual("tetra", p.Name);
            Assert.AreEqual(pos.Length / 3, p.VertexCount);
            Assert.AreEqual(idx.Length, p.IndexCount);
            Assert.AreEqual(0, pack.IndexOf("tetra"));
            Assert.AreEqual(-1, pack.IndexOf("nothing"));
        }

        [Test]
        public void PositionsComeBackInsideTheQuantisationError()
        {
            MeshPack pack = RoundTrip(out float[] pos, out _, out _, out _);
            MeshPiece p = pack.PieceAt(0);
            var got = new float[p.VertexCount * 3];
            pack.ReadPositions(0, got);

            // u16 across a 3 m span is 46 microns. The tolerance here is a thousand times
            // that, and it still catches any real decode bug: a wrong stride or a swapped
            // axis is off by whole metres, never by a hair.
            for (int i = 0; i < pos.Length; i++)
                Assert.AreEqual(pos[i], got[i], 0.001f, $"component {i}");
        }

        [Test]
        public void NormalsStayUnitLengthAndPointTheSameWay()
        {
            MeshPack pack = RoundTrip(out _, out float[] nrm, out _, out _);
            MeshPiece p = pack.PieceAt(0);
            var got = new float[p.VertexCount * 3];
            pack.ReadNormals(0, got);
            for (int v = 0; v < p.VertexCount; v++)
            {
                for (int k = 0; k < 3; k++)
                    Assert.AreEqual(nrm[v * 3 + k], got[v * 3 + k], 0.01f);
                double length = Math.Sqrt(got[v * 3] * got[v * 3]
                                          + got[v * 3 + 1] * got[v * 3 + 1]
                                          + got[v * 3 + 2] * got[v * 3 + 2]);
                Assert.AreEqual(1.0, length, 0.02, "an i8 normal stopped being unit length");
            }
        }

        [Test]
        public void ColoursAreExact()
        {
            // Colour is the ONLY thing a piece carries besides its shape — there are no
            // textures and no per-piece material — so it is the one channel that must not
            // be approximate.
            MeshPack pack = RoundTrip(out _, out _, out byte[] col, out _);
            var got = new byte[pack.PieceAt(0).VertexCount * 4];
            pack.ReadColors(0, got);
            CollectionAssert.AreEqual(col, got);
        }

        [Test]
        public void IndicesComeBackUnchangedAndInRange()
        {
            MeshPack pack = RoundTrip(out _, out _, out _, out int[] idx);
            var got = new int[pack.PieceAt(0).IndexCount];
            pack.ReadIndices(0, got);
            CollectionAssert.AreEqual(idx, got);
        }

        [Test]
        public void BoundsEncloseEveryVertex()
        {
            // The renderer sizes its culling volume from these. Bounds that do not contain
            // the mesh make it vanish at certain camera angles and nowhere else, which is
            // the hardest class of rendering bug to reproduce.
            MeshPack pack = RoundTrip(out _, out _, out _, out _);
            MeshPiece p = pack.PieceAt(0);
            var got = new float[p.VertexCount * 3];
            pack.ReadPositions(0, got);
            for (int v = 0; v < p.VertexCount; v++)
            {
                Assert.GreaterOrEqual(got[v * 3], p.MinX - 1e-3f);
                Assert.LessOrEqual(got[v * 3], p.MaxX + 1e-3f);
                Assert.GreaterOrEqual(got[v * 3 + 1], p.MinY - 1e-3f);
                Assert.LessOrEqual(got[v * 3 + 1], p.MaxY + 1e-3f);
                Assert.GreaterOrEqual(got[v * 3 + 2], p.MinZ - 1e-3f);
                Assert.LessOrEqual(got[v * 3 + 2], p.MaxZ + 1e-3f);
            }
            Assert.AreEqual(2f, p.Height, 1e-3f);
            Assert.AreEqual(1f, p.Width, 1e-3f);
            Assert.AreEqual(3f, p.Depth, 1e-3f);
        }

        [Test]
        public void SeveralPiecesKeepTheirOwnDataAndTheirOwnScale()
        {
            // The reason positions quantise per PIECE rather than per pack: a gravestone and
            // a castle share no scale, and one global quantum would spend all its precision
            // on the castle. This pins that a tiny piece next to a huge one stays accurate.
            Sample(out float[] pos, out float[] nrm, out byte[] col, out int[] idx);
            var tiny = new float[pos.Length];
            for (int i = 0; i < pos.Length; i++) tiny[i] = pos[i] * 0.001f;

            byte[] bytes = new MeshPackWriter()
                .Add("huge", pos, nrm, col, idx)
                .Add("tiny", tiny, nrm, col, idx)
                .ToBytes();
            MeshPack pack = MeshPack.Read(bytes);

            Assert.AreEqual(2, pack.Count);
            Assert.AreEqual(1, pack.IndexOf("tiny"));
            Assert.AreEqual(pack.PieceAt(0).VertexCount, pack.PieceAt(1).VertexStart,
                "the second piece does not start where the first ends");

            var got = new float[pack.PieceAt(1).VertexCount * 3];
            pack.ReadPositions(1, got);
            for (int i = 0; i < tiny.Length; i++)
                Assert.AreEqual(tiny[i], got[i], 1e-6f, "a small piece lost precision to a big one");
        }

        [Test]
        public void ATruncatedPackIsRefusedRatherThanMisread()
        {
            Sample(out float[] pos, out float[] nrm, out byte[] col, out int[] idx);
            byte[] full = new MeshPackWriter().Add("tetra", pos, nrm, col, idx).ToBytes();

            // Cut it anywhere past the header. Reading on would produce a mesh, not an error.
            for (int cut = 9; cut < full.Length; cut += 7)
            {
                var chopped = new byte[cut];
                Array.Copy(full, chopped, cut);
                Assert.Throws<MeshPackException>(() => MeshPack.Read(chopped),
                    $"a pack cut to {cut} bytes was accepted");
            }
        }

        [Test]
        public void RubbishIsRefused()
        {
            Assert.Throws<MeshPackException>(() => MeshPack.Read(null));
            Assert.Throws<MeshPackException>(() => MeshPack.Read(new byte[0]));
            Assert.Throws<MeshPackException>(() => MeshPack.Read(new byte[64]));
        }

        [Test]
        public void AFuturePackIsRefusedRatherThanReadWithTodaysLayout()
        {
            // The failure this prevents: a newer baker adds a vertex channel, an older build
            // reads the same bytes with the old stride, and every mesh becomes noise.
            Sample(out float[] pos, out float[] nrm, out byte[] col, out int[] idx);
            byte[] bytes = new MeshPackWriter().Add("tetra", pos, nrm, col, idx).ToBytes();
            bytes[4] = (byte)(MeshPack.Version + 1);
            MeshPackException e = Assert.Throws<MeshPackException>(() => MeshPack.Read(bytes));
            StringAssert.Contains("version", e.Message);
        }

        [Test]
        public void UndersizedBuffersAreRefused()
        {
            MeshPack pack = RoundTrip(out _, out _, out _, out _);
            Assert.Throws<MeshPackException>(() => pack.ReadPositions(0, new float[3]));
            Assert.Throws<MeshPackException>(() => pack.ReadNormals(0, new float[3]));
            Assert.Throws<MeshPackException>(() => pack.ReadColors(0, new byte[3]));
            Assert.Throws<MeshPackException>(() => pack.ReadIndices(0, new int[3]));
        }

        [Test]
        public void TheWriterRefusesGeometryThatWouldNotDraw()
        {
            Sample(out float[] pos, out float[] nrm, out byte[] col, out int[] idx);
            var w = new MeshPackWriter();
            Assert.Throws<MeshPackException>(() => w.Add("", pos, nrm, col, idx));
            Assert.Throws<MeshPackException>(
                () => w.Add(new string('x', MeshPack.NameBytes + 1), pos, nrm, col, idx));
            Assert.Throws<MeshPackException>(() => w.Add("a", new float[4], nrm, col, idx));
            Assert.Throws<MeshPackException>(() => w.Add("a", pos, new float[3], col, idx));
            Assert.Throws<MeshPackException>(() => w.Add("a", pos, nrm, new byte[3], idx));
            Assert.Throws<MeshPackException>(() => w.Add("a", pos, nrm, col, new[] { 0, 1 }));
            // The one that matters most: an index past the end of its own piece is what turns
            // a mesh into a spray of triangles reaching across the level.
            Assert.Throws<MeshPackException>(() => w.Add("a", pos, nrm, col, new[] { 0, 1, 99 }));
        }
    }
}
