using System;
using System.Text;

namespace BattleRunner.Core.Art
{
    /// <summary>Thrown when a pack is truncated, mislabelled, or from a future version.</summary>
    public sealed class MeshPackException : Exception
    {
        public MeshPackException(string message) : base(message) { }
    }

    /// <summary>One mesh inside a pack: where its data starts and how big it is in the world.</summary>
    public readonly struct MeshPiece
    {
        public readonly string Name;
        public readonly int VertexStart, VertexCount, IndexStart, IndexCount;
        public readonly float MinX, MinY, MinZ, MaxX, MaxY, MaxZ;

        public MeshPiece(string name, int vertexStart, int vertexCount,
            int indexStart, int indexCount,
            float minX, float minY, float minZ, float maxX, float maxY, float maxZ)
        {
            Name = name;
            VertexStart = vertexStart;
            VertexCount = vertexCount;
            IndexStart = indexStart;
            IndexCount = indexCount;
            MinX = minX; MinY = minY; MinZ = minZ;
            MaxX = maxX; MaxY = maxY; MaxZ = maxZ;
        }

        public float Height => MaxY - MinY;
        public float Width => MaxX - MinX;
        public float Depth => MaxZ - MinZ;
    }

    /// <summary>
    /// The imported scenery, decoded from one committed binary.
    ///
    /// WHY THIS IS IN CORE. It is engine-free on purpose, which is what lets the format be
    /// tested at all: the test assembly references Core and nothing else, so every assertion
    /// about this file runs identically under headless `dotnet test` and under Unity's
    /// runner. A decoder living in Gameplay could only be checked by opening the editor.
    ///
    /// WHY A CUSTOM FORMAT AND NOT AN IMPORTER. Unity cannot read .glb natively and the
    /// packages that can bring a whole second material and rendering path alongside the
    /// Graphics.RenderMeshInstanced one this game already uses. Baking offline into one
    /// TextAsset means one committed file, one hand-written .meta, no model importer, no
    /// prefabs, and meshes that arrive in exactly the shape the instanced renderer wants.
    ///
    /// THE LAYOUT, little-endian throughout:
    ///
    ///     'B','R','S','P' | version u16 | pieceCount u16
    ///     pieceCount records of 64 bytes:
    ///         name        24 bytes utf8, NUL padded
    ///         vertexStart u32, vertexCount u32, indexStart u32, indexCount u32
    ///         boundsMin   3 x f32
    ///         boundsMax   3 x f32
    ///     vertex block, 14 bytes each:
    ///         position 3 x u16   quantised across the PIECE'S OWN bounds
    ///         normal   3 x i8    over 127
    ///         padding  1 x u8    so colour always starts at offset 10
    ///         colour   4 x u8    rgba
    ///     index block, u16 each
    ///
    /// Positions quantise per piece rather than per pack because a gravestone and a castle
    /// wall share no scale, and one global quantum would spend all of its precision on the
    /// castle. Within a piece, u16 across its own bounding box is about 0.02 mm on a 1.3 m
    /// wall — far below anything the eye or the depth buffer can resolve.
    /// </summary>
    public sealed class MeshPack
    {
        public const int NameBytes = 24;
        public const int RecordBytes = 64;   // 24 name + 4 x u32 + 6 x f32
        public const int VertexBytes = 14;   // 3 x u16 + 3 x i8 + pad + 4 x u8
        public const int Version = 1;

        private static readonly byte[] Magic = { (byte)'B', (byte)'R', (byte)'S', (byte)'P' };

        private readonly byte[] _data;
        private readonly MeshPiece[] _pieces;
        private readonly int _vertexBase;
        private readonly int _indexBase;

        public int Count => _pieces.Length;
        public MeshPiece PieceAt(int index) => _pieces[index];

        private MeshPack(byte[] data, MeshPiece[] pieces, int vertexBase, int indexBase)
        {
            _data = data;
            _pieces = pieces;
            _vertexBase = vertexBase;
            _indexBase = indexBase;
        }

        public int IndexOf(string name)
        {
            for (int i = 0; i < _pieces.Length; i++)
                if (_pieces[i].Name == name) return i;
            return -1;
        }

