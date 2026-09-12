namespace BattleRunner.Core.Art
{
    /// <summary>One piece of a landmark, positioned in the landmark's own local space.</summary>
    public readonly struct LandmarkPart
    {
        public readonly string Piece;
        public readonly float X, Y, Z, Yaw, Scale;

        public LandmarkPart(string piece, float x, float y, float z, float yaw = 0f, float scale = 1f)
        {
            Piece = piece;
            X = x; Y = y; Z = z;
            Yaw = yaw;
            Scale = scale;
        }
    }

    /// <summary>
    /// A structure assembled from pack pieces: a castle, a cottage, a mill, a crypt.
    ///
    /// WHY PIECES AND NOT ONE BAKED MESH. A castle baked flat is 7,100 vertices and one draw
    /// call — but six castles on screen are six draws of 7,100. Kept as parts, the same six
    /// castles are still three draws in total, because every wall in the level lands in the
    /// same instancing bucket no matter which castle it belongs to. It also means a landmark
    /// is DATA, which is what lets a test walk one.
    ///
    /// Local space is Kenney's: roughly one unit per module, y up, and the field multiplies
    /// the whole thing by a world scale when it places it.
    /// </summary>
    public sealed class Landmark
    {
        public readonly string Name;
        public readonly LandmarkPart[] Parts;
        /// <summary>Half-width in local units. The field keeps this clear of the road.</summary>
        public readonly float Radius;

        public Landmark(string name, float radius, LandmarkPart[] parts)
        {
            Name = name;
            Radius = radius;
            Parts = parts;
        }
    }

    /// <summary>
    /// Every landmark the game can build. Assembled by hand from measured module sizes —
    /// a castle wall is 1.00 x 1.00 in plan and 1.31 tall, a tower segment is 1.01 tall, a
    /// town wall is a panel on the +x face of a unit cell — so the courses line up rather
    /// than nearly lining up.
    /// </summary>
    public static class Landmarks
    {
        public const string Keep = "keep";
        public const string Cottage = "cottage";
        public const string Windmill = "windmill";
        public const string Watermill = "watermill";
        public const string Mausoleum = "mausoleum";
        public const string Ruin = "ruin";
        public const string Outcrop = "outcrop";
        public const string PineStand = "pinestand";
        public const string SiegeCamp = "siegecamp";

        // EIGHT MORE, ONE PER WORLD, FROM PARTS ALREADY IN THE PACK.
        //
        // Nine landmarks served eight worlds, and two of them did most of the work: Ruin
        // appeared in five worlds and Mausoleum in five, so 1-1 and 2-1 shared both their
        // low walls and their only house. Sixteen baked modules — the whole roof, corner and
        // tower vocabulary — had never been referenced by anything at all.
        //
        // A landmark is a PART LIST, so every one of these costs zero fetch, zero bake and
        // zero additional draw calls: a chapel's walls land in the same instancing bucket as
        // a cottage's. The only thing being added is arrangement.
        public const string Chapel = "chapel";
        public const string StiltHouse = "stilthouse";
        public const string ColumnHall = "columnhall";
        public const string Smithy = "smithy";
        public const string BoneShrine = "boneshrine";
        public const string Watchtower = "watchtower";
        public const string Gatehouse = "gatehouse";
        public const string Manor = "manor";

        /// <summary>A square tower: base, middle, roof, and a flag if it is a corner one.</summary>
        private static void Tower(System.Collections.Generic.List<LandmarkPart> into,
            float x, float z, bool flagged)
        {
            into.Add(new LandmarkPart("ca_tower-square-base", x, 0f, z));
            into.Add(new LandmarkPart("ca_tower-square-mid", x, 1.01f, z));
            into.Add(new LandmarkPart("ca_tower-square-roof", x, 2.02f, z));
            if (flagged) into.Add(new LandmarkPart("ca_flag", x, 4.03f, z));
        }

        private static Landmark BuildKeep()
        {
            var parts = new System.Collections.Generic.List<LandmarkPart>();
            // Two courses of wall front and back, two along the sides, corner towers, and a
            // gate in the middle of the front wall. Walls tile at exactly 1.0 because that is
            // their measured plan size; anything else leaves a seam a fog bank cannot hide.
            for (int i = -2; i <= 2; i++)
            {
                if (i != 0) parts.Add(new LandmarkPart("ca_wall", i, 0f, -2.5f));
                parts.Add(new LandmarkPart("ca_wall", i, 0f, 2.5f, 180f));
            }
            parts.Add(new LandmarkPart("ca_gate", 0f, 0f, -2.5f));
            parts.Add(new LandmarkPart("ca_wall-doorway", 0f, 0f, -2.5f));
            for (int k = -1; k <= 1; k++)
            {
                parts.Add(new LandmarkPart("ca_wall", -2.5f, 0f, k, 90f));
                parts.Add(new LandmarkPart("ca_wall", 2.5f, 0f, k, 270f));
            }
            Tower(parts, -2.5f, -2.5f, true);
            Tower(parts, 2.5f, -2.5f, true);
            Tower(parts, -2.5f, 2.5f, false);
            Tower(parts, 2.5f, 2.5f, false);
            // The inner keep, taller than the curtain so the silhouette has a peak.
            parts.Add(new LandmarkPart("ca_tower-square-base", 0f, 0f, 0.5f, 0f, 1.6f));
            parts.Add(new LandmarkPart("ca_tower-square-mid", 0f, 1.62f, 0.5f, 0f, 1.6f));
            parts.Add(new LandmarkPart("ca_tower-square-roof", 0f, 3.23f, 0.5f, 0f, 1.6f));
            parts.Add(new LandmarkPart("ca_flag-wide", 0f, 6.45f, 0.5f, 0f, 1.4f));
            return new Landmark(Keep, 3.3f, parts.ToArray());
        }

        /// <summary>
        /// A house. The town kit's wall is a PANEL on the +x face of a unit cell, not a box,
        /// so four rotated copies make the four walls and the cell interior stays hollow —
        /// which is free, because nothing ever sees inside it.
        /// </summary>
        private static Landmark BuildCottage()
        {
            var parts = new System.Collections.Generic.List<LandmarkPart>
            {
                new LandmarkPart("fa_wall-door", 0f, 0f, 0f, 0f),
                new LandmarkPart("fa_wall", 0f, 0f, 0f, 90f),
                new LandmarkPart("fa_wall", 0f, 0f, 0f, 180f),
                new LandmarkPart("fa_wall", 0f, 0f, 0f, 270f),
                new LandmarkPart("fa_roof-high", 0f, 1f, 0f),
                new LandmarkPart("fa_chimney", 0.3f, 1f, -0.3f, 0f, 0.7f),
                new LandmarkPart("fa_fence", 1.4f, 0f, 0f, 90f),
                new LandmarkPart("fa_fence", 1.4f, 0f, 1f, 90f),
                new LandmarkPart("fa_cart", -1.5f, 0f, 0.8f, 35f)
            };
            return new Landmark(Cottage, 2.2f, parts.ToArray());
        }

        private static Landmark BuildMausoleum()
        {
            var parts = new System.Collections.Generic.List<LandmarkPart>
            {
                new LandmarkPart("gr_crypt-large", 0f, 0f, 0f, 0f, 1.4f),
                new LandmarkPart("gr_crypt-door", 0f, 0f, -1.1f, 0f, 1.2f),
                // The columns carry the height. At 1.13 units a single one is 5.7 m at
                // landmark scale, which reads as a large building rather than as a landmark;
                // stacked and enlarged they clear the treeline instead.
                new LandmarkPart("gr_column-large", -1.5f, 0f, -1.3f, 0f, 1.5f),
                new LandmarkPart("gr_column-large", 1.5f, 0f, -1.3f, 0f, 1.5f),
                new LandmarkPart("gr_column-large", -1.5f, 1.7f, -1.3f, 0f, 1.5f),
                new LandmarkPart("gr_column-large", 1.5f, 1.7f, -1.3f, 0f, 1.5f),
                new LandmarkPart("gr_crypt", 0f, 1.4f, 0f, 0f, 1.1f),
                new LandmarkPart("gr_crypt", -2.4f, 0f, 1.1f, 20f, 0.8f),
                new LandmarkPart("gr_crypt", 2.4f, 0f, 1.4f, -15f, 0.8f)
            };
            return new Landmark(Mausoleum, 3.0f, parts.ToArray());
        }

        private static Landmark BuildRuin()
        {
            // A keep that lost. Broken walls, one surviving tower stump, no roof and no flag —
            // the same vocabulary as the Keep so a player reads it as the same civilisation.
            var parts = new System.Collections.Generic.List<LandmarkPart>
            {
                new LandmarkPart("ca_wall", -1.5f, 0f, -1.5f),
                new LandmarkPart("ca_wall-half", -0.5f, 0f, -1.5f),
                new LandmarkPart("ca_wall-narrow", 1.2f, 0f, -1.5f, 12f),
                new LandmarkPart("ca_wall", -1.5f, 0f, 0.5f, 90f),
                new LandmarkPart("ca_wall-half", -1.5f, 0f, 1.5f, 90f),
                new LandmarkPart("ca_tower-square-base", 1.8f, 0f, 1.2f),
                new LandmarkPart("ca_tower-square-mid", 1.8f, 1.01f, 1.2f),
                new LandmarkPart("ca_stairs-stone", 0.4f, 0f, 0.6f, 45f),
                new LandmarkPart("fa_wall-broken", -0.6f, 0f, 2.2f, 200f)
            };
            return new Landmark(Ruin, 2.9f, parts.ToArray());
        }

        private static Landmark BuildOutcrop()
        {
            var parts = new System.Collections.Generic.List<LandmarkPart>
            {
                new LandmarkPart("na_cliff_rock", 0f, 0f, 0f, 0f, 1.6f),
                new LandmarkPart("na_cliff_rock", 1.4f, 0f, 0.6f, 130f, 1.2f),
                new LandmarkPart("na_cliff_top_rock", 0f, 1.6f, 0f, 40f, 1.3f),
                new LandmarkPart("na_statue_head", -1.6f, 0f, 0.4f, 25f, 0.9f)
            };
            return new Landmark(Outcrop, 2.3f, parts.ToArray());
        }

        private static Landmark BuildPineStand()
        {
            var parts = new System.Collections.Generic.List<LandmarkPart>
            {
                new LandmarkPart("na_tree_pineDefaultB", 0f, 0f, 0f, 0f, 1.4f),
                new LandmarkPart("na_tree_pineDefaultB", 1.6f, 0f, 1.1f, 90f, 1.1f),
                new LandmarkPart("na_tree_pineGroundA", -1.4f, 0f, 0.8f, 200f, 1.3f),
                new LandmarkPart("na_tree_pineGroundA", 0.6f, 0f, -1.5f, 310f, 1.0f)
            };
            return new Landmark(PineStand, 2.4f, parts.ToArray());
        }

        private static Landmark BuildSiegeCamp()
        {
            var parts = new System.Collections.Generic.List<LandmarkPart>
            {
                new LandmarkPart("ca_siege-tower", 0f, 0f, 0f, 15f),
                new LandmarkPart("su_tent", -2.2f, 0f, 0.8f, 40f, 1.4f),
                new LandmarkPart("su_tent", -1.4f, 0f, 2.2f, 120f, 1.2f),
                new LandmarkPart("su_barrel", -0.6f, 0f, 1.4f, 0f, 1.2f),
                new LandmarkPart("fa_cart", 1.8f, 0f, 1.2f, 250f)
            };
            return new Landmark(SiegeCamp, 3.1f, parts.ToArray());
        }


        /// <summary>A wayside chapel: four walls, a gabled roof, a low yard wall and graves.</summary>
        private static Landmark BuildChapel()
        {
            var parts = new System.Collections.Generic.List<LandmarkPart>
            {
                // The fa_ walls are panels on ONE face of a unit cell, so four of them at the
                // same origin with four yaws is a closed room. Same trick the cottage uses.
                new LandmarkPart("fa_wall-door", 0f, 0f, 0f),
                new LandmarkPart("fa_wall", 0f, 0f, 0f, 90f),
                new LandmarkPart("fa_wall", 0f, 0f, 0f, 180f),
                new LandmarkPart("fa_wall", 0f, 0f, 0f, 270f),
                new LandmarkPart("fa_roof-gable", 0f, 1f, 0f),
                // A bell tower, because a landmark has to clear 6 m at the world's landmark
                // scale to read as one from the road — the nave alone tops out at 5.3.
                new LandmarkPart("ca_tower-square-base", 1.3f, 0f, -0.9f, 0f, 0.75f),
                new LandmarkPart("ca_tower-square-mid", 1.3f, 0.758f, -0.9f, 0f, 0.75f),
                new LandmarkPart("ca_tower-square-roof", 1.3f, 1.515f, -0.9f, 0f, 0.75f),
                new LandmarkPart("ca_wall-half", 1.8f, 0f, 0.4f),
                new LandmarkPart("gr_cross", -1.6f, 0f, 0.9f, 15f, 1.1f),
                new LandmarkPart("gr_gravestone-round", -1.9f, 0f, -0.6f, 40f)
            };
            return new Landmark(Chapel, 2.6f, parts.ToArray());
        }

        /// <summary>A hut on a plank platform, for the worlds standing in water.</summary>
        private static Landmark BuildStiltHouse()
        {
            var parts = new System.Collections.Generic.List<LandmarkPart>
            {
                new LandmarkPart("fa_planks", 0f, 0f, 0f, 0f, 2.2f),
                new LandmarkPart("fa_wall-wood", 0f, 0.06f, 0f),
                new LandmarkPart("fa_wall-wood", 0f, 0.06f, 0f, 90f),
                new LandmarkPart("fa_wall-wood", 0f, 0.06f, 0f, 180f),
                new LandmarkPart("fa_wall-wood", 0f, 0.06f, 0f, 270f),
                new LandmarkPart("fa_roof-flat", 0f, 1.06f, 0f),
                new LandmarkPart("fa_overhang", 0f, 1.06f, 0.9f),
                new LandmarkPart("fa_chimney", -0.3f, 1.186f, -0.3f, 0f, 0.9f),
                new LandmarkPart("na_stump_oldTall", -2f, 0f, 1f, 30f),
                new LandmarkPart("na_stump_oldTall", 2.1f, 0f, -0.8f, 200f, 0.9f)
            };
            return new Landmark(StiltHouse, 2.7f, parts.ToArray());
        }

        /// <summary>Four columns and a slab: what is left of a hall, not a ruin of a house.</summary>
        private static Landmark BuildColumnHall()
        {
            var parts = new System.Collections.Generic.List<LandmarkPart>();
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                parts.Add(new LandmarkPart("gr_column-large", sx * 1.4f, 0f, sz * 1.4f, 0f, 1.6f));
            // 1.13 tall at 1.6 scale is 1.808, which is where the slab goes. Measured, not
            // guessed: the stacking test refuses anything that floats by more than 5 cm.
            parts.Add(new LandmarkPart("fa_roof-flat", 0f, 1.808f, 0f, 0f, 3f));
            parts.Add(new LandmarkPart("ca_stairs-stone", 0f, 0f, -2.2f, 0f, 1.2f));
            parts.Add(new LandmarkPart("gr_crypt", 0f, 0f, 0.8f, 0f, 1.1f));
            parts.Add(new LandmarkPart("gr_column-large", -2.3f, 0f, 0f, 25f));
            return new Landmark(ColumnHall, 3f, parts.ToArray());
        }

        /// <summary>A forge: three walls, an open front, a chimney and barrels.</summary>
        private static Landmark BuildSmithy()
        {
            var parts = new System.Collections.Generic.List<LandmarkPart>
            {
                new LandmarkPart("fa_wall", 0f, 0f, 0f),
                new LandmarkPart("fa_wall", 0f, 0f, 0f, 90f),
                new LandmarkPart("fa_wall", 0f, 0f, 0f, 180f),
                // The fourth side is a HALF wall, so the forge is open to the road and reads
                // as a working building rather than a sealed box.
                new LandmarkPart("fa_wall-half", 0f, 0f, 0f, 270f),
                new LandmarkPart("fa_roof-point", 0f, 1f, 0f),
                new LandmarkPart("fa_chimney", 0.35f, 1f, -0.35f),
                new LandmarkPart("fa_overhang", 0f, 1f, 1f, 0f, 1.2f),
                new LandmarkPart("su_barrel", 1.7f, 0f, 0.9f, 0f, 1.2f),
                new LandmarkPart("su_barrel", 2f, 0f, 0.2f, 40f),
                new LandmarkPart("fa_cart", -1.9f, 0f, 0.7f, 300f)
            };
            return new Landmark(Smithy, 2.8f, parts.ToArray());
        }

        /// <summary>A head on a plinth among broken rock. No architecture; a marker.</summary>
        private static Landmark BuildBoneShrine()
        {
            var parts = new System.Collections.Generic.List<LandmarkPart>
            {
                new LandmarkPart("ca_tower-base", 0f, 0f, 0f, 0f, 1.4f),
                new LandmarkPart("ca_tower-top", 0f, 1.834f, 0f, 0f, 1.4f),
                new LandmarkPart("na_statue_head", 0f, 1.964f, 0f, 25f, 1.2f),
                new LandmarkPart("na_cliff_rock", -1.8f, 0f, 0.7f, 120f, 1.2f),
                new LandmarkPart("na_cliff_rock", 1.9f, 0f, -0.5f, 20f),
                new LandmarkPart("gr_column-large", 1.3f, 0f, 1.6f, 0f, 0.9f)
            };
            return new Landmark(BoneShrine, 2.6f, parts.ToArray());
        }

        /// <summary>A round tower, flagged, on a broken curtain wall. The tallest of the eight.</summary>
        private static Landmark BuildWatchtower()
        {
            var parts = new System.Collections.Generic.List<LandmarkPart>
            {
                new LandmarkPart("ca_tower-base", 0f, 0f, 0f, 0f, 1.3f),
                new LandmarkPart("ca_tower-square", 0f, 1.703f, 0f, 0f, 1.3f),
                new LandmarkPart("ca_tower-square-top", 0f, 3.406f, 0f, 0f, 1.3f),
                new LandmarkPart("ca_tower-top", 0f, 3.796f, 0f, 0f, 1.3f),
                new LandmarkPart("ca_flag", 0f, 3.965f, 0f, 0f, 1.1f),
                new LandmarkPart("ca_wall-corner", -1.7f, 0f, -1.7f),
                new LandmarkPart("ca_wall-narrow", 1.6f, 0f, 1.2f, 90f),
                new LandmarkPart("na_rock_tallA", -1.8f, 0f, 1.1f, 60f)
            };
            return new Landmark(Watchtower, 2.4f, parts.ToArray());
        }

        /// <summary>A gate between two square towers: the only landmark you could walk through.</summary>
        private static Landmark BuildGatehouse()
        {
            var parts = new System.Collections.Generic.List<LandmarkPart>
            {
                new LandmarkPart("ca_gate", 0f, 0f, 0f, 0f, 1.3f),
                new LandmarkPart("ca_wall-doorway", 0f, 0f, 0f, 0f, 1.3f)
            };
            for (int sx = -1; sx <= 1; sx += 2)
            {
                float x = sx * 1.7f;
                parts.Add(new LandmarkPart("ca_tower-square-base", x, 0f, 0f, 0f, 1.2f));
                parts.Add(new LandmarkPart("ca_tower-square-mid", x, 1.212f, 0f, 0f, 1.2f));
                parts.Add(new LandmarkPart("ca_tower-square-roof", x, 2.424f, 0f, 0f, 1.2f));
            }
            parts.Add(new LandmarkPart("ca_wall-corner", -2.6f, 0f, 0.9f, 0f, 1.1f));
            parts.Add(new LandmarkPart("ca_wall", 2.6f, 0f, 0.9f, 180f, 1.1f));
            return new Landmark(Gatehouse, 3.2f, parts.ToArray());
        }

        /// <summary>A two-wing house with a corner roof — the largest domestic building.</summary>
        private static Landmark BuildManor()
        {
            var parts = new System.Collections.Generic.List<LandmarkPart>
            {
                new LandmarkPart("fa_wall-corner", 0f, 0f, 0f),
                new LandmarkPart("fa_wall", 0f, 0f, 0f, 90f),
                new LandmarkPart("fa_wall-door", 0f, 0f, 0f, 180f),
                new LandmarkPart("fa_wall-corner", 0f, 0f, 0f, 270f),
                new LandmarkPart("fa_roof", 0f, 1f, 0f),
                // The second wing: a half-height range off one side under its own corner roof,
                // which is what stops this reading as the cottage at a larger scale.
                new LandmarkPart("fa_wall-half", 1.5f, 0f, 0.6f),
                new LandmarkPart("fa_wall-half", 1.5f, 0f, 0.6f, 180f),
                new LandmarkPart("fa_roof-corner", 1.5f, 0.5f, 0.6f, 0f, 0.8f),
                new LandmarkPart("fa_chimney", -0.4f, 1f, -0.4f, 0f, 1.1f),
                new LandmarkPart("fa_fence-gate", -1.9f, 0f, 0.8f, 90f),
                new LandmarkPart("na_fence_simple", -2f, 0f, -0.6f, 90f, 1.2f)
            };
            return new Landmark(Manor, 2.7f, parts.ToArray());
        }

        public static readonly Landmark[] All =
        {
            BuildKeep(),
            BuildCottage(),
            new Landmark(Windmill, 3.3f, new[]
            {
                new LandmarkPart("fa_windmill", 0f, 0f, 0f),
                new LandmarkPart("fa_fence", 1.6f, 0f, 0.4f, 90f),
                new LandmarkPart("na_crops_bambooStageA", 2.4f, 0f, 1.2f, 0f, 1.4f)
            }),
            new Landmark(Watermill, 2.9f, new[]
            {
                new LandmarkPart("fa_watermill", 0f, 0f, 0f),
                new LandmarkPart("fa_wall-wood", 1.3f, 0f, 0.6f, 270f),
                new LandmarkPart("fa_planks", 1.9f, 0f, 0f, 0f, 1.5f)
            }),
            BuildMausoleum(),
            BuildRuin(),
            BuildOutcrop(),
            BuildPineStand(),
            BuildSiegeCamp(),
            BuildChapel(),
            BuildStiltHouse(),
            BuildColumnHall(),
            BuildSmithy(),
            BuildBoneShrine(),
            BuildWatchtower(),
            BuildGatehouse(),
            BuildManor()
        };

        public static Landmark ByName(string name)
        {
            foreach (Landmark l in All)
                if (l.Name == name) return l;
            return null;
        }
    }
}
