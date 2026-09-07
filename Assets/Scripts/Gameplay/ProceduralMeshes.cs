using System;
using System.Collections.Generic;
using UnityEngine;

namespace BattleRunner.Gameplay
{
    /// <summary>
    /// Greybox meshes built in code — no model assets, nothing to strip or import.
    /// The unit mesh is a chunky low-poly "warrior" (body + head, ~48 tris) sized
    /// about 1 unit tall so crowd spacing math reads directly in meters. The boss
    /// gets its own mesh at the same 1-unit convention.
    ///
    /// Everything is built from six-sided hulls with duplicated corner vertices, so
    /// normals are hard and per-face. That matters beyond style: CrowdInstanced's rim
    /// term is dot(normal, view)-driven, so a shape assembled only from axis-aligned
    /// boxes presents the camera one flat face whose rim is a single constant — the
    /// reason the boss read as a featureless slab. AddPrism and AddOrientedBox exist to
    /// put slanted faces in the silhouette and give that term something to shade.
    /// </summary>
    public static class ProceduralMeshes
    {
        private static Mesh _unit;
        private static Mesh _boss;
        private static Mesh _cube;
        private static Mesh _shockWall;
        private static Mesh _dome;

        public static Mesh Unit
        {
            get
            {
                if (_unit == null) _unit = BuildUnit();
                return _unit;
            }
        }

