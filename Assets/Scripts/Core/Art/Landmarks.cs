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
            BuildSiegeCamp()
        };

        public static Landmark ByName(string name)
        {
            foreach (Landmark l in All)
                if (l.Name == name) return l;
            return null;
        }
    }
}
