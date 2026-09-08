using System;
using System.Collections.Generic;
using BattleRunner.Core.Boss;
using BattleRunner.Core.World;
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
        private static readonly Mesh[] _props =
            new Mesh[System.Enum.GetValues(typeof(PropKind)).Length];

        private static readonly Mesh[] _bosses =
            new Mesh[System.Enum.GetValues(typeof(BossArchetype)).Length];
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
        /// One body per archetype. Not one mesh recoloured six times — the roster shipped
        /// as two bosses that used the SAME mesh and differed only in tint, and the report
        /// was, correctly, that the boss never changes.
        ///
        /// What separates them is the SILHOUETTE, because that is all that survives the
        /// distance: the boss stands about 16 m out at 6x scale, backlit against fog, and
        /// under those conditions a different shoulder width is invisible while a different
        /// number of legs is unmistakable. So the six differ in the things an outline can
        /// carry — leg count, height, how far forward the mass leans, and what breaks the
        /// skyline above the head.
        /// </summary>
        public static Mesh Boss(BossArchetype archetype)
        {
            int i = (int)archetype;
            if (i < 0 || i >= _bosses.Length) i = 0;
            if (_bosses[i] == null) _bosses[i] = BuildBoss((BossArchetype)i);
            return _bosses[i];
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
        private static Mesh BuildBoss(BossArchetype archetype) => archetype switch
        {
            BossArchetype.Volley => BuildLich(),
            BossArchetype.Warded => BuildWarden(),
            BossArchetype.Drain => BuildLeech(),
            BossArchetype.Summoner => BuildShepherd(),
            BossArchetype.Enrage => BuildHound(),
            _ => BuildColossus()
        };

        private static Mesh BuildColossus()
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

            return Finish(v, t, "Boss_Colossus");
        }

        /// <summary>
        /// EMBER LICH — the volley. No legs at all: a robe cone widening to the ground, so
        /// it reads as gliding rather than walking, which is the cheapest way to say
        /// "undead" in an outline. Tallest of the six, and the only one whose weapon rises
        /// ABOVE its head — a staff with a heavy orb, which is what the eye tracks during a
        /// three-blow telegraph.
        /// </summary>
        private static Mesh BuildLich()
        {
            var v = new List<Vector3>();
            var t = new List<int>();

            // Robe: a wide skirt narrowing to a thin waist. One prism does the whole
            // lower body, which is exactly why there is no walk to animate.
            AddPrism(v, t, new Vector3(0f, 0f, 0f), new Vector2(0.52f, 0.44f),
                           new Vector3(0f, 0.52f, -0.01f), new Vector2(0.24f, 0.20f));

            // Chest, narrow and upright. This one does NOT lean — the hunch belongs to
            // the Leech, and two bosses with the same posture read as the same boss.
            AddPrism(v, t, new Vector3(0f, 0.50f, -0.01f), new Vector2(0.26f, 0.20f),
                           new Vector3(0f, 0.82f, -0.03f), new Vector2(0.34f, 0.22f));

            // Hood: a tall wedge coming to a point ahead of the shoulders.
            AddPrism(v, t, new Vector3(0f, 0.80f, -0.05f), new Vector2(0.22f, 0.22f),
                           new Vector3(0f, 1.02f, -0.09f), new Vector2(0.09f, 0.09f));

            // Three thin crown spikes, splayed. Small, but they are the highest thing on
            // the head and they break the hood's clean wedge.
            for (int i = -1; i <= 1; i++)
                AddPrism(v, t, new Vector3(i * 0.09f, 0.86f, -0.04f), new Vector2(0.045f, 0.045f),
                               new Vector3(i * 0.15f, 1.06f, -0.02f), new Vector2(0.012f, 0.012f));

            // Shoulder mantle, flat and wide — a lich is broad at the shoulder and
            // nowhere else.
            AddPrism(v, t, new Vector3(-0.30f, 0.74f, -0.02f), new Vector2(0.24f, 0.24f),
                           new Vector3(-0.34f, 0.84f, -0.02f), new Vector2(0.12f, 0.14f));
            AddPrism(v, t, new Vector3(0.30f, 0.74f, -0.02f), new Vector2(0.24f, 0.24f),
                           new Vector3(0.34f, 0.84f, -0.02f), new Vector2(0.12f, 0.14f));

            // Staff, planted and leaning out, with the orb over the head.
            Quaternion lean = Quaternion.Euler(0f, 0f, 9f);
            AddOrientedBox(v, t, new Vector3(-0.40f, 0.62f, -0.04f), new Vector3(0.05f, 1.24f, 0.05f), lean);
            AddBox(v, t, new Vector3(-0.50f, 1.26f, -0.04f), new Vector3(0.17f, 0.17f, 0.17f));

            return Finish(v, t, "Boss_Lich");
        }

        /// <summary>
        /// GRAVE WARDEN — the ward. Squat and immensely wide, and the tower shield is held
        /// OUT IN FRONT rather than at the side: it is the first thing the geometry presents
        /// to the camera, so a player looking down the lane sees a wall before they see a
        /// body, which is the entire read.
        /// </summary>
        private static Mesh BuildWarden()
        {
            var v = new List<Vector3>();
            var t = new List<int>();

            // Short, very thick legs. Wide stance, barely any taper — this one is rooted.
            AddPrism(v, t, new Vector3(-0.22f, 0f, 0.04f), new Vector2(0.26f, 0.28f),
                           new Vector3(-0.19f, 0.26f, 0.02f), new Vector2(0.23f, 0.25f));
            AddPrism(v, t, new Vector3(0.22f, 0f, 0.04f), new Vector2(0.26f, 0.28f),
                           new Vector3(0.19f, 0.26f, 0.02f), new Vector2(0.23f, 0.25f));

            // Torso: broad at the bottom too, so the whole figure is a block rather than
            // the wedge every other boss here is.
            AddPrism(v, t, new Vector3(0f, 0.22f, 0.02f), new Vector2(0.44f, 0.30f),
                           new Vector3(0f, 0.62f, 0f), new Vector2(0.50f, 0.32f));

            // Head sunk almost to the shoulder line, with a low flat crest.
            AddPrism(v, t, new Vector3(0f, 0.60f, -0.04f), new Vector2(0.20f, 0.20f),
                           new Vector3(0f, 0.74f, -0.05f), new Vector2(0.18f, 0.18f));
            AddBox(v, t, new Vector3(0f, 0.78f, -0.05f), new Vector3(0.06f, 0.10f, 0.26f));

            // Pauldrons, matched and huge — the only symmetric boss in the set, because
            // symmetry is what makes a thing read as a fortification.
            AddPrism(v, t, new Vector3(-0.44f, 0.52f, 0f), new Vector2(0.28f, 0.30f),
                           new Vector3(-0.48f, 0.70f, 0f), new Vector2(0.16f, 0.18f));
            AddPrism(v, t, new Vector3(0.44f, 0.52f, 0f), new Vector2(0.28f, 0.30f),
                           new Vector3(0.48f, 0.70f, 0f), new Vector2(0.16f, 0.18f));

            // The tower shield: a tall slab forward of the body, tilted back a little so
            // it catches the key light instead of facing flat into shadow.
            Quaternion tilt = Quaternion.Euler(-11f, 0f, 0f);
            AddOrientedBox(v, t, new Vector3(0.06f, 0.46f, -0.30f), new Vector3(0.62f, 0.86f, 0.07f), tilt);
            AddOrientedBox(v, t, new Vector3(0.06f, 0.46f, -0.35f), new Vector3(0.12f, 0.90f, 0.05f), tilt);

            return Finish(v, t, "Boss_Warden");
        }

        /// <summary>
        /// HOLLOW LEECH — the drain. Bent almost horizontal, head thrust forward BELOW the
        /// shoulder line, and two long arms reaching past it toward the army. Nothing else
        /// in the set leans; at range that forward mass is the whole identity, and it is
        /// also the honest picture of what the fight does to you.
        /// </summary>
        private static Mesh BuildLeech()
        {
            var v = new List<Vector3>();
            var t = new List<int>();

            // Long thin legs, splayed back — the weight is all in front of them.
            AddPrism(v, t, new Vector3(-0.17f, 0f, 0.10f), new Vector2(0.13f, 0.15f),
                           new Vector3(-0.14f, 0.44f, 0.02f), new Vector2(0.12f, 0.14f));
            AddPrism(v, t, new Vector3(0.17f, 0f, 0.10f), new Vector2(0.13f, 0.15f),
                           new Vector3(0.14f, 0.44f, 0.02f), new Vector2(0.12f, 0.14f));

            // Torso, hunched: it RISES only a little while travelling a long way forward.
            AddPrism(v, t, new Vector3(0f, 0.40f, 0.06f), new Vector2(0.26f, 0.22f),
                           new Vector3(0f, 0.64f, -0.22f), new Vector2(0.34f, 0.26f));

            // Head, out past the shoulders and DOWN — the posture of something feeding.
            AddPrism(v, t, new Vector3(0f, 0.60f, -0.26f), new Vector2(0.18f, 0.18f),
                           new Vector3(0f, 0.52f, -0.44f), new Vector2(0.11f, 0.11f));

            // Two long arms reaching further forward still, ending in splayed claws.
            for (int side = -1; side <= 1; side += 2)
            {
                AddPrism(v, t, new Vector3(side * 0.30f, 0.60f, -0.18f), new Vector2(0.12f, 0.12f),
                               new Vector3(side * 0.34f, 0.40f, -0.52f), new Vector2(0.07f, 0.07f));
                for (int finger = -1; finger <= 1; finger++)
                    AddPrism(v, t, new Vector3(side * 0.34f, 0.40f, -0.52f), new Vector2(0.05f, 0.05f),
                                   new Vector3(side * 0.34f + finger * 0.07f, 0.30f, -0.66f),
                                   new Vector2(0.015f, 0.015f));
            }

            // Spines up the back, rising behind the hunch so the outline is serrated
            // instead of smooth.
            for (int i = 0; i < 4; i++)
            {
                float f = i / 3f;
                AddPrism(v, t, new Vector3(0f, 0.50f + f * 0.14f, 0.06f - f * 0.20f),
                               new Vector2(0.07f, 0.07f),
                               new Vector3(0f, 0.66f + f * 0.20f, 0.14f - f * 0.20f),
                               new Vector2(0.02f, 0.02f));
            }

            return Finish(v, t, "Boss_Leech");
        }

        /// <summary>
        /// PALE SHEPHERD — the summoner. Tall, thin and vertical, under a HALO ring that
        /// floats clear of the head. The ring is the point: it is the only closed shape in
        /// the roster, it sits in empty sky where nothing occludes it, and it is where the
        /// adds visibly come from.
        /// </summary>
        private static Mesh BuildShepherd()
        {
            var v = new List<Vector3>();
            var t = new List<int>();

            // Narrow robe, nearly a column.
            AddPrism(v, t, new Vector3(0f, 0f, 0f), new Vector2(0.34f, 0.30f),
                           new Vector3(0f, 0.58f, 0f), new Vector2(0.22f, 0.19f));

            AddPrism(v, t, new Vector3(0f, 0.56f, 0f), new Vector2(0.24f, 0.20f),
                           new Vector3(0f, 0.86f, -0.02f), new Vector2(0.28f, 0.20f));

            AddPrism(v, t, new Vector3(0f, 0.84f, -0.03f), new Vector2(0.17f, 0.17f),
                           new Vector3(0f, 1.00f, -0.04f), new Vector2(0.15f, 0.15f));

            // The halo: eight blocks on a circle above the head. Eight is enough to read
            // as a ring in silhouette and cheap enough not to care.
            const int spokes = 8;
            for (int i = 0; i < spokes; i++)
            {
                float a = (float)(i * 2.0 * Math.PI / spokes);
                AddOrientedBox(v, t,
                    new Vector3(Mathf.Cos(a) * 0.30f, 1.16f, Mathf.Sin(a) * 0.30f),
                    new Vector3(0.11f, 0.05f, 0.05f),
                    Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f));
            }

            // Two braziers on tall poles, held out to either side — the sources.
            for (int side = -1; side <= 1; side += 2)
            {
                AddPrism(v, t, new Vector3(side * 0.36f, 0.10f, 0.02f), new Vector2(0.05f, 0.05f),
                               new Vector3(side * 0.42f, 0.86f, 0.02f), new Vector2(0.04f, 0.04f));
                AddPrism(v, t, new Vector3(side * 0.42f, 0.86f, 0.02f), new Vector2(0.16f, 0.16f),
                               new Vector3(side * 0.42f, 1.00f, 0.02f), new Vector2(0.20f, 0.20f));
            }

            return Finish(v, t, "Boss_Shepherd");
        }

        /// <summary>
        /// GORE HOUND — the enrage. FOUR LEGS, which is the single most legible difference
        /// available: at 16 m every other boss in this set is an upright biped, and a
        /// horizontal four-legged mass with its head low and forward cannot be mistaken for
        /// any of them even as a black shape.
        ///
        /// Deliberately the shortest of the six. It is not the least dangerous.
        /// </summary>
        private static Mesh BuildHound()
        {
            var v = new List<Vector3>();
            var t = new List<int>();

            // Four legs: front pair thicker and further apart, back pair tucked in.
            for (int side = -1; side <= 1; side += 2)
            {
                AddPrism(v, t, new Vector3(side * 0.26f, 0f, -0.22f), new Vector2(0.15f, 0.16f),
                               new Vector3(side * 0.22f, 0.40f, -0.18f), new Vector2(0.14f, 0.15f));
                AddPrism(v, t, new Vector3(side * 0.20f, 0f, 0.28f), new Vector2(0.13f, 0.14f),
                               new Vector3(side * 0.18f, 0.36f, 0.22f), new Vector2(0.13f, 0.15f));
            }

            // Body: long front to back, low, and slightly higher at the shoulder than at
            // the hip so the back slopes down and away.
            AddPrism(v, t, new Vector3(0f, 0.32f, 0.24f), new Vector2(0.34f, 0.26f),
                           new Vector3(0f, 0.42f, -0.24f), new Vector2(0.40f, 0.30f));

            // Neck and head, thrust forward and DOWN off the front of the body.
            AddPrism(v, t, new Vector3(0f, 0.44f, -0.26f), new Vector2(0.24f, 0.22f),
                           new Vector3(0f, 0.38f, -0.50f), new Vector2(0.20f, 0.19f));
            AddPrism(v, t, new Vector3(0f, 0.38f, -0.50f), new Vector2(0.20f, 0.19f),
                           new Vector3(0f, 0.31f, -0.68f), new Vector2(0.13f, 0.12f));

            // A pair of short back-swept horns off the skull.
            for (int side = -1; side <= 1; side += 2)
                AddPrism(v, t, new Vector3(side * 0.09f, 0.46f, -0.50f), new Vector2(0.06f, 0.06f),
                               new Vector3(side * 0.15f, 0.62f, -0.34f), new Vector2(0.018f, 0.018f));

            // Ridge of spines along the spine, tallest over the shoulder.
            for (int i = 0; i < 5; i++)
            {
                float f = i / 4f;
                float z = Mathf.Lerp(-0.20f, 0.26f, f);
                float h = Mathf.Lerp(0.26f, 0.10f, f);
                AddPrism(v, t, new Vector3(0f, 0.40f, z), new Vector2(0.08f, 0.09f),
                               new Vector3(0f, 0.40f + h, z + 0.05f), new Vector2(0.02f, 0.02f));
            }

            // Tail, low and trailing.
            AddPrism(v, t, new Vector3(0f, 0.34f, 0.30f), new Vector2(0.14f, 0.14f),
                           new Vector3(0f, 0.22f, 0.58f), new Vector2(0.04f, 0.04f));

            return Finish(v, t, "Boss_Hound");
        }

        /// <summary>
        /// What stands beside the road.
        ///
        /// Measured from the device screenshots, either side of the road was `(10, 8, 12)` in
        /// every frame — pure black. There was no background to be tired of, because there was
        /// no background: `SpawnGroundStrip` builds a ground box, four lane lines, two rails
        /// and rung decals, and nothing at all exists beyond x = +/-4.16 m.
        ///
        /// These are built from the same prism/oriented-box toolkit as the six bosses, kept
        /// cheap (four to eight hulls each) because a verge carries hundreds of them, and
        /// normalised to roughly one unit tall so the placer can scale them freely. All of
        /// them are drawn by SceneryField in one instanced call per kind, mixed through the
        /// imported Kenney pieces so the verge never reads as purely bought-in.
        /// </summary>
        public static Mesh Prop(PropKind kind)
        {
            int i = (int)kind;
            if (i < 0 || i >= _props.Length) i = 0;
            if (_props[i] == null) _props[i] = BuildProp((PropKind)i);
            return _props[i];
        }

        private static Mesh BuildProp(PropKind kind) => kind switch
        {
            PropKind.DeadTree => BuildDeadTree(),
            PropKind.BrokenColumn => BuildBrokenColumn(),
            PropKind.Brazier => BuildBrazier(),
            PropKind.Obelisk => BuildObelisk(),
            PropKind.HangingCage => BuildHangingCage(),
            PropKind.BoneArch => BuildBoneArch(),
            PropKind.RockSpire => BuildRockSpire(),
            PropKind.RuinedWall => BuildRuinedWall(),
            PropKind.Stump => BuildStump(),
            _ => BuildGravestone()
        };

        /// <summary>A leaning slab on a plinth. The lean is what stops a field of them reading as a fence.</summary>
        private static Mesh BuildGravestone()
        {
            var v = new List<Vector3>();
            var t = new List<int>();
            AddPrism(v, t, new Vector3(0f, 0f, 0f), new Vector2(0.58f, 0.40f),
                           new Vector3(0f, 0.10f, 0f), new Vector2(0.50f, 0.34f));
            AddOrientedBox(v, t, new Vector3(0.03f, 0.55f, 0f), new Vector3(0.42f, 0.92f, 0.13f),
                Quaternion.Euler(0f, 0f, -6f));
            AddPrism(v, t, new Vector3(0.06f, 0.98f, 0f), new Vector2(0.40f, 0.13f),
                           new Vector3(0.07f, 1.06f, 0f), new Vector2(0.26f, 0.10f));
            return Finish(v, t, "Prop_Gravestone");
        }

        /// <summary>Trunk and three bare branches, all leaning the same way, as if wind-set.</summary>
        private static Mesh BuildDeadTree()
        {
            var v = new List<Vector3>();
            var t = new List<int>();
            AddPrism(v, t, new Vector3(0f, 0f, 0f), new Vector2(0.26f, 0.26f),
                           new Vector3(0.05f, 0.62f, 0.02f), new Vector2(0.13f, 0.13f));
            AddPrism(v, t, new Vector3(0.05f, 0.62f, 0.02f), new Vector2(0.13f, 0.13f),
                           new Vector3(0.11f, 1.18f, 0.04f), new Vector2(0.05f, 0.05f));
            AddPrism(v, t, new Vector3(0.04f, 0.58f, 0f), new Vector2(0.09f, 0.09f),
                           new Vector3(-0.34f, 0.92f, 0.10f), new Vector2(0.02f, 0.02f));
            AddPrism(v, t, new Vector3(0.06f, 0.74f, 0f), new Vector2(0.08f, 0.08f),
                           new Vector3(0.40f, 1.02f, -0.12f), new Vector2(0.02f, 0.02f));
            AddPrism(v, t, new Vector3(0.08f, 0.90f, 0.02f), new Vector2(0.06f, 0.06f),
                           new Vector3(-0.16f, 1.24f, -0.14f), new Vector2(0.015f, 0.015f));
            return Finish(v, t, "Prop_DeadTree");
        }

        /// <summary>A shaft snapped off at an angle. Half a column reads older than a whole one.</summary>
        private static Mesh BuildBrokenColumn()
        {
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(0f, 0.06f, 0f), new Vector3(0.62f, 0.12f, 0.62f));
            AddPrism(v, t, new Vector3(0f, 0.12f, 0f), new Vector2(0.40f, 0.40f),
                           new Vector3(0f, 0.86f, 0f), new Vector2(0.34f, 0.34f));
            AddOrientedBox(v, t, new Vector3(0f, 0.92f, 0f), new Vector3(0.36f, 0.16f, 0.36f),
                Quaternion.Euler(13f, 0f, 9f));
            return Finish(v, t, "Prop_BrokenColumn");
        }

        /// <summary>Three legs and a bowl. The only prop that looks like someone lit it.</summary>
        private static Mesh BuildBrazier()
        {
            var v = new List<Vector3>();
            var t = new List<int>();
            for (int i = 0; i < 3; i++)
            {
                float a = i * Mathf.PI * 2f / 3f;
                var foot = new Vector3(Mathf.Cos(a) * 0.26f, 0f, Mathf.Sin(a) * 0.26f);
                AddPrism(v, t, foot, new Vector2(0.10f, 0.10f),
                               new Vector3(0f, 0.52f, 0f), new Vector2(0.08f, 0.08f));
            }
            AddPrism(v, t, new Vector3(0f, 0.50f, 0f), new Vector2(0.30f, 0.30f),
                           new Vector3(0f, 0.74f, 0f), new Vector2(0.50f, 0.50f));
            return Finish(v, t, "Prop_Brazier");
        }

        /// <summary>A tall four-sided taper under a pyramid cap. The tallest thing on a verge.</summary>
        private static Mesh BuildObelisk()
        {
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(0f, 0.05f, 0f), new Vector3(0.52f, 0.10f, 0.52f));
            AddPrism(v, t, new Vector3(0f, 0.10f, 0f), new Vector2(0.36f, 0.36f),
                           new Vector3(0f, 1.18f, 0f), new Vector2(0.17f, 0.17f));
            AddPrism(v, t, new Vector3(0f, 1.18f, 0f), new Vector2(0.17f, 0.17f),
                           new Vector3(0f, 1.36f, 0f), new Vector2(0.02f, 0.02f));
            return Finish(v, t, "Prop_Obelisk");
        }

        /// <summary>A gibbet: post, arm, chain and an empty cage. Reads at any distance.</summary>
        private static Mesh BuildHangingCage()
        {
            var v = new List<Vector3>();
            var t = new List<int>();
            AddPrism(v, t, new Vector3(0f, 0f, 0f), new Vector2(0.20f, 0.20f),
                           new Vector3(0f, 1.20f, 0f), new Vector2(0.13f, 0.13f));
            AddOrientedBox(v, t, new Vector3(0.24f, 1.16f, 0f), new Vector3(0.52f, 0.09f, 0.09f),
                Quaternion.identity);
            AddBox(v, t, new Vector3(0.46f, 1.02f, 0f), new Vector3(0.035f, 0.24f, 0.035f));
            // Cage: a lid, a floor and four uprights. Hollow, so the silhouette has holes in
            // it, which is the entire reason to hang one beside a road.
            AddBox(v, t, new Vector3(0.46f, 0.90f, 0f), new Vector3(0.30f, 0.05f, 0.30f));
            AddBox(v, t, new Vector3(0.46f, 0.54f, 0f), new Vector3(0.30f, 0.05f, 0.30f));
            for (int i = 0; i < 4; i++)
            {
                float dx = (i % 2 == 0 ? -1f : 1f) * 0.13f;
                float dz = (i < 2 ? -1f : 1f) * 0.13f;
                AddBox(v, t, new Vector3(0.46f + dx, 0.72f, dz), new Vector3(0.04f, 0.36f, 0.04f));
            }
            return Finish(v, t, "Prop_HangingCage");
        }

        /// <summary>Two ribs meeting overhead. Three segments a side so the curve reads as a curve.</summary>
        private static Mesh BuildBoneArch()
        {
            var v = new List<Vector3>();
            var t = new List<int>();
            for (int side = -1; side <= 1; side += 2)
            {
                AddPrism(v, t, new Vector3(side * 0.52f, 0f, 0f), new Vector2(0.18f, 0.18f),
                               new Vector3(side * 0.40f, 0.48f, 0f), new Vector2(0.13f, 0.13f));
                AddPrism(v, t, new Vector3(side * 0.40f, 0.48f, 0f), new Vector2(0.13f, 0.13f),
                               new Vector3(side * 0.22f, 0.86f, 0f), new Vector2(0.10f, 0.10f));
                AddPrism(v, t, new Vector3(side * 0.22f, 0.86f, 0f), new Vector2(0.10f, 0.10f),
                               new Vector3(side * 0.03f, 1.04f, 0f), new Vector2(0.08f, 0.08f));
            }
            return Finish(v, t, "Prop_BoneArch");
        }

        /// <summary>Three stacked leaning blocks. Asymmetric on purpose — rock is never a cone.</summary>
        private static Mesh BuildRockSpire()
        {
            var v = new List<Vector3>();
            var t = new List<int>();
            AddPrism(v, t, new Vector3(0f, 0f, 0f), new Vector2(0.62f, 0.54f),
                           new Vector3(0.07f, 0.52f, 0.04f), new Vector2(0.34f, 0.30f));
            AddPrism(v, t, new Vector3(0.07f, 0.52f, 0.04f), new Vector2(0.34f, 0.30f),
                           new Vector3(0.17f, 0.96f, -0.05f), new Vector2(0.18f, 0.16f));
            AddPrism(v, t, new Vector3(0.17f, 0.96f, -0.05f), new Vector2(0.18f, 0.16f),
                           new Vector3(0.12f, 1.26f, -0.02f), new Vector2(0.04f, 0.04f));
            return Finish(v, t, "Prop_RockSpire");
        }

        /// <summary>Three courses at three heights — a wall that stopped being a wall.</summary>
        private static Mesh BuildRuinedWall()
        {
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(-0.52f, 0.30f, 0f), new Vector3(0.52f, 0.60f, 0.26f));
            AddBox(v, t, new Vector3(0f, 0.43f, 0f), new Vector3(0.50f, 0.86f, 0.26f));
            AddBox(v, t, new Vector3(0.50f, 0.22f, 0f), new Vector3(0.48f, 0.44f, 0.26f));
            AddOrientedBox(v, t, new Vector3(0.86f, 0.10f, 0.18f), new Vector3(0.30f, 0.20f, 0.24f),
                Quaternion.Euler(0f, 24f, 11f));
            return Finish(v, t, "Prop_RuinedWall");
        }

        /// <summary>A cut trunk with the splinters still on it. Low, so it breaks up a skyline of tall things.</summary>
        private static Mesh BuildStump()
        {
            var v = new List<Vector3>();
            var t = new List<int>();
            AddPrism(v, t, new Vector3(0f, 0f, 0f), new Vector2(0.60f, 0.56f),
                           new Vector3(0.02f, 0.34f, 0f), new Vector2(0.44f, 0.42f));
            AddOrientedBox(v, t, new Vector3(-0.10f, 0.44f, 0.05f), new Vector3(0.14f, 0.24f, 0.12f),
                Quaternion.Euler(0f, 20f, -14f));
            AddOrientedBox(v, t, new Vector3(0.14f, 0.40f, -0.08f), new Vector3(0.11f, 0.17f, 0.10f),
                Quaternion.Euler(0f, -35f, 17f));
            return Finish(v, t, "Prop_Stump");
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
        ///
        /// The two ends are SWAPPED when the caller's "top" is actually lower. AddHull
        /// takes corners 0-3 as the bottom face and winds every quad from that assumption,
        /// so a segment that descends — a head thrust down and forward, a trailing tail —
        /// would come out inside-out: normals pointing inward, every face backface-culled,
        /// and the limb rendering as a hole. Swapping is the whole fix and it changes
        /// nothing for a segment that rises.
        /// </summary>
        private static void AddPrism(List<Vector3> vertices, List<int> triangles,
            Vector3 bottomCenter, Vector2 bottomSize, Vector3 topCenter, Vector2 topSize)
        {
            if (topCenter.y < bottomCenter.y)
            {
                Vector3 swapC = bottomCenter; bottomCenter = topCenter; topCenter = swapC;
                Vector2 swapS = bottomSize; bottomSize = topSize; topSize = swapS;
            }

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