        /// <summary>
        /// The boss body. Not the soldier at 6x — horned, hunched, deliberately
        /// lopsided, and carrying a cleaver.
        /// </summary>
        public static Mesh Boss
        {
            get
            {
                if (_boss == null) _boss = BuildBoss();
                return _boss;
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

        /// <summary>
        /// An open cylinder WALL — radius 1, standing from y=0 to y=1 — for additive
        /// shockwaves. It replaces a flat annulus that never appeared on device, and it
        /// removes both candidate reasons why:
        ///
        /// The annulus carried UVs, and BattleRunner/Vfx shaped its falloff from UV.x. It
        /// was the only mesh in this project with a UV channel, and if that channel did not
        /// reach the shader then uv.x read 0, sin(0) was 0, and the ring drew nothing while
        /// the UV-free debris motes on the same material drew fine — exactly what the
        /// screenshots showed. This mesh has NO UVs at all; the shader shapes its falloff
        /// from object-space height, which every mesh has.
        ///
        /// The annulus was also flat, so RecalculateBounds gave it a zero-extent Y and the
        /// camera saw it nearly edge-on: at 5.5 m up and 10 m back, a ground ring is
        /// squashed to a thin ellipse. A wall has real height, real bounds, and faces the
        /// camera — which is how shockwaves are drawn in a 3D scene anyway.
        /// </summary>
        public static Mesh ShockWall
        {
            get
            {
                if (_shockWall == null) _shockWall = BuildShockWall(48);
                return _shockWall;
            }
        }

        /// <summary>
        /// The four kinds of soldier in the army. Four meshes means four instanced draw
        /// calls instead of one — still nothing on any GPU — and it is the difference
        /// between an army and a photocopy. The two that carry something ABOVE the head
        /// (spear, banner) are doing most of the work: at 0.47 scale and 26 m a unit is
        /// about ten pixels tall, so anything at chest height is invisible and only what
        /// breaks the skyline reads.
        /// </summary>
        public enum SoldierKind { Spear = 0, Shield = 1, Axe = 2, Banner = 3 }

        public const int SoldierKindCount = 4;

        private static readonly Mesh[] _soldiers = new Mesh[SoldierKindCount];

        public static Mesh Soldier(SoldierKind kind)
        {
            int i = (int)kind;
            if (_soldiers[i] == null) _soldiers[i] = BuildSoldier(kind);
            return _soldiers[i];
        }

        /// <summary>
        /// The old unit mesh read as a CROSS, and the geometry says why: a 0.34-wide torso
        /// with pauldrons jutting to +/-0.24 at head height, and no legs at all — a single
        /// box from the ground to the shoulders. That is a plus sign with a head on it.
        ///
        /// The rebuild fixes both halves. Legs are separated with a real gap between them,
        /// which is the single strongest cue that a silhouette is a person; and the
        /// pauldrons come in to +/-0.19 and drop to shoulder height so the outline tapers
        /// from a wide base to a narrow head instead of spreading into a T.
        ///
        /// The hip line at y = 0.30 is load-bearing: CrowdInstanced swings everything below
        /// it about that pivot to make the march cycle, so every archetype must keep its
        /// legs under it and its body above it.
        /// </summary>
        private static Mesh BuildSoldier(SoldierKind kind)
        {
            var v = new List<Vector3>();
            var t = new List<int>();

            // Legs. Offset in x on purpose — the shader picks which limb swings forward
            // from sign(x), so anything centred on the midline stays still.
            AddPrism(v, t, new Vector3(-0.105f, 0f, 0f), new Vector2(0.13f, 0.17f),
                           new Vector3(-0.09f, 0.32f, 0f), new Vector2(0.14f, 0.18f));
            AddPrism(v, t, new Vector3(0.105f, 0f, 0f), new Vector2(0.13f, 0.17f),
                           new Vector3(0.09f, 0.32f, 0f), new Vector2(0.14f, 0.18f));

            // Torso: narrow at the waist, wide at the chest, leaning very slightly forward.
            AddPrism(v, t, new Vector3(0f, 0.28f, 0.01f), new Vector2(0.24f, 0.19f),
                           new Vector3(0f, 0.66f, -0.01f), new Vector2(0.32f, 0.22f));

            // Pauldrons: IN to 0.19 and down to the shoulder line. This is the single number
            // that was making a cross out of a soldier.
            AddPrism(v, t, new Vector3(-0.19f, 0.56f, 0f), new Vector2(0.13f, 0.19f),
                           new Vector3(-0.20f, 0.68f, 0f), new Vector2(0.10f, 0.15f));
            AddPrism(v, t, new Vector3(0.19f, 0.56f, 0f), new Vector2(0.13f, 0.19f),
                           new Vector3(0.20f, 0.68f, 0f), new Vector2(0.10f, 0.15f));

            // Head and helmet crest — the crest gives the skyline a point rather than a flat top.
            AddPrism(v, t, new Vector3(0f, 0.68f, -0.01f), new Vector2(0.17f, 0.17f),
                           new Vector3(0f, 0.84f, -0.01f), new Vector2(0.15f, 0.15f));
            AddPrism(v, t, new Vector3(0f, 0.83f, -0.01f), new Vector2(0.09f, 0.13f),
                           new Vector3(0f, 0.93f, 0.01f), new Vector2(0.03f, 0.06f));

            switch (kind)
            {
                case SoldierKind.Spear:
                    // Tall and thin, well clear of the helmet: the one shape that survives
                    // being ten pixels tall, because it breaks the skyline.
                    AddOrientedBox(v, t, new Vector3(0.23f, 0.62f, 0.02f),
                        new Vector3(0.035f, 1.05f, 0.035f), Quaternion.Euler(0f, 0f, -9f));
                    AddOrientedBox(v, t, new Vector3(0.31f, 1.12f, 0.02f),
                        new Vector3(0.06f, 0.16f, 0.03f), Quaternion.Euler(0f, 0f, -9f));
                    break;

                case SoldierKind.Shield:
                    AddOrientedBox(v, t, new Vector3(-0.25f, 0.44f, -0.09f),
                        new Vector3(0.21f, 0.30f, 0.05f), Quaternion.Euler(0f, 14f, 4f));
                    break;

                case SoldierKind.Axe:
                    AddOrientedBox(v, t, new Vector3(0.24f, 0.50f, 0.02f),
                        new Vector3(0.035f, 0.62f, 0.035f), Quaternion.Euler(0f, 0f, -14f));
                    AddOrientedBox(v, t, new Vector3(0.32f, 0.78f, 0.02f),
                        new Vector3(0.20f, 0.18f, 0.04f), Quaternion.Euler(0f, 0f, -14f));
                    break;

                case SoldierKind.Banner:
                    // Banners over a crowd is the oldest army silhouette there is, and it is
                    // the only element here tall enough to read across the whole formation.
                    AddOrientedBox(v, t, new Vector3(0.20f, 0.78f, 0.02f),
                        new Vector3(0.035f, 1.35f, 0.035f), Quaternion.Euler(0f, 0f, -5f));
                    AddOrientedBox(v, t, new Vector3(0.29f, 1.24f, 0.02f),
                        new Vector3(0.24f, 0.30f, 0.02f), Quaternion.Euler(0f, 0f, -5f));
                    break;
            }

            return Finish(v, t, $"Soldier{kind}");
        }

        private static Mesh BuildUnit() => BuildSoldier(SoldierKind.Spear);

        /// <summary>
        /// The boss, ~1.11 units tall so BossView's 6x renders a figure just under 7 m.
        ///
        /// It faces -Z, toward the camera and the oncoming army, using the same front
        /// convention as the unit mesh. That is load-bearing now: every previous boss
        /// was the unit mesh, which is symmetric about both X and Z, so the 180-degree
        /// yaw BossView used to apply was a no-op. This mesh is asymmetric on purpose
        /// and would be turned to face away.
        ///
        /// Widths are budgeted against the road. Lanes are 2.2 m and the road spans
        /// +/-3.3 m, so at 6x nothing may exceed x = 0.55. The body stops at 0.410
        /// (2.46 m, the left pauldron) and only the cleaver's outer corner reaches 0.584,
        /// or 3.51 m — 21 cm of overhang, which is the point of a weapon too big for the
        /// street. 132 triangles.
        /// </summary>
        private static Mesh BuildBoss()
        {
            var v = new List<Vector3>();
            var t = new List<int>();

            // Legs: planted wide, tapering IN as they rise, so the stance reads heavy.
            AddPrism(v, t, new Vector3(-0.16f, 0f, 0.02f), new Vector2(0.19f, 0.22f),
                           new Vector3(-0.12f, 0.32f, 0f), new Vector2(0.15f, 0.19f));
            AddPrism(v, t, new Vector3(0.16f, 0f, 0.02f), new Vector2(0.19f, 0.22f),
                           new Vector3(0.12f, 0.32f, 0f), new Vector2(0.15f, 0.19f));

            // Torso: narrow waist to broad shoulders, and leaning forward onto the
            // player. The lean is what separates this profile from a box at range —
            // a vertical trunk reads as a pillar however wide the top is.
            AddPrism(v, t, new Vector3(0f, 0.28f, 0.01f), new Vector2(0.30f, 0.24f),
                           new Vector3(0f, 0.72f, -0.05f), new Vector2(0.44f, 0.30f));

            // Head: small, sunk between the shoulders and pushed out ahead of them.
            AddPrism(v, t, new Vector3(0f, 0.70f, -0.09f), new Vector2(0.20f, 0.20f),
                           new Vector3(0f, 0.87f, -0.10f), new Vector2(0.17f, 0.17f));

            // Pauldrons, deliberately mismatched: the left one is larger and rides
            // higher. Bilateral symmetry is what made the old boss read as scenery.
            AddPrism(v, t, new Vector3(-0.28f, 0.58f, -0.03f), new Vector2(0.26f, 0.28f),
                           new Vector3(-0.33f, 0.80f, -0.03f), new Vector2(0.13f, 0.15f));
            AddPrism(v, t, new Vector3(0.27f, 0.56f, -0.02f), new Vector2(0.22f, 0.25f),
                           new Vector3(0.30f, 0.70f, -0.02f), new Vector2(0.11f, 0.13f));

            // Horns. Two segments on the left so the sweep bends outward and forward;
            // the right one is a snapped stub. This is the single strongest cue that
            // the thing at the end of the lane is not another soldier, and it is the
            // highest point on the mesh — nothing else competes with it.
            AddPrism(v, t, new Vector3(-0.09f, 0.83f, -0.09f), new Vector2(0.08f, 0.08f),
                           new Vector3(-0.17f, 0.98f, -0.05f), new Vector2(0.055f, 0.055f));
            AddPrism(v, t, new Vector3(-0.17f, 0.98f, -0.05f), new Vector2(0.055f, 0.055f),
                           new Vector3(-0.22f, 1.11f, 0.02f), new Vector2(0.018f, 0.018f));
            AddPrism(v, t, new Vector3(0.09f, 0.83f, -0.09f), new Vector2(0.08f, 0.08f),
                           new Vector3(0.15f, 0.94f, -0.06f), new Vector2(0.05f, 0.05f));

            // Cleaver, held out on the right. Haft and head share the same 16-degree
            // tilt so they stay one object.
            Quaternion tilt = Quaternion.Euler(0f, 0f, -16f);
            AddOrientedBox(v, t, new Vector3(0.36f, 0.52f, -0.06f), new Vector3(0.05f, 0.66f, 0.05f), tilt);
            AddOrientedBox(v, t, new Vector3(0.44f, 0.87f, -0.06f), new Vector3(0.22f, 0.28f, 0.045f), tilt);

            return Finish(v, t, "BossGreybox");
        }

        /// <summary>
        /// A unit hemisphere standing on y=0, for the shield barrier. Normals are SET, not
        /// recalculated: on a sphere the outward normal is just the normalised position, and
        /// RecalculateNormals would skew the equator ring because it only has geometry on
        /// one side of it — which is exactly the ring the player sees edge-on and where the
        /// fresnel term is doing its most visible work.
        ///
        /// Object-space y runs 0 at the base to 1 at the pole, which is what the Vfx
        /// shader's ripple rides on.
        /// </summary>
        public static Mesh Dome
        {
            get
            {
                if (_dome == null) _dome = BuildDome(24, 8);
                return _dome;
            }
        }

        private static Mesh BuildDome(int slices, int stacks)
        {
            var vertices = new List<Vector3>((slices + 1) * (stacks + 1));
            var normals = new List<Vector3>((slices + 1) * (stacks + 1));
            var triangles = new List<int>(slices * stacks * 6);

            for (int ring = 0; ring <= stacks; ring++)
            {
                float phi = (float)(ring * Math.PI * 0.5 / stacks); // 0 at the base, PI/2 at the pole
                float y = Mathf.Sin(phi);
                float r = Mathf.Cos(phi);
                for (int i = 0; i <= slices; i++)
                {
                    float theta = (float)(i * 2.0 * Math.PI / slices);
                    var v = new Vector3(Mathf.Cos(theta) * r, y, Mathf.Sin(theta) * r);
                    vertices.Add(v);
                    normals.Add(v.normalized);
                }
            }

            int stride = slices + 1;
            for (int ring = 0; ring < stacks; ring++)
            {
                for (int i = 0; i < slices; i++)
                {
                    int a = ring * stride + i;
                    int b = a + stride;
                    triangles.Add(a);
                    triangles.Add(b);
                    triangles.Add(a + 1);
                    triangles.Add(a + 1);
                    triangles.Add(b);
                    triangles.Add(b + 1);
                }
            }

            var mesh = new Mesh { name = "DomeGreybox" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildShockWall(int segments)
        {
            var vertices = new List<Vector3>(segments * 2 + 2);
            var triangles = new List<int>(segments * 6);

            for (int i = 0; i <= segments; i++)
            {
                float angle = (float)(i * 2.0 * Math.PI / segments);
                float sin = Mathf.Sin(angle);
                float cos = Mathf.Cos(angle);
                vertices.Add(new Vector3(cos, 0f, sin));
                vertices.Add(new Vector3(cos, 1f, sin));
            }

            // Winding is irrelevant — the Vfx shader is Cull Off, and seeing both the near
            // and the far wall is wanted: additive, they sum through the middle of the wave.
            for (int i = 0; i < segments; i++)
            {
                int v = i * 2;
                triangles.Add(v);
                triangles.Add(v + 1);
                triangles.Add(v + 2);
                triangles.Add(v + 1);
                triangles.Add(v + 3);
                triangles.Add(v + 2);
            }

            var mesh = new Mesh { name = "ShockWallGreybox" };
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
            return Finish(vertices, triangles, "BoxGreybox");
        }

        private static Mesh Finish(List<Vector3> vertices, List<int> triangles, string name)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddBox(List<Vector3> vertices, List<int> triangles, Vector3 center, Vector3 size)
        {
            Vector3 h = size * 0.5f;
            AddHull(vertices, triangles, new[]
            {
                center + new Vector3(-h.x, -h.y, -h.z), center + new Vector3(h.x, -h.y, -h.z),
                center + new Vector3(h.x, -h.y, h.z), center + new Vector3(-h.x, -h.y, h.z),
                center + new Vector3(-h.x, h.y, -h.z), center + new Vector3(h.x, h.y, -h.z),
                center + new Vector3(h.x, h.y, h.z), center + new Vector3(-h.x, h.y, h.z)
            });
        }

        /// <summary>
        /// A box whose top face may be a different size and sit off to one side: taper
        /// plus lean in one primitive. Sizes are XZ footprints; the Y span comes from
        /// the two centers, so a segment can be chained by reusing the previous top as
        /// the next bottom.
        /// </summary>
        private static void AddPrism(List<Vector3> vertices, List<int> triangles,
            Vector3 bottomCenter, Vector2 bottomSize, Vector3 topCenter, Vector2 topSize)
        {
            Vector2 b = bottomSize * 0.5f;
            Vector2 t = topSize * 0.5f;
            AddHull(vertices, triangles, new[]
            {
                bottomCenter + new Vector3(-b.x, 0f, -b.y), bottomCenter + new Vector3(b.x, 0f, -b.y),
                bottomCenter + new Vector3(b.x, 0f, b.y), bottomCenter + new Vector3(-b.x, 0f, b.y),
                topCenter + new Vector3(-t.x, 0f, -t.y), topCenter + new Vector3(t.x, 0f, -t.y),
                topCenter + new Vector3(t.x, 0f, t.y), topCenter + new Vector3(-t.x, 0f, t.y)
            });
        }

        /// <summary>A box rotated about its own center — for parts that are not axis-aligned.</summary>
        private static void AddOrientedBox(List<Vector3> vertices, List<int> triangles,
            Vector3 center, Vector3 size, Quaternion rotation)
        {
            Vector3 h = size * 0.5f;
            var local = new[]
            {
                new Vector3(-h.x, -h.y, -h.z), new Vector3(h.x, -h.y, -h.z),
                new Vector3(h.x, -h.y, h.z), new Vector3(-h.x, -h.y, h.z),
                new Vector3(-h.x, h.y, -h.z), new Vector3(h.x, h.y, -h.z),
                new Vector3(h.x, h.y, h.z), new Vector3(-h.x, h.y, h.z)
            };
            for (int i = 0; i < local.Length; i++) local[i] = center + rotation * local[i];
            AddHull(vertices, triangles, local);
        }

        /// <summary>
        /// Six quads over eight corners, ordered 0-3 bottom then 4-7 top, each corner
        /// duplicated per face so RecalculateNormals produces hard edges.
        /// </summary>
        private static void AddHull(List<Vector3> vertices, List<int> triangles, Vector3[] corners)
        {
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
