using System;
using System.Collections.Generic;
using System.Text;

namespace BattleRunner.Core.Art
{
    /// <summary>
    /// Builds a pack in the format <see cref="MeshPack"/> reads.
    ///
    /// The game never calls this — `tooling/fetch_scenery.py` writes the shipped pack. It
    /// exists so the format can be TESTED: a round trip through a writer and a reader in the
    /// same language proves the two agree, and a test that had to load the committed binary
    /// would depend on a file path that differs between `dotnet test` and Unity's runner.
    ///
    /// It is also the specification. If the Python baker and this ever disagree, this is the
    /// one with assertions on it.
    /// </summary>
    public sealed class MeshPackWriter
    {
        private sealed class Entry
        {
            public string Name;
            public float[] Positions;
            public float[] Normals;
            public byte[] Colors;
            public int[] Indices;
        }

        private readonly List<Entry> _entries = new List<Entry>();

        /// <summary>
        /// Positions and normals are x/y/z triples, colours are rgba quads, indices are
        /// triangle corners relative to this piece's own first vertex.
        /// </summary>
        public MeshPackWriter Add(string name, float[] positions, float[] normals,
            byte[] colors, int[] indices)
        {
            if (string.IsNullOrEmpty(name))
                throw new MeshPackException("a piece needs a name");
            if (Encoding.UTF8.GetByteCount(name) > MeshPack.NameBytes)
                throw new MeshPackException($"'{name}' is over {MeshPack.NameBytes} bytes");
            if (positions == null || positions.Length == 0 || positions.Length % 3 != 0)
                throw new MeshPackException($"'{name}' has no positions");

            int vertexCount = positions.Length / 3;
            if (vertexCount > ushort.MaxValue)
                throw new MeshPackException($"'{name}' exceeds the u16 index space");
            if (normals == null || normals.Length != positions.Length)
                throw new MeshPackException($"'{name}' normals do not match its positions");
            if (colors == null || colors.Length != vertexCount * 4)
                throw new MeshPackException($"'{name}' colours do not match its positions");
            if (indices == null || indices.Length == 0 || indices.Length % 3 != 0)
                throw new MeshPackException($"'{name}' is not made of triangles");
            foreach (int i in indices)
                if (i < 0 || i >= vertexCount)
                    throw new MeshPackException($"'{name}' index {i} is outside its own vertices");

            _entries.Add(new Entry
            {
                Name = name, Positions = positions, Normals = normals,
                Colors = colors, Indices = indices
            });
            return this;
        }

        public byte[] ToBytes()
        {
            int count = _entries.Count;
            if (count > ushort.MaxValue) throw new MeshPackException("too many pieces");

            var table = new List<byte>();
            var vertices = new List<byte>();
            var indices = new List<byte>();
            int vertexStart = 0, indexStart = 0;

            foreach (Entry e in _entries)
            {
                int n = e.Positions.Length / 3;
                float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
                float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;
                for (int i = 0; i < n; i++)
                {
                    minX = Math.Min(minX, e.Positions[i * 3]);
                    maxX = Math.Max(maxX, e.Positions[i * 3]);
                    minY = Math.Min(minY, e.Positions[i * 3 + 1]);
                    maxY = Math.Max(maxY, e.Positions[i * 3 + 1]);
                    minZ = Math.Min(minZ, e.Positions[i * 3 + 2]);
                    maxZ = Math.Max(maxZ, e.Positions[i * 3 + 2]);
                }

                byte[] raw = Encoding.UTF8.GetBytes(e.Name);
                table.AddRange(raw);
                for (int i = raw.Length; i < MeshPack.NameBytes; i++) table.Add(0);
                WriteU32(table, vertexStart);
                WriteU32(table, n);
                WriteU32(table, indexStart);
                WriteU32(table, e.Indices.Length);
                foreach (float f in new[] { minX, minY, minZ, maxX, maxY, maxZ })
                    WriteF32(table, f);

                // A degenerate axis (a flat plane) would divide by zero; the floor keeps the
                // quantised value pinned at 0 across that axis, which is exactly right.
                float sx = Math.Max(maxX - minX, 1e-6f);
                float sy = Math.Max(maxY - minY, 1e-6f);
                float sz = Math.Max(maxZ - minZ, 1e-6f);
                for (int i = 0; i < n; i++)
                {
                    WriteU16(vertices, Quantise(e.Positions[i * 3], minX, sx));
                    WriteU16(vertices, Quantise(e.Positions[i * 3 + 1], minY, sy));
                    WriteU16(vertices, Quantise(e.Positions[i * 3 + 2], minZ, sz));
                    vertices.Add(Octet(e.Normals[i * 3]));
                    vertices.Add(Octet(e.Normals[i * 3 + 1]));
                    vertices.Add(Octet(e.Normals[i * 3 + 2]));
                    vertices.Add(0);
                    vertices.Add(e.Colors[i * 4]);
                    vertices.Add(e.Colors[i * 4 + 1]);
                    vertices.Add(e.Colors[i * 4 + 2]);
                    vertices.Add(e.Colors[i * 4 + 3]);
                }

                foreach (int i in e.Indices) WriteU16(indices, i);
                vertexStart += n;
                indexStart += e.Indices.Length;
            }

            var all = new List<byte> { (byte)'B', (byte)'R', (byte)'S', (byte)'P' };
            WriteU16(all, MeshPack.Version);
            WriteU16(all, count);
            all.AddRange(table);
            all.AddRange(vertices);
            all.AddRange(indices);
            return all.ToArray();
        }

        private static int Quantise(float value, float min, float span)
        {
            int q = (int)Math.Round((value - min) / span * 65535.0);
            return q < 0 ? 0 : (q > 65535 ? 65535 : q);
        }

        private static byte Octet(float unitComponent)
        {
            int v = (int)Math.Round(unitComponent * 127.0);
            if (v < -127) v = -127;
            if (v > 127) v = 127;
            return (byte)(sbyte)v;
        }

        private static void WriteU16(List<byte> to, int value)
        {
            to.Add((byte)(value & 0xFF));
            to.Add((byte)((value >> 8) & 0xFF));
        }

        private static void WriteU32(List<byte> to, int value)
        {
            to.Add((byte)(value & 0xFF));
            to.Add((byte)((value >> 8) & 0xFF));
            to.Add((byte)((value >> 16) & 0xFF));
            to.Add((byte)((value >> 24) & 0xFF));
        }

        private static void WriteF32(List<byte> to, float value)
        {
            byte[] raw = BitConverter.GetBytes(value);
            if (!BitConverter.IsLittleEndian) Array.Reverse(raw);
            to.AddRange(raw);
        }
    }
}
