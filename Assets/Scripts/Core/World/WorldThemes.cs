using System;
using System.Collections.Generic;
using BattleRunner.Core.Art;

namespace BattleRunner.Core.World
{
    /// <summary>
    /// The eight worlds, authored.
    ///
    /// The game shipped with ONE. Measured across nine device frames, the sky above the
    /// horizon was (11,10,15) +/- 2 in every single one and the road in front of the camera
    /// was (25-29, 25-31, 37-46) in all of them — two different levels and two different
    /// bosses, one look. `EnvironmentLook.Apply()` took no arguments and ran once at boot,
    /// before a level existed, and `TrackController` built the road material from constants in
    /// `Initialize`. There was nowhere for a second world to come from.
    ///
    /// AN ACT WEARS ONE OF THESE, and `ThemeVariant` bends it per round, so eight authored
    /// worlds carry an endless game without eight becoming a visible cycle.
    ///
    /// The first entry reproduces the shipped look exactly. That is deliberate: it is the one
    /// palette that has been seen on a real screen and judged, so it stays the baseline the
    /// other seven are pushed away from.
    /// </summary>
    public static class WorldThemes
    {
        /// <summary>
        /// The furthest any world may push its fog. The ground strip is built to run past
        /// this, and the camera's far clip past that again — a world whose fog ended beyond
        /// the road would show the player the end of the world.
        /// </summary>
        public const float MaxFogEnd = 185f;

