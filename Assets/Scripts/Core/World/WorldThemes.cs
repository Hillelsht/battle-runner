using System;
using System.Collections.Generic;

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
                SkyStars = 1.6f,
                Fog = new Rgb(0.440f, 0.300f, 0.230f), FogStart = 70f, FogEnd = 170f,
                LightColor = new Rgb(0.750f, 0.780f, 0.950f),
                LightIntensity = 1.10f, LightPitch = 32f, LightYaw = 250f,
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
                SkyStars = 0.50f, SkyGlowPower = 6f,
                Fog = new Rgb(0.266f, 0.389f, 0.200f), FogStart = 45f, FogEnd = 120f,
                LightColor = new Rgb(0.700f, 0.820f, 0.720f),
                LightIntensity = 0.95f, LightPitch = 26f, LightYaw = 215f,
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
                SkyStars = 0.15f, SkyGlowPower = 5f,
                Fog = new Rgb(0.162f, 0.332f, 0.360f), FogStart = 35f, FogEnd = 105f,
                LightColor = new Rgb(0.620f, 0.780f, 0.980f),
                LightIntensity = 0.85f, LightPitch = 24f, LightYaw = 285f,
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
                SkyStars = 0.30f, SkyGlowPower = 10f, SkyGlowHeight = 12f,
                Fog = new Rgb(0.805f, 0.364f, 0.200f), FogStart = 55f, FogEnd = 150f,
                LightColor = new Rgb(1.000f, 0.800f, 0.620f),
                LightIntensity = 1.25f, LightPitch = 38f, LightYaw = 200f,
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
                SkyStars = 2.40f, SkyGlowPower = 6f,
                Fog = new Rgb(0.409f, 0.352f, 0.300f), FogStart = 100f, FogEnd = 185f,
                LightColor = new Rgb(0.880f, 0.860f, 0.920f),
                LightIntensity = 1.15f, LightPitch = 40f, LightYaw = 265f,
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
                SkyStars = 2.80f, SkyGlowPower = 7f, SkyGlowHeight = 20f,
                Fog = new Rgb(0.305f, 0.531f, 0.620f), FogStart = 60f, FogEnd = 155f,
                LightColor = new Rgb(0.800f, 0.900f, 1.000f),
                LightIntensity = 1.30f, LightPitch = 30f, LightYaw = 300f,
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
                SkyStars = 0.20f, SkyGlowPower = 9f,
                Fog = new Rgb(0.629f, 0.160f, 0.160f), FogStart = 40f, FogEnd = 115f,
                LightColor = new Rgb(0.950f, 0.680f, 0.680f),
                LightIntensity = 0.90f, LightPitch = 22f, LightYaw = 230f,
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
                SkyStars = 0.90f, SkyGlowPower = 9f,
                Fog = new Rgb(0.350f, 0.209f, 0.420f), FogStart = 65f, FogEnd = 165f,
                LightColor = new Rgb(0.820f, 0.760f, 1.000f),
                LightIntensity = 1.05f, LightPitch = 34f, LightYaw = 245f,
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
