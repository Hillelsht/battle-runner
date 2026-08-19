using System.Collections.Generic;
using UnityEngine;

namespace BattleRunner.Gameplay
{
    /// <summary>
    /// Greybox meshes built in code — no model assets, nothing to strip or import.
    /// The unit mesh is a chunky low-poly "warrior" (body + head, ~48 tris) sized
    /// about 1 unit tall so crowd spacing math reads directly in meters.
    /// </summary>
    public static class ProceduralMeshes
    {
        private static Mesh _unit;
        private static Mesh _cube;

        public static Mesh Unit
        {
            get
            {
                if (_unit == null) _unit = BuildUnit();
                return _unit;
            }
        }

        public static Mesh Cube
        {
            get
            {
                if (_cube == null) _cube = BuildBox(Vector3.zero, Vector3.one);
                return _cube;
            }
        }

        private static Mesh BuildUnit()
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();

            // Torso: tapered box, feet at y=0.
            AddBox(vertices, triangles, new Vector3(0f, 0.34f, 0f), new Vector3(0.34f, 0.68f, 0.22f));
            // Head.
            AddBox(vertices, triangles, new Vector3(0f, 0.84f, 0f), new Vector3(0.22f, 0.22f, 0.2f));
            // Shoulder pauldrons — the dark-fantasy silhouette.
            AddBox(vertices, triangles, new Vector3(-0.24f, 0.62f, 0f), new Vector3(0.12f, 0.12f, 0.2f));
            AddBox(vertices, triangles, new Vector3(0.24f, 0.62f, 0f), new Vector3(0.12f, 0.12f, 0.2f));

            var mesh = new Mesh { name = "UnitGreybox" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public static Mesh BuildBox(Vector3 center, Vector3 size)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            AddBox(vertices, triangles, center, size);
            var mesh = new Mesh { name = "BoxGreybox" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddBox(List<Vector3> vertices, List<int> triangles, Vector3 center, Vector3 size)
        {
            Vector3 h = size * 0.5f;
            Vector3[] corners =
            {
                center + new Vector3(-h.x, -h.y, -h.z), center + new Vector3(h.x, -h.y, -h.z),
                center + new Vector3(h.x, -h.y, h.z), center + new Vector3(-h.x, -h.y, h.z),
                center + new Vector3(-h.x, h.y, -h.z), center + new Vector3(h.x, h.y, -h.z),
                center + new Vector3(h.x, h.y, h.z), center + new Vector3(-h.x, h.y, h.z)
            };
            int[][] faces =
            {
                new[] { 0, 1, 2, 3 }, // bottom
                new[] { 7, 6, 5, 4 }, // top
                new[] { 4, 5, 1, 0 }, // front (-z)
                new[] { 6, 7, 3, 2 }, // back (+z)
                new[] { 7, 4, 0, 3 }, // left
                new[] { 5, 6, 2, 1 }  // right
            };

            foreach (int[] face in faces)
            {
                int baseIndex = vertices.Count;
                // Duplicate vertices per face for hard normals.
                vertices.Add(corners[face[0]]);
                vertices.Add(corners[face[1]]);
                vertices.Add(corners[face[2]]);
                vertices.Add(corners[face[3]]);
                triangles.Add(baseIndex);
                triangles.Add(baseIndex + 1);
                triangles.Add(baseIndex + 2);
                triangles.Add(baseIndex);
                triangles.Add(baseIndex + 2);
                triangles.Add(baseIndex + 3);
            }
        }
    }
}