        private static readonly WorldTheme[] Table =
        {
            // ---- 0 : the shipped look, kept as the baseline -----------------
            new WorldTheme
            {
                DisplayName = "The Ashen Road",
                RoadStone = new Rgb(0.310f, 0.295f, 0.285f),
                RoadMortar = new Rgb(0.145f, 0.138f, 0.135f),
                RoadDamp = new Rgb(0.300f, 0.330f, 0.440f),
                RoadTiling = 1.60f, RoadWetness = 0.55f, RoadGloss = 8f, RoadStoneVariation = 0.45f,
                SkyZenith = new Rgb(0.022f, 0.020f, 0.055f),
                SkyHorizon = new Rgb(0.085f, 0.070f, 0.125f),
                SkyGlow = new Rgb(0.500f, 0.330f, 0.230f),
                SkyStars = 1.6f, SkyGlowYaw = 0.0f,
                Fog = new Rgb(0.440f, 0.300f, 0.230f), FogStart = 70f, FogEnd = 170f,
                LightColor = new Rgb(0.750f, 0.780f, 0.950f),
                LightIntensity = 1.10f, LightPitch = 32f, LightYaw = 250f,
                // Ground: ash over dead grass; the baseline, and the only world whose ground is meant to be forgettable.
                Ground = new Rgb(0.228f, 0.200f, 0.152f),
                GroundAlt = new Rgb(0.296f, 0.260f, 0.188f),
                GroundPatchScale = 0.09f, GroundSpeckle = 0.32f,
                GroundSheen = new Rgb(0.30f, 0.33f, 0.44f), GroundSheenStrength = 0.10f,
                Scenery = new SceneryPalette
                {
                    Verge = new[]
                    {
                        "gr_gravestone-round",
                        "gr_gravestone-wide",
                        "gr_gravestone-broken",
                        "gr_cross",
                        "gr_grave",
                        "na_stump_old",
                        "na_rock_smallA",
                        "na_grass",
                    },
                    Field = new[]
                    {
                        "na_tree_default",
                        "na_tree_thin",
                        "na_tree_simple",
                        "gr_iron-fence",
                        "gr_brick-wall",
                        "fa_fence-broken",
                        "na_rock_largeA",
                    },
                    Landmarks = new[]
                    {
                        Landmarks.Ruin,
                        Landmarks.Cottage,
                        Landmarks.Mausoleum,
                    },
                    VergeDensity = 15f, FieldDensity = 9f,
                    LandmarkSpacing = 105f,
                    VergeTint = 0.74f, FieldTint = 0.3f, LandmarkTint = 0.12f
                },
                Props = new[] { PropKind.Gravestone, PropKind.DeadTree, PropKind.BrokenColumn },
                PropDensity = 9f,
                PropStone = new Rgb(0.280f, 0.268f, 0.255f),
                Accent = new Rgb(0.85f, 0.55f, 0.30f)
            },

            // ---- 1 : green, low, and drowning -------------------------------
            new WorldTheme
            {
                DisplayName = "Gallows Mire",
                RoadStone = new Rgb(0.240f, 0.260f, 0.200f),
                RoadMortar = new Rgb(0.110f, 0.120f, 0.100f),
                RoadDamp = new Rgb(0.260f, 0.360f, 0.280f),
                RoadTiling = 1.35f, RoadWetness = 0.80f, RoadGloss = 10f, RoadStoneVariation = 0.52f,
                SkyZenith = new Rgb(0.020f, 0.028f, 0.030f),
                SkyHorizon = new Rgb(0.070f, 0.095f, 0.070f),
                SkyGlow = new Rgb(0.280f, 0.420f, 0.200f),
                SkyStars = 0.50f, SkyGlowPower = 6f, SkyGlowYaw = 26.0f,
                Fog = new Rgb(0.266f, 0.389f, 0.200f), FogStart = 45f, FogEnd = 120f,
                LightColor = new Rgb(0.700f, 0.820f, 0.720f),
                LightIntensity = 0.95f, LightPitch = 26f, LightYaw = 215f,
                // Ground: standing bog water between sedge; the coarsest patches in the game and the second wettest.
                Ground = new Rgb(0.095f, 0.138f, 0.088f),
                GroundAlt = new Rgb(0.152f, 0.200f, 0.108f),
                GroundPatchScale = 0.06f, GroundSpeckle = 0.26f,
                GroundSheen = new Rgb(0.42f, 0.62f, 0.48f), GroundSheenStrength = 0.55f,
                Scenery = new SceneryPalette
                {
                    Verge = new[]
                    {
                        "na_stump_oldTall",
                        "na_stump_round",
                        "na_log",
                        "na_grass_leafs",
                        "na_plant_bushSmall",
                        "na_mushroom_redGroup",
                        "gr_gravestone-debris",
                        "gr_debris",
                    },
                    Field = new[]
                    {
                        "na_tree_thin",
                        "na_tree_tall",
                        "na_tree_plateau",
                        "fa_fence-broken",
                        "su_tent",
                        "na_crop_carrot",
                        "fa_planks",
                    },
                    Landmarks = new[]
                    {
                        Landmarks.Watermill,
                        Landmarks.Cottage,
                        Landmarks.Ruin,
                    },
                    VergeDensity = 22f, FieldDensity = 13f,
                    LandmarkSpacing = 95f,
                    VergeTint = 0.78f, FieldTint = 0.34f, LandmarkTint = 0.14f
                },
                Props = new[] { PropKind.DeadTree, PropKind.HangingCage, PropKind.Stump, PropKind.Gravestone },
                PropDensity = 13f,
                PropStone = new Rgb(0.200f, 0.215f, 0.170f),
                Accent = new Rgb(0.45f, 0.95f, 0.40f)
            },

            // ---- 2 : cold, tight, and under something ----------------------
            new WorldTheme
            {
                DisplayName = "The Sunken Crypt",
                RoadStone = new Rgb(0.220f, 0.240f, 0.280f),
                RoadMortar = new Rgb(0.100f, 0.110f, 0.130f),
                RoadDamp = new Rgb(0.340f, 0.480f, 0.620f),
                RoadTiling = 2.10f, RoadWetness = 0.90f, RoadGloss = 14f, RoadStoneVariation = 0.38f,
                SkyZenith = new Rgb(0.014f, 0.020f, 0.038f),
                SkyHorizon = new Rgb(0.050f, 0.080f, 0.115f),
                SkyGlow = new Rgb(0.160f, 0.360f, 0.440f),
                SkyStars = 0.15f, SkyGlowPower = 5f, SkyGlowYaw = -22.0f,
                Fog = new Rgb(0.162f, 0.332f, 0.360f), FogStart = 35f, FogEnd = 105f,
                LightColor = new Rgb(0.620f, 0.780f, 0.980f),
                LightIntensity = 0.85f, LightPitch = 24f, LightYaw = 285f,
                // Ground: flooded flagstone; the shallows are the patch colour, so the sheen sits in them.
                Ground = new Rgb(0.088f, 0.148f, 0.168f),
                GroundAlt = new Rgb(0.118f, 0.228f, 0.282f),
                GroundPatchScale = 0.07f, GroundSpeckle = 0.22f,
                GroundSheen = new Rgb(0.40f, 0.62f, 0.80f), GroundSheenStrength = 0.78f,
                Scenery = new SceneryPalette
                {
                    Verge = new[]
                    {
                        "gr_gravestone-bevel",
                        "gr_gravestone-decorative",
                        "gr_grave-border",
                        "gr_candle",
                        "gr_lantern-candle",
                        "na_stone_smallA",
                        "na_rock_smallC",
                        "gr_rocks",
                    },
                    Field = new[]
                    {
                        "gr_pillar-large",
                        "gr_pillar-square",
                        "gr_crypt-small",
                        "gr_brick-wall-curve",
                        "gr_coffin",
                        "na_statue_column",
                        "gr_iron-fence-damaged",
                    },
                    Landmarks = new[]
                    {
                        Landmarks.Mausoleum,
                        Landmarks.Ruin,
                        Landmarks.Outcrop,
                    },
                    VergeDensity = 17f, FieldDensity = 12f,
                    LandmarkSpacing = 88f,
                    VergeTint = 0.7f, FieldTint = 0.24f, LandmarkTint = 0.08f
                },
                Props = new[] { PropKind.BrokenColumn, PropKind.Obelisk, PropKind.BoneArch },
                PropDensity = 10f,
                PropStone = new Rgb(0.205f, 0.220f, 0.250f),
                Accent = new Rgb(0.35f, 0.85f, 1.00f)
            },

            // ---- 3 : basalt, ash, and something burning under it -----------
            new WorldTheme
            {
                DisplayName = "Ember Fields",
                RoadStone = new Rgb(0.200f, 0.170f, 0.160f),
                RoadMortar = new Rgb(0.220f, 0.090f, 0.050f),   // cracks, not mortar
                RoadDamp = new Rgb(0.550f, 0.250f, 0.120f),
                RoadTiling = 1.50f, RoadWetness = 0.20f, RoadGloss = 6f, RoadStoneVariation = 0.60f,
                SkyZenith = new Rgb(0.045f, 0.022f, 0.020f),
                SkyHorizon = new Rgb(0.140f, 0.070f, 0.048f),
                SkyGlow = new Rgb(0.950f, 0.420f, 0.160f),
                SkyStars = 0.30f, SkyGlowPower = 10f, SkyGlowHeight = 12f, SkyGlowYaw = 10.0f,
                Fog = new Rgb(0.805f, 0.364f, 0.200f), FogStart = 55f, FogEnd = 150f,
                LightColor = new Rgb(1.000f, 0.800f, 0.620f),
                LightIntensity = 1.25f, LightPitch = 38f, LightYaw = 200f,
                // Ground: scorched earth veined with cinder; the only ground whose patch colour is brighter than its light.
                Ground = new Rgb(0.150f, 0.092f, 0.062f),
                GroundAlt = new Rgb(0.330f, 0.128f, 0.040f),
                GroundPatchScale = 0.11f, GroundSpeckle = 0.40f,
                GroundSheen = new Rgb(1.10f, 0.42f, 0.16f), GroundSheenStrength = 0.30f,
                Scenery = new SceneryPalette
                {
                    Verge = new[]
                    {
                        "na_stump_old",
                        "na_rock_tallA",
                        "na_rock_smallB",
                        "gr_debris",
                        "na_log",
                        "gr_gravestone-broken",
                        "na_stone_tallA",
                    },
                    Field = new[]
                    {
                        "na_rock_largeB",
                        "na_rock_largeC",
                        "fa_fence-broken",
                        "na_log_large",
                        "su_barrel",
                        "fa_cart",
                        "na_tree_small",
                    },
                    Landmarks = new[]
                    {
                        Landmarks.SiegeCamp,
                        Landmarks.Ruin,
                        Landmarks.Windmill,
                    },
                    VergeDensity = 12f, FieldDensity = 8f,
                    LandmarkSpacing = 100f,
                    VergeTint = 0.66f, FieldTint = 0.22f, LandmarkTint = 0.06f
                },
                Props = new[] { PropKind.RockSpire, PropKind.Brazier, PropKind.RuinedWall },
                PropDensity = 8f,
                PropStone = new Rgb(0.175f, 0.150f, 0.145f),
                Accent = new Rgb(1.50f, 0.55f, 0.18f)
            },

            // ---- 4 : pale, wide open, and very old --------------------------
            new WorldTheme
            {
                DisplayName = "The Bone Wastes",
                RoadStone = new Rgb(0.420f, 0.400f, 0.350f),
                RoadMortar = new Rgb(0.200f, 0.190f, 0.160f),
                RoadDamp = new Rgb(0.350f, 0.330f, 0.300f),
                RoadTiling = 1.20f, RoadWetness = 0.15f, RoadGloss = 5f, RoadStoneVariation = 0.55f,
                SkyZenith = new Rgb(0.028f, 0.026f, 0.048f),
                SkyHorizon = new Rgb(0.115f, 0.100f, 0.095f),
                SkyGlow = new Rgb(0.420f, 0.360f, 0.280f),
                SkyStars = 2.40f, SkyGlowPower = 6f, SkyGlowYaw = -34.0f,
                Fog = new Rgb(0.409f, 0.352f, 0.300f), FogStart = 100f, FogEnd = 185f,
                LightColor = new Rgb(0.880f, 0.860f, 0.920f),
                LightIntensity = 1.15f, LightPitch = 40f, LightYaw = 265f,
                // Ground: pale sand and bone grit; the brightest ground before the snow, and almost dry.
                Ground = new Rgb(0.370f, 0.345f, 0.290f),
                GroundAlt = new Rgb(0.458f, 0.432f, 0.362f),
                GroundPatchScale = 0.05f, GroundSpeckle = 0.18f,
                GroundSheen = new Rgb(0.55f, 0.52f, 0.44f), GroundSheenStrength = 0.05f,
                Scenery = new SceneryPalette
                {
                    Verge = new[]
                    {
                        "gr_gravestone-cross",
                        "gr_cross",
                        "gr_gravestone-round",
                        "na_rock_tallB",
                        "na_stone_tallA",
                        "gr_grave",
                        "na_rock_smallA",
                    },
                    Field = new[]
                    {
                        "na_statue_obelisk",
                        "na_statue_ring",
                        "gr_pillar-large",
                        "na_rock_largeA",
                        "gr_brick-wall-end",
                        "na_tree_thin",
                        "gr_crypt-small",
                    },
                    Landmarks = new[]
                    {
                        Landmarks.Outcrop,
                        Landmarks.Mausoleum,
                        Landmarks.Keep,
                    },
                    VergeDensity = 10f, FieldDensity = 7f,
                    LandmarkSpacing = 120f,
                    VergeTint = 0.62f, FieldTint = 0.2f, LandmarkTint = 0.05f
                },
                Props = new[] { PropKind.Gravestone, PropKind.BoneArch, PropKind.RockSpire },
                PropDensity = 7f,
                PropStone = new Rgb(0.455f, 0.435f, 0.385f),
                Accent = new Rgb(0.95f, 0.88f, 0.62f)
            },

            // ---- 5 : ice, and far too many stars ---------------------------
            new WorldTheme
            {
                DisplayName = "The Frozen Reach",
                RoadStone = new Rgb(0.360f, 0.420f, 0.480f),
                RoadMortar = new Rgb(0.200f, 0.240f, 0.300f),
                RoadDamp = new Rgb(0.550f, 0.700f, 0.880f),
                RoadTiling = 1.05f, RoadWetness = 0.95f, RoadGloss = 18f, RoadStoneVariation = 0.30f,
                SkyZenith = new Rgb(0.020f, 0.030f, 0.058f),
                SkyHorizon = new Rgb(0.095f, 0.125f, 0.165f),
                SkyGlow = new Rgb(0.300f, 0.580f, 0.720f),
                SkyStars = 2.80f, SkyGlowPower = 7f, SkyGlowHeight = 20f, SkyGlowYaw = 38.0f,
                Fog = new Rgb(0.305f, 0.531f, 0.620f), FogStart = 60f, FogEnd = 155f,
                LightColor = new Rgb(0.800f, 0.900f, 1.000f),
                LightIntensity = 1.30f, LightPitch = 30f, LightYaw = 300f,
                // Ground: snow, the brightest surface in the game; fine patches so drifts read as drifts.
                Ground = new Rgb(0.520f, 0.570f, 0.640f),
                GroundAlt = new Rgb(0.645f, 0.700f, 0.782f),
                GroundPatchScale = 0.14f, GroundSpeckle = 0.16f,
                GroundSheen = new Rgb(0.72f, 0.86f, 1.00f), GroundSheenStrength = 0.68f,
                Scenery = new SceneryPalette
                {
                    Verge = new[]
                    {
                        "na_rock_tallA",
                        "na_rock_smallC",
                        "na_stump_round",
                        "na_stone_smallA",
                        "gr_gravestone-bevel",
                        "na_rock_smallB",
                    },
                    Field = new[]
                    {
                        "na_tree_pineTallA",
                        "na_tree_pineTallB",
                        "na_tree_pineDefaultA",
                        "na_tree_pineSmallA",
                        "na_tree_pineRoundA",
                        "gr_iron-fence",
                        "na_rock_largeC",
                    },
                    Landmarks = new[]
                    {
                        Landmarks.PineStand,
                        Landmarks.Keep,
                        Landmarks.Cottage,
                    },
                    VergeDensity = 13f, FieldDensity = 15f,
                    LandmarkSpacing = 98f,
                    VergeTint = 0.6f, FieldTint = 0.18f, LandmarkTint = 0.05f
                },
                Props = new[] { PropKind.RockSpire, PropKind.Obelisk, PropKind.DeadTree },
                PropDensity = 8f,
                PropStone = new Rgb(0.390f, 0.450f, 0.510f),
                Accent = new Rgb(0.55f, 0.90f, 1.00f)
            },

            // ---- 6 : red, close, and wet ------------------------------------
            new WorldTheme
            {
                DisplayName = "The Blood Marsh",
                RoadStone = new Rgb(0.260f, 0.170f, 0.170f),
                RoadMortar = new Rgb(0.130f, 0.080f, 0.080f),
                RoadDamp = new Rgb(0.450f, 0.160f, 0.180f),
                RoadTiling = 1.40f, RoadWetness = 0.85f, RoadGloss = 9f, RoadStoneVariation = 0.50f,
                SkyZenith = new Rgb(0.038f, 0.014f, 0.020f),
                SkyHorizon = new Rgb(0.125f, 0.048f, 0.060f),
                SkyGlow = new Rgb(0.720f, 0.160f, 0.200f),
                SkyStars = 0.20f, SkyGlowPower = 9f, SkyGlowYaw = -12.0f,
                Fog = new Rgb(0.629f, 0.160f, 0.160f), FogStart = 40f, FogEnd = 115f,
                LightColor = new Rgb(0.950f, 0.680f, 0.680f),
                LightIntensity = 0.90f, LightPitch = 22f, LightYaw = 230f,
                // Ground: red silt under standing water.
                Ground = new Rgb(0.235f, 0.122f, 0.118f),
                GroundAlt = new Rgb(0.320f, 0.145f, 0.136f),
                GroundPatchScale = 0.06f, GroundSpeckle = 0.34f,
                GroundSheen = new Rgb(0.80f, 0.30f, 0.34f), GroundSheenStrength = 0.48f,
                Scenery = new SceneryPalette
                {
                    Verge = new[]
                    {
                        "na_stump_oldTall",
                        "gr_gravestone-debris",
                        "na_grass_large",
                        "na_plant_bushDetailed",
                        "gr_bench-damaged",
                        "na_log",
                        "gr_debris",
                    },
                    Field = new[]
                    {
                        "na_tree_oak",
                        "na_tree_plateau",
                        "gr_iron-fence-curve",
                        "su_fence",
                        "fa_stall",
                        "na_crop_melon",
                        "su_tent",
                    },
                    Landmarks = new[]
                    {
                        Landmarks.Watermill,
                        Landmarks.SiegeCamp,
                        Landmarks.Mausoleum,
                    },
                    VergeDensity = 20f, FieldDensity = 12f,
                    LandmarkSpacing = 92f,
                    VergeTint = 0.76f, FieldTint = 0.32f, LandmarkTint = 0.13f
                },
                Props = new[] { PropKind.Stump, PropKind.HangingCage, PropKind.Gravestone, PropKind.DeadTree },
                PropDensity = 12f,
                PropStone = new Rgb(0.225f, 0.150f, 0.150f),
                Accent = new Rgb(1.30f, 0.22f, 0.28f)
            },

            // ---- 7 : obsidian, gold and violet — the end of the road --------
            new WorldTheme
            {
                DisplayName = "The Throne of Dust",
                RoadStone = new Rgb(0.160f, 0.150f, 0.180f),
                RoadMortar = new Rgb(0.260f, 0.200f, 0.090f),   // gold in the seams
                RoadDamp = new Rgb(0.400f, 0.300f, 0.550f),
                RoadTiling = 1.80f, RoadWetness = 0.45f, RoadGloss = 12f, RoadStoneVariation = 0.35f,
                SkyZenith = new Rgb(0.018f, 0.014f, 0.036f),
                SkyHorizon = new Rgb(0.070f, 0.055f, 0.115f),
                SkyGlow = new Rgb(0.400f, 0.220f, 0.620f),
                SkyStars = 0.90f, SkyGlowPower = 9f, SkyGlowYaw = 30.0f,
                Fog = new Rgb(0.350f, 0.209f, 0.420f), FogStart = 65f, FogEnd = 165f,
                LightColor = new Rgb(0.820f, 0.760f, 1.000f),
                LightIntensity = 1.05f, LightPitch = 34f, LightYaw = 245f,
                // Ground: violet dust over old flags.
                Ground = new Rgb(0.172f, 0.148f, 0.220f),
                GroundAlt = new Rgb(0.232f, 0.196f, 0.302f),
                GroundPatchScale = 0.10f, GroundSpeckle = 0.28f,
                GroundSheen = new Rgb(0.52f, 0.38f, 0.70f), GroundSheenStrength = 0.12f,
                Scenery = new SceneryPalette
                {
                    Verge = new[]
                    {
                        "gr_gravestone-decorative",
                        "gr_bench",
                        "gr_lantern-candle",
                        "gr_grave-border",
                        "na_stone_tallA",
                        "gr_rocks",
                        "gr_candle",
                    },
                    Field = new[]
                    {
                        "gr_pillar-square",
                        "gr_pillar-large",
                        "na_statue_column",
                        "na_statue_ring",
                        "gr_brick-wall",
                        "fa_lantern",
                        "gr_crypt-small",
                    },
                    Landmarks = new[]
                    {
                        Landmarks.Keep,
                        Landmarks.Mausoleum,
                        Landmarks.Ruin,
                    },
                    VergeDensity = 16f, FieldDensity = 11f,
                    LandmarkSpacing = 82f,
                    VergeTint = 0.68f, FieldTint = 0.26f, LandmarkTint = 0.04f
                },
                Props = new[] { PropKind.Obelisk, PropKind.Brazier, PropKind.RuinedWall, PropKind.BoneArch },
                PropDensity = 9f,
                PropStone = new Rgb(0.150f, 0.140f, 0.170f),
                Accent = new Rgb(0.95f, 0.55f, 1.40f)
            }
        };

        public static IReadOnlyList<WorldTheme> All => Table;

        public static int Count => Table.Length;

        /// <summary>The world for a theme slot. Wraps, so a slot can never be out of range.</summary>
        public static WorldTheme At(int slot)
        {
            if (Table.Length == 0) return null;
            int i = slot % Table.Length;
            if (i < 0) i += Table.Length;
            return Table[i];
        }

        /// <summary>The world an act wears.</summary>
        public static WorldTheme For(Progression.RoundPlan plan) => At(plan.ThemeSlot);
    }
}