        /// <summary>
        /// Decode a pack, or throw. Every failure is loud: a mesh built from a misread buffer
        /// is not a visible error, it is a scattering of triangles across the level that
        /// looks like a physics bug and costs a day to trace back here.
        /// </summary>
        public static MeshPack Read(byte[] data)
        {
            if (data == null) throw new MeshPackException("no data");
            if (data.Length < 8) throw new MeshPackException("shorter than a header");
            for (int i = 0; i < Magic.Length; i++)
                if (data[i] != Magic[i]) throw new MeshPackException("not a mesh pack");

            int version = ReadU16(data, 4);
            if (version != Version)
                throw new MeshPackException($"pack is version {version}, this build reads {Version}");

            int count = ReadU16(data, 6);
            int tableBytes = count * RecordBytes;
            if (data.Length < 8 + tableBytes)
                throw new MeshPackException("piece table is truncated");

            var pieces = new MeshPiece[count];
            long vertexTotal = 0, indexTotal = 0;
            for (int i = 0; i < count; i++)
            {
                int at = 8 + i * RecordBytes;
                int nameLength = 0;
                while (nameLength < NameBytes && data[at + nameLength] != 0) nameLength++;
                string name = Encoding.UTF8.GetString(data, at, nameLength);

                int p = at + NameBytes;
                long vStart = ReadU32(data, p);
                long vCount = ReadU32(data, p + 4);
                long iStart = ReadU32(data, p + 8);
                long iCount = ReadU32(data, p + 12);

                if (vCount == 0 || iCount == 0 || iCount % 3 != 0)
                    throw new MeshPackException($"piece '{name}' is not made of triangles");
                if (vStart != vertexTotal || iStart != indexTotal)
                    throw new MeshPackException($"piece '{name}' is out of order in the pack");
                vertexTotal = vStart + vCount;
                indexTotal = iStart + iCount;

                pieces[i] = new MeshPiece(name, (int)vStart, (int)vCount, (int)iStart, (int)iCount,
                    ReadF32(data, p + 16), ReadF32(data, p + 20), ReadF32(data, p + 24),
                    ReadF32(data, p + 28), ReadF32(data, p + 32), ReadF32(data, p + 36));
            }

            int vertexBase = 8 + tableBytes;
            int indexBase = vertexBase + (int)(vertexTotal * VertexBytes);
            long need = indexBase + indexTotal * 2L;
            if (data.Length < need)
                throw new MeshPackException(
                    $"pack is {data.Length} bytes but its table describes {need}");

            return new MeshPack(data, pieces, vertexBase, indexBase);
        }

        /// <summary>
        /// Positions in metres, x/y/z triples. <paramref name="destination"/> must hold at
        /// least VertexCount * 3.
        /// </summary>
        public void ReadPositions(int piece, float[] destination)
        {
            MeshPiece p = _pieces[piece];
            Require(destination, p.VertexCount * 3, "positions");
            float sx = (p.MaxX - p.MinX) / 65535f;
            float sy = (p.MaxY - p.MinY) / 65535f;
            float sz = (p.MaxZ - p.MinZ) / 65535f;
            for (int i = 0; i < p.VertexCount; i++)
            {
                int at = _vertexBase + (p.VertexStart + i) * VertexBytes;
                destination[i * 3] = p.MinX + ReadU16(_data, at) * sx;
                destination[i * 3 + 1] = p.MinY + ReadU16(_data, at + 2) * sy;
                destination[i * 3 + 2] = p.MinZ + ReadU16(_data, at + 4) * sz;
            }
        }

        /// <summary>Unit normals, x/y/z triples.</summary>
        public void ReadNormals(int piece, float[] destination)
        {
            MeshPiece p = _pieces[piece];
            Require(destination, p.VertexCount * 3, "normals");
            for (int i = 0; i < p.VertexCount; i++)
            {
                int at = _vertexBase + (p.VertexStart + i) * VertexBytes + 6;
                destination[i * 3] = (sbyte)_data[at] / 127f;
                destination[i * 3 + 1] = (sbyte)_data[at + 1] / 127f;
                destination[i * 3 + 2] = (sbyte)_data[at + 2] / 127f;
            }
        }

        /// <summary>Vertex colours as rgba bytes. This is the ONLY colour a piece carries.</summary>
        public void ReadColors(int piece, byte[] destination)
        {
            MeshPiece p = _pieces[piece];
            if (destination == null || destination.Length < p.VertexCount * 4)
                throw new MeshPackException($"colour buffer too small for '{p.Name}'");
            for (int i = 0; i < p.VertexCount; i++)
            {
                int at = _vertexBase + (p.VertexStart + i) * VertexBytes + 10;
                destination[i * 4] = _data[at];
                destination[i * 4 + 1] = _data[at + 1];
                destination[i * 4 + 2] = _data[at + 2];
                destination[i * 4 + 3] = _data[at + 3];
            }
        }

        /// <summary>Triangle indices, already relative to the piece's own first vertex.</summary>
        public void ReadIndices(int piece, int[] destination)
        {
            MeshPiece p = _pieces[piece];
            if (destination == null || destination.Length < p.IndexCount)
                throw new MeshPackException($"index buffer too small for '{p.Name}'");
            for (int i = 0; i < p.IndexCount; i++)
            {
                int value = ReadU16(_data, _indexBase + (p.IndexStart + i) * 2);
                if (value >= p.VertexCount)
                    throw new MeshPackException(
                        $"piece '{p.Name}' index {i} points at vertex {value} of {p.VertexCount}");
                destination[i] = value;
            }
        }

        private static void Require(float[] buffer, int length, string what)
        {
            if (buffer == null || buffer.Length < length)
                throw new MeshPackException($"{what} buffer needs {length} entries");
        }

        private static int ReadU16(byte[] d, int at) => d[at] | (d[at + 1] << 8);

        private static long ReadU32(byte[] d, int at) =>
            (long)d[at] | ((long)d[at + 1] << 8) | ((long)d[at + 2] << 16) | ((long)d[at + 3] << 24);

        /// <summary>
        /// The pack is always little-endian. BitConverter follows the HOST, so on a
        /// big-endian machine the four bytes are reversed first rather than assumed. No
        /// unsafe code and no BitConverter.Int32BitsToSingle, which netstandard2.1 has but
        /// Unity's older Mono profile does not reliably expose.
        /// </summary>
        private static float ReadF32(byte[] d, int at)
        {
            if (BitConverter.IsLittleEndian) return BitConverter.ToSingle(d, at);
            var flipped = new[] { d[at + 3], d[at + 2], d[at + 1], d[at] };
            return BitConverter.ToSingle(flipped, 0);
        }
    }
}
