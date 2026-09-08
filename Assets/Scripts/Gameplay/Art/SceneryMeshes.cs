using BattleRunner.Core.Art;
using UnityEngine;

namespace BattleRunner.Gameplay.Art
{
    /// <summary>
    /// Turns the committed mesh pack into Unity meshes, once, and hands them out by index.
    ///
    /// Cached exactly the way <see cref="ProceduralMeshes"/> caches its own: a Mesh is
    /// immutable once built, every instance of a gravestone is the same gravestone, and
    /// doc 04 bans mid-run allocation — so the whole pack is decoded at boot and never
    /// touched again.
    ///
    /// The pack is a TextAsset rather than an imported model. Unity cannot read .glb, and a
    /// TextAsset costs one committed file and one hand-written .meta instead of an importer
    /// setting per model and a GUID per model in a repo whose metas are all hand-authored.
    /// </summary>
    public static class SceneryMeshes
    {
        private static Mesh[] _meshes;
        private static bool _loaded;

        /// <summary>True when the pack decoded and there is something to draw.</summary>
        public static bool Available => _loaded && _meshes != null && _meshes.Length > 0;

        public static int Count => _meshes == null ? 0 : _meshes.Length;

        /// <summary>
        /// Decode the pack. Safe to call more than once; only the first call does work.
        ///
        /// Every failure degrades to "no imported scenery" rather than throwing: the game
        /// shipped for months with nothing beside the road, so an unreadable pack must cost
        /// the player a bare verge and not a black screen.
        /// </summary>
        public static void Load()
        {
            if (_loaded) return;
            _loaded = true;

            var asset = Resources.Load<TextAsset>("Meshes/scenery");
            if (asset == null)
            {
                Debug.LogWarning("[Scenery] Meshes/scenery.bytes is missing — no imported scenery.");
                return;
            }

            MeshPack pack;
            try
            {
                pack = MeshPack.Read(asset.bytes);
            }
            catch (MeshPackException e)
            {
                // Loud, because a pack that decodes WRONG does not throw — it scatters
                // triangles across the level and looks like a physics bug.
                Debug.LogError("[Scenery] Mesh pack rejected: " + e.Message);
                return;
            }

            // The generated name table is the contract between Core and the binary. If they
            // disagree, a palette that names a piece would silently draw a different one.
            if (pack.Count != SceneryPieces.Count)
            {
                Debug.LogError($"[Scenery] Pack holds {pack.Count} pieces but SceneryPieces " +
                               $"lists {SceneryPieces.Count}. Re-run tooling/fetch_scenery.py.");
                return;
            }

            _meshes = new Mesh[pack.Count];
            var positions = new float[3];
            var normals = new float[3];
            var colors = new byte[4];
            var indices = new int[3];

            for (int i = 0; i < pack.Count; i++)
            {
                MeshPiece piece = pack.PieceAt(i);
                if (piece.Name != SceneryPieces.Names[i])
                {
                    Debug.LogError($"[Scenery] Pack piece {i} is '{piece.Name}' but the table " +
                                   $"says '{SceneryPieces.Names[i]}'. Re-run the baker.");
                    _meshes = null;
                    return;
                }

                int vertexFloats = piece.VertexCount * 3;
                if (positions.Length < vertexFloats)
                {
                    positions = new float[vertexFloats];
                    normals = new float[vertexFloats];
                    colors = new byte[piece.VertexCount * 4];
                }
                if (colors.Length < piece.VertexCount * 4) colors = new byte[piece.VertexCount * 4];
                if (indices.Length < piece.IndexCount) indices = new int[piece.IndexCount];

                pack.ReadPositions(i, positions);
                pack.ReadNormals(i, normals);
                pack.ReadColors(i, colors);
                pack.ReadIndices(i, indices);

                var v = new Vector3[piece.VertexCount];
                var n = new Vector3[piece.VertexCount];
                var c = new Color32[piece.VertexCount];
                for (int k = 0; k < piece.VertexCount; k++)
                {
                    v[k] = new Vector3(positions[k * 3], positions[k * 3 + 1], positions[k * 3 + 2]);
                    n[k] = new Vector3(normals[k * 3], normals[k * 3 + 1], normals[k * 3 + 2]);
                    c[k] = new Color32(colors[k * 4], colors[k * 4 + 1],
                        colors[k * 4 + 2], colors[k * 4 + 3]);
                }
                var tris = new int[piece.IndexCount];
                System.Array.Copy(indices, tris, piece.IndexCount);

                var mesh = new Mesh { name = "Scenery_" + piece.Name };
                mesh.vertices = v;
                mesh.normals = n;          // SET, not recalculated: the bake already welded
                mesh.colors32 = c;         //  by normal, so recalculating would smooth edges
                mesh.triangles = tris;
                mesh.RecalculateBounds();
                // Nothing in this game reads a scenery mesh back, and leaving it readable
                // keeps a second copy of every vertex in system memory for the whole run.
                mesh.UploadMeshData(true);
                _meshes[i] = mesh;
            }

            Debug.Log($"[Scenery] {pack.Count} pieces loaded.");
        }

        /// <summary>Null when the pack failed to load or the index is out of range.</summary>
        public static Mesh At(int index)
        {
            if (_meshes == null || index < 0 || index >= _meshes.Length) return null;
            return _meshes[index];
        }
    }
}
