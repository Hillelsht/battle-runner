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
                RoadTiling = 1.60f, RoadWetness = 0.55f, RoadGloss = 8f, RoadStoneVariation = 0.72f, RoadGrimeContrast = 0.52f,
                RoadMortarWidth = 0.075f, Surface = RoadSurfaces.Cobble,
                SkyZenith = new Rgb(0.022f, 0.020f, 0.055f),
                SkyHorizon = new Rgb(0.085f, 0.070f, 0.125f),
                SkyGlow = new Rgb(0.500f, 0.330f, 0.230f),
                // The judged baseline: 0.40 / 0.35, left alone.
                SkyStars = 1.6f, SkyGlowYaw = 0.0f,
                Fog = new Rgb(0.440f, 0.300f, 0.230f), FogStart = 70f, FogEnd = 170f,
                LightColor = new Rgb(0.750f, 0.780f, 0.950f),
                LightIntensity = 1.10f, LightPitch = 32f, LightYaw = 250f,
                // Ground: ash over dead grass; the baseline, and the only world whose ground is meant to be forgettable.
                Ground = new Rgb(0.228f, 0.200f, 0.152f),
                GroundAlt = new Rgb(0.296f, 0.260f, 0.188f),
                GroundPatchScale = 0.09f, GroundSpeckle = 0.32f,
                GroundSurface = RoadSurfaces.Dirt, GroundSurfaceTiling = 0.30f,
                GroundGloss = 10f, GradeTemperature = -4f, GradeTint = 0f,
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
                        Landmarks.Chapel,
                    },
                    VergeDensity = 20f, FieldDensity = 9f,
                    LandmarkSpacing = 105f,
                    VergeTint = 0.32f, FieldTint = 0.2f, LandmarkTint = 0.12f,
                    // The judged baseline. Left on the defaults on purpose: this is the one palette
                    // that has been looked at on a real screen, and it is what the other seven are
                    // measured away from.
                    // The anchor world, and the style the other four are named against: a pointed span
                    // with crosses on it, which is the one thing over the road that says graveyard
                    // from four hundred metres away.
                    Arch = ArchStyle.Gothic, ArchSpacing = 128f,
                    VergeScale = 2.00f, FieldScale = 2.80f, LandmarkScale = 3.40f,
                },
                Props = new[] { PropKind.Gravestone, PropKind.DeadTree, PropKind.BrokenColumn },
                PropDensity = 4f,
                PropStone = new Rgb(0.280f, 0.268f, 0.255f),
                Accent = new Rgb(0.85f, 0.55f, 0.30f)
            },

            // ---- 1 : THE ONE IN DAYLIGHT ------------------------------------
            //
            // "one can be diablo style with crosses, another can be a fairy tale style with
            // castles and flying pony, etc. Like every world is very different from the
            // previous."
            //
            // This slot held Gallows Mire — a green swamp, which world 6 (The Blood Marsh)
            // already is and does better. It is now the counterweight to the Ashen Road, and
            // the counterweight is not a colour, it is the TIME OF DAY.
            //
            // MEASURED ACROSS ALL EIGHT WORLDS BEFORE THIS: every zenith sat between 0.016
            // and 0.030 luminance — a span of 0.014 across the entire game. The sky is the
            // largest single area in a runner's frame and it was black in every world, which
            // is most of why eight settings read as one. The palettes underneath were never
            // the problem: measured frame brightness already ran 0.090 (Ember Fields) to
            // 0.318 (Frozen Reach), a 3.5x spread nobody could see past the identical sky.
            //
            // Thistlewood's zenith is 0.44 — roughly EIGHTEEN TIMES any of the other seven.
            // Nothing else in this table is that far from its neighbours on any axis.
            new WorldTheme
            {
                DisplayName = "Thistlewood",
                // Warm dry flagstone, barely wet. Every other world's road is damp, which is
                // a night-and-rain cue the eye reads before it reads hue.
                RoadStone = new Rgb(0.620f, 0.575f, 0.495f),
                RoadMortar = new Rgb(0.430f, 0.400f, 0.350f),
                RoadDamp = new Rgb(0.640f, 0.620f, 0.560f),
                RoadTiling = 1.45f, RoadWetness = 0.10f, RoadGloss = 22f, RoadStoneVariation = 0.55f, RoadGrimeContrast = 0.26f,
                // PLANKS, and not because a boardwalk is storybook: it is the only surface of the
                // eight not already carrying another world, and two worlds paved the same way
                // are two worlds the player walks down identically however they are coloured.
                RoadMortarWidth = 0.095f, Surface = RoadSurfaces.Planks,
                // A real blue overhead and a pale warm band on the horizon.
                SkyZenith = new Rgb(0.230f, 0.430f, 0.780f),
                SkyHorizon = new Rgb(0.620f, 0.680f, 0.780f),
                SkyGlow = new Rgb(0.260f, 0.200f, 0.105f),
                // A WIDE SOFT VAULT. pow(height, 1.70) keeps the horizon colour most of the
                // way up, so the pastel gradient takes the whole frame instead of snapping to a
                // zenith twenty degrees off the road. Storybook skies are big.
                SkyZenithFalloff = 1.70f, SkyGroundFalloff = 0.55f,
                // No stars, obviously — and a WIDE, LOW glow rather than a tight ember band:
                // power 3 spreads the warm light right along the horizon like late afternoon
                // instead of pointing at one spot the way a burning city does.
                SkyStars = 0.0f, SkyGlowPower = 3f, SkyGlowHeight = 7f, SkyGlowYaw = -14.0f,
                // Haze, not fog, and it reaches almost to MaxFogEnd: seeing a castle on the
                // horizon is half of what makes this world storybook rather than pretty.
                // Fog is horizon + 0.70 x glow on red and green, to the fourth decimal. That
                // relationship is what stops a seam where the road meets the sky, and a daylight
                // world is where it would show worst — there is no darkness to hide it in.
                Fog = new Rgb(0.802f, 0.820f, 0.884f), FogStart = 95f, FogEnd = 182f,
                LightColor = new Rgb(1.000f, 0.965f, 0.880f),
                LightIntensity = 1.55f, LightPitch = 52f, LightYaw = 205f,
                // Ground: meadow. The brightest ground in the game by a wide margin, and the
                // only one that is a growing thing rather than what is left of one.
                Ground = new Rgb(0.330f, 0.470f, 0.205f),
                GroundAlt = new Rgb(0.455f, 0.590f, 0.270f),
                GroundPatchScale = 0.11f, GroundSpeckle = 0.20f,
                GroundSurface = RoadSurfaces.Dirt, GroundSurfaceTiling = 0.34f,
                GroundGloss = 6f, GradeTemperature = 9f, GradeTint = 5f,
                GroundSheen = new Rgb(0.70f, 0.80f, 0.55f), GroundSheenStrength = 0.08f,
                Scenery = new SceneryPalette
                {
                    // Round, fat and alive. Not one broken, burnt or dead piece in the list,
                    // which is the single hardest rule to keep in a pack built for a graveyard.
                    Verge = new[]
                    {
                        "na_grass_large",
                        "na_grass",
                        "na_grass_leafs",
                        "na_plant_bushDetailed",
                        "na_plant_bushSmall",
                        "na_mushroom_redGroup",
                        "na_stone_smallA",
                        "gr_bench",
                    },
                    Field = new[]
                    {
                        "na_tree_oak",
                        "na_tree_default",
                        "na_tree_pineRoundA",
                        "na_tree_small",
                        "fa_stall",
                        "fa_cart",
                        "fa_fence-gate",
                        "fa_lantern",
                        "na_crop_melon",
                        "na_crops_bambooStageA",
                    },
                    Landmarks = new[]
                    {
                        Landmarks.MarketGreen,
                        Landmarks.Keep,
                        Landmarks.Cottage,
                        Landmarks.Windmill,
                    },
                    // Villages close together — a storybook road always has somewhere in sight.
                    VergeDensity = 24f, FieldDensity = 16f,
                    LandmarkSpacing = 82f,
                    // NEARLY UNTINTED, and that is the other half of this world. Kenney's models
                    // ship with cheerful baked vertex colour and every world so far has dragged
                    // it 26-34% toward its own grim stone, because a dark-fantasy road cannot
                    // afford cheerful. This one is not a dark-fantasy road. Letting the pack be
                    // the colour it already is costs nothing and no other world can do it.
                    VergeTint = 0.08f, FieldTint = 0.03f, LandmarkTint = 0.01f,
                    // EVERYTHING BIGGER AND ROUNDER. The size language is half of what makes a world a
                    // place: a storybook is drawn with fat trees and tall pointed roofs, and the
                    // same eight pieces at 2.0 read as the same eight pieces.
                    // 4.35 and not the 4.70 first authored: the keep's tallest part is 7.67
                    // units, so 4.70 stands it 36 m and it blots out the sky. This is the
                    // largest landmark scale in the game that still leaves a horizon.
                    // A gateway you go THROUGH rather than under, and close together, because a
                    // storybook road is always arriving somewhere.
                    Arch = ArchStyle.Timber, ArchSpacing = 112f,
                    // And the pony, which was asked for by name. No Kenney kit in this pack
                    // has a creature or a character mesh of any kind — 119 pieces across five
                    // kits, checked — so it is the one piece of scenery in the game that could
                    // only ever have been built rather than fetched.
                    SkyRider = SkyRiderKind.Pegasus,
                    VergeScale = 2.55f, FieldScale = 3.55f, LandmarkScale = 4.35f,
                },
                // Three kinds, all of them read as HEDGEROW rather than ruin: a coppiced
                // stump, a dry-stone field wall, and a lit post at the roadside. The same
                // three meshes every other world uses for a graveyard, asked to be a village.
                Props = new[] { PropKind.Stump, PropKind.RuinedWall, PropKind.Brazier },
                PropDensity = 4f,
                PropStone = new Rgb(0.560f, 0.520f, 0.430f),
                Accent = new Rgb(1.00f, 0.52f, 0.86f)
            },

            // ---- 2 : cold, tight, and under something ----------------------
            new WorldTheme
            {
                DisplayName = "The Sunken Crypt",
                RoadStone = new Rgb(0.220f, 0.240f, 0.280f),
                RoadMortar = new Rgb(0.100f, 0.110f, 0.130f),
                RoadDamp = new Rgb(0.340f, 0.480f, 0.620f),
                RoadTiling = 1.90f, RoadWetness = 0.90f, RoadGloss = 14f, RoadStoneVariation = 0.76f, RoadGrimeContrast = 0.50f,
                RoadMortarWidth = 0.040f, Surface = RoadSurfaces.Flagstone,
                SkyZenith = new Rgb(0.014f, 0.020f, 0.038f),
                SkyHorizon = new Rgb(0.050f, 0.080f, 0.115f),
                SkyGlow = new Rgb(0.160f, 0.360f, 0.440f),
                // A LID, NOT A SKY. At 0.16 the zenith colour arrives almost immediately above
                // the horizon, which is the difference between being outdoors and being under
                // several metres of stone.
                SkyZenithFalloff = 0.16f, SkyGroundFalloff = 0.20f,
                SkyStars = 0.15f, SkyGlowPower = 5f, SkyGlowYaw = -22.0f,
                Fog = new Rgb(0.162f, 0.332f, 0.360f), FogStart = 35f, FogEnd = 105f,
                LightColor = new Rgb(0.620f, 0.780f, 0.980f),
                LightIntensity = 0.85f, LightPitch = 24f, LightYaw = 285f,
                // Ground: flooded flagstone; the shallows are the patch colour, so the sheen sits in them.
                Ground = new Rgb(0.088f, 0.148f, 0.168f),
                GroundAlt = new Rgb(0.118f, 0.228f, 0.282f),
                GroundPatchScale = 0.07f, GroundSpeckle = 0.22f,
                GroundSurface = RoadSurfaces.Gravel, GroundSurfaceTiling = 0.42f,
                GroundGloss = 22f, GradeTemperature = -14f, GradeTint = -3f,
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
                        Landmarks.ColumnHall,
                        Landmarks.Outcrop,
                    },
                    VergeDensity = 22f, FieldDensity = 12f,
                    LandmarkSpacing = 88f,
                    VergeTint = 0.3f, FieldTint = 0.17f, LandmarkTint = 0.08f,
                    // SMALL AND TIGHT, because a crypt is a place you are too big for. The landmark
                    // scale is the lowest in the game — a structure underground cannot tower —
                    // and 2.80 rather than the 2.60 first authored because a world's scale is
                    // bounded by its OWN shortest landmark: the column hall is 2.19 units, so
                    // below 2.75 it stops being a landmark and becomes a large prop.
                    // The same gothic span as the Ashen Road and nearly TWICE as often, which is the
                    // whole difference: underground the arches are the ceiling, so they come at you.
                    Arch = ArchStyle.Gothic, ArchSpacing = 74f,
                    VergeScale = 1.50f, FieldScale = 2.05f, LandmarkScale = 2.80f,
                },
                Props = new[] { PropKind.BrokenColumn, PropKind.Obelisk, PropKind.BoneArch },
                PropDensity = 5f,
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
                RoadTiling = 1.30f, RoadWetness = 0.20f, RoadGloss = 6f, RoadStoneVariation = 0.85f, RoadGrimeContrast = 0.56f,
                RoadMortarWidth = 0.130f, Surface = RoadSurfaces.Dirt,
                SkyZenith = new Rgb(0.045f, 0.022f, 0.020f),
                SkyHorizon = new Rgb(0.140f, 0.070f, 0.048f),
                SkyGlow = new Rgb(0.950f, 0.420f, 0.160f),
                // Smoke banking up: a little slower than the baseline so the ember band keeps its
                // height and the column above it stays dirty rather than clearing to black.
                SkyZenithFalloff = 0.55f, SkyGroundFalloff = 0.30f,
                SkyStars = 0.30f, SkyGlowPower = 10f, SkyGlowHeight = 12f, SkyGlowYaw = 10.0f,
                Fog = new Rgb(0.805f, 0.364f, 0.200f), FogStart = 55f, FogEnd = 150f,
                LightColor = new Rgb(1.000f, 0.800f, 0.620f),
                LightIntensity = 1.25f, LightPitch = 38f, LightYaw = 200f,
                // Ground: scorched earth veined with cinder; the only ground whose patch colour is brighter than its light.
                Ground = new Rgb(0.150f, 0.092f, 0.062f),
                GroundAlt = new Rgb(0.330f, 0.128f, 0.040f),
                GroundPatchScale = 0.11f, GroundSpeckle = 0.40f,
                GroundSurface = RoadSurfaces.Gravel, GroundSurfaceTiling = 0.34f,
                GroundGloss = 5f, GradeTemperature = 18f, GradeTint = 4f,
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
                        Landmarks.Smithy,
                        Landmarks.Windmill,
                    },
                    VergeDensity = 16f, FieldDensity = 8f,
                    LandmarkSpacing = 100f,
                    VergeTint = 0.28f, FieldTint = 0.15f, LandmarkTint = 0.06f,
                    // Jagged and a size up: the field is basalt and burnt trunks, and they have to
                    // stand over the verge rather than beside it.
                    // Fallen spans, spread thin. A volcanic field is somewhere that USED to have roads.
                    Arch = ArchStyle.Broken, ArchSpacing = 150f,
                    VergeScale = 2.20f, FieldScale = 3.15f, LandmarkScale = 4.00f,
                },
                Props = new[] { PropKind.RockSpire, PropKind.Brazier, PropKind.RuinedWall },
                PropDensity = 4f,
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
                RoadTiling = 3.20f, RoadWetness = 0.15f, RoadGloss = 5f, RoadStoneVariation = 0.82f, RoadGrimeContrast = 0.50f,
                RoadMortarWidth = 0.020f, Surface = RoadSurfaces.Sand,
                SkyZenith = new Rgb(0.028f, 0.026f, 0.048f),
                SkyHorizon = new Rgb(0.115f, 0.100f, 0.095f),
                SkyGlow = new Rgb(0.420f, 0.360f, 0.280f),
                // THE BIGGEST SKY IN THE GAME, and the one number that says 'desert': at 2.10 the
                // gradient never quite arrives, so the sky reads as going on rather than as
                // closing over. Paired with the widest scale spread of any world.
                SkyZenithFalloff = 2.10f, SkyGroundFalloff = 0.70f,
                SkyStars = 2.40f, SkyGlowPower = 6f, SkyGlowYaw = -34.0f,
                Fog = new Rgb(0.409f, 0.352f, 0.300f), FogStart = 100f, FogEnd = 185f,
                LightColor = new Rgb(0.880f, 0.860f, 0.920f),
                LightIntensity = 1.15f, LightPitch = 40f, LightYaw = 265f,
                // Ground: pale sand and bone grit; the brightest ground before the snow, and almost dry.
                Ground = new Rgb(0.370f, 0.345f, 0.290f),
                GroundAlt = new Rgb(0.458f, 0.432f, 0.362f),
                GroundPatchScale = 0.05f, GroundSpeckle = 0.18f,
                GroundSurface = RoadSurfaces.Dirt, GroundSurfaceTiling = 0.26f,
                GroundGloss = 4f, GradeTemperature = 7f, GradeTint = -2f,
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
                        Landmarks.BoneShrine,
                        // The keep used to stand here and it was the thing CAPPING this world:
                        // a castle is 7.66 units, so any world holding one cannot scale past
                        // 4.44 without filling the sky — and "enormous" is the whole point of a
                        // bone desert. A watchtower is 4.92, which buys back a third of the range
                        // and is a better fit for somewhere nobody garrisons.
                        Landmarks.Watchtower,
                    },
                    VergeDensity = 13f, FieldDensity = 7f,
                    LandmarkSpacing = 120f,
                    VergeTint = 0.27f, FieldTint = 0.14f, LandmarkTint = 0.05f,
                    // THE WIDEST SPREAD OF ANY WORLD, and that IS the world: a bleached desert is small
                    // debris under enormous bone arches, with nothing in between. A uniform scale
                    // here would read as rubble. 6.20 is the largest landmark scale in the game
                    // and it only fits because the keep was taken out of this world's set.
                    // Ribcage arches, which is what this world has always been described as and had no
                    // way to draw: the three bands are all to the SIDE of the road, and a ribcage
                    // is over it.
                    Arch = ArchStyle.Rib, ArchSpacing = 96f,
                    VergeScale = 1.65f, FieldScale = 4.20f, LandmarkScale = 6.20f,
                },
                Props = new[] { PropKind.Gravestone, PropKind.BoneArch, PropKind.RockSpire },
                PropDensity = 3f,
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
                RoadTiling = 1.35f, RoadWetness = 0.95f, RoadGloss = 18f, RoadStoneVariation = 0.62f, RoadGrimeContrast = 0.38f,
                RoadMortarWidth = 0.055f, Surface = RoadSurfaces.Snow,
                SkyZenith = new Rgb(0.020f, 0.030f, 0.058f),
                SkyHorizon = new Rgb(0.095f, 0.125f, 0.165f),
                SkyGlow = new Rgb(0.300f, 0.580f, 0.720f),
                // A dark zenith pulled down close over a pale horizon, which is what gives the
                // aurora band something to sit against instead of glowing into more of itself.
                SkyZenithFalloff = 0.30f, SkyGroundFalloff = 0.45f,
                SkyStars = 2.80f, SkyGlowPower = 7f, SkyGlowHeight = 20f, SkyGlowYaw = 38.0f,
                Fog = new Rgb(0.305f, 0.531f, 0.620f), FogStart = 60f, FogEnd = 155f,
                LightColor = new Rgb(0.800f, 0.900f, 1.000f),
                LightIntensity = 1.30f, LightPitch = 30f, LightYaw = 300f,
                // Ground: snow, the brightest surface in the game; fine patches so drifts read as drifts.
                Ground = new Rgb(0.520f, 0.570f, 0.640f),
                GroundAlt = new Rgb(0.645f, 0.700f, 0.782f),
                GroundPatchScale = 0.14f, GroundSpeckle = 0.16f,
                GroundSurface = RoadSurfaces.Gravel, GroundSurfaceTiling = 0.20f,
                GroundGloss = 28f, GradeTemperature = -22f, GradeTint = 2f,
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
                        Landmarks.Watchtower,
                        Landmarks.Cottage,
                    },
                    VergeDensity = 17f, FieldDensity = 15f,
                    LandmarkSpacing = 98f,
                    VergeTint = 0.26f, FieldTint = 0.13f, LandmarkTint = 0.05f,
                    // Tall and thin. Ice is vertical, so the field is pushed up while the verge stays
                    // low — the opposite proportion to the marsh.
                    // A flat slab of ice far overhead on slender columns — the highest span in the game,
                    // so it reads as something that could come down.
                    Arch = ArchStyle.Frozen, ArchSpacing = 138f,
                    VergeScale = 1.80f, FieldScale = 3.45f, LandmarkScale = 4.40f,
                },
                Props = new[] { PropKind.RockSpire, PropKind.Obelisk, PropKind.DeadTree },
                PropDensity = 4f,
                PropStone = new Rgb(0.390f, 0.450f, 0.510f),
                Accent = new Rgb(0.55f, 0.90f, 1.00f)
            },

            // ---- 6 : red, close, and wet ------------------------------------
            new WorldTheme
            {
                DisplayName = "The Blood Marsh",
                // Raised 45%. This was the darkest road of the eight at 0.189 luma, and it
                // is the one world whose contrast no amount of structure could fix: with the
                // stone that dark the grime, the joint and the tone all land inside a few
                // per cent of black and the predicted range came out at 17 against a mean of
                // 55. Same argument as MinVergeLuma, applied to the road: a surface that dark
                // in a fogged night scene is a hole, not an object. Still the darkest road.
                RoadStone = new Rgb(0.377f, 0.247f, 0.247f),
                RoadMortar = new Rgb(0.176f, 0.108f, 0.108f),
                RoadDamp = new Rgb(0.450f, 0.160f, 0.180f),
                RoadTiling = 3.30f, RoadWetness = 0.85f, RoadGloss = 9f, RoadStoneVariation = 0.78f, RoadGrimeContrast = 0.60f,
                RoadMortarWidth = 0.060f, Surface = RoadSurfaces.Gravel,
                SkyZenith = new Rgb(0.038f, 0.014f, 0.020f),
                SkyHorizon = new Rgb(0.125f, 0.048f, 0.060f),
                SkyGlow = new Rgb(0.720f, 0.160f, 0.200f),
                // Cloud at head height. Nearly as closed as the crypt and for the same reason —
                // the marsh is a place with no horizon.
                SkyZenithFalloff = 0.22f, SkyGroundFalloff = 0.25f,
                SkyStars = 0.20f, SkyGlowPower = 9f, SkyGlowYaw = -12.0f,
                Fog = new Rgb(0.629f, 0.160f, 0.160f), FogStart = 40f, FogEnd = 115f,
                LightColor = new Rgb(0.950f, 0.680f, 0.680f),
                LightIntensity = 1.05f, LightPitch = 22f, LightYaw = 230f,
                // Ground: red silt under standing water.
                Ground = new Rgb(0.235f, 0.122f, 0.118f),
                GroundAlt = new Rgb(0.320f, 0.145f, 0.136f),
                GroundPatchScale = 0.06f, GroundSpeckle = 0.34f,
                GroundSurface = RoadSurfaces.Dirt, GroundSurfaceTiling = 0.32f,
                GroundGloss = 14f, GradeTemperature = 6f, GradeTint = 9f,
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
                        Landmarks.Gatehouse,
                        Landmarks.SiegeCamp,
                        Landmarks.Mausoleum,
                    },
                    VergeDensity = 26f, FieldDensity = 12f,
                    LandmarkSpacing = 92f,
                    VergeTint = 0.33f, FieldTint = 0.21f, LandmarkTint = 0.13f,
                    // LOW AND DENSE, with the three bands almost the same size, which is the point: a
                    // swamp has no horizon and no hierarchy, just more of it in every direction.
                    // The floor here is the gatehouse at 1.70 units, the shortest landmark in the
                    // game, which is why this cannot go under 3.60.
                    // Ribs again, but the sparsest in the game: the marsh has no horizon, and anything
                    // regular overhead would give it one and undo the world.
                    Arch = ArchStyle.Rib, ArchSpacing = 160f,
                    VergeScale = 2.45f, FieldScale = 2.60f, LandmarkScale = 3.65f,
                },
                Props = new[] { PropKind.Stump, PropKind.HangingCage, PropKind.Gravestone, PropKind.DeadTree },
                PropDensity = 5f,
                PropStone = new Rgb(0.225f, 0.150f, 0.150f),
                Accent = new Rgb(1.30f, 0.22f, 0.28f)
            },

            // ---- 7 : obsidian, gold and violet — the end of the road --------
            new WorldTheme
            {
                DisplayName = "The Throne of Dust",
                RoadStone = new Rgb(0.215f, 0.205f, 0.235f),
                RoadMortar = new Rgb(0.420f, 0.320f, 0.130f),   // gold in the seams, and bright
                // enough to be gold: at 0.26/0.20/0.09 the gilding landed within 2%
                // of the tesserae's own luminance and the pattern read as nothing.
                RoadDamp = new Rgb(0.400f, 0.300f, 0.550f),
                RoadTiling = 2.60f, RoadWetness = 0.45f, RoadGloss = 12f, RoadStoneVariation = 0.70f, RoadGrimeContrast = 0.42f,
                RoadMortarWidth = 0.160f, Surface = RoadSurfaces.Mosaic,
                SkyZenith = new Rgb(0.018f, 0.014f, 0.036f),
                SkyHorizon = new Rgb(0.070f, 0.055f, 0.115f),
                SkyGlow = new Rgb(0.400f, 0.220f, 0.620f),
                // A tall imperial sky, opening out above a ruin. The only world that is both wide
                // above and monumental below; everything else picks one.
                SkyZenithFalloff = 0.90f, SkyGroundFalloff = 0.40f,
                SkyStars = 0.90f, SkyGlowPower = 9f, SkyGlowYaw = 30.0f,
                Fog = new Rgb(0.350f, 0.209f, 0.420f), FogStart = 65f, FogEnd = 165f,
                LightColor = new Rgb(0.820f, 0.760f, 1.000f),
                LightIntensity = 1.05f, LightPitch = 34f, LightYaw = 245f,
                // Ground: violet dust over old flags.
                Ground = new Rgb(0.172f, 0.148f, 0.220f),
                GroundAlt = new Rgb(0.232f, 0.196f, 0.302f),
                GroundPatchScale = 0.10f, GroundSpeckle = 0.28f,
                GroundSurface = RoadSurfaces.Flagstone, GroundSurfaceTiling = 0.38f,
                GroundGloss = 9f, GradeTemperature = -6f, GradeTint = 7f,
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
                        Landmarks.Manor,
                        Landmarks.Ruin,
                    },
                    VergeDensity = 21f, FieldDensity = 11f,
                    LandmarkSpacing = 82f,
                    VergeTint = 0.29f, FieldTint = 0.16f, LandmarkTint = 0.04f,
                    // MONUMENTAL, and 4.40 is as monumental as a world holding a KEEP can be: at
                    // 4.44 the castle stands 34 m and fills the sky from thirty metres away. That
                    // ceiling is the honest limit of expressing size through this one number, and
                    // it is why the Bone Wastes gave its castle up to go bigger.
                    // Broken spans, close together. An imperial ruin is a place where the ARCHITECTURE is
                    // what is left standing, so this is the one world where the arches are the point.
                    Arch = ArchStyle.Broken, ArchSpacing = 104f,
                    VergeScale = 2.05f, FieldScale = 3.05f, LandmarkScale = 4.40f,
                },
                Props = new[] { PropKind.Obelisk, PropKind.Brazier, PropKind.RuinedWall, PropKind.BoneArch },
                PropDensity = 4f,
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
