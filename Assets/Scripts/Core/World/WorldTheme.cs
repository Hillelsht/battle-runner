using System;
using BattleRunner.Core.Art;

namespace BattleRunner.Core.World
{
    /// <summary>Roadside furniture a world can be dressed with. Meshes live in Gameplay.</summary>
    public enum PropKind
    {
        Gravestone = 0,
        DeadTree = 1,
        BrokenColumn = 2,
        Brazier = 3,
        Obelisk = 4,
        HangingCage = 5,
        BoneArch = 6,
        RockSpire = 7,
        RuinedWall = 8,
        Stump = 9
    }

    /// <summary>
    /// One world: the road under the army, the sky over it, the weather between, and what
    /// stands beside it.
    ///
    /// WHAT IS DELIBERATELY NOT HERE: the gate colours and the enemy tint. Blue means gain,
    /// red means loss, and a player reads those in the quarter-second before a gate arrives.
    /// Repainting them per world would trade a real gameplay signal for decoration, so the
    /// semantic palette stays global and this table only dresses the environment.
    ///
    /// Public fields with object initialisers rather than a thirty-argument constructor: this
    /// is a data table, and at this width a positional call is unreadable and a swapped pair
    /// of colours is invisible in review.
    /// </summary>
    public sealed class WorldTheme
    {
        public string DisplayName = "World";

        // --- the road ------------------------------------------------------
        public Rgb RoadStone;
        public Rgb RoadMortar;
        /// <summary>The wet sheen colour. Reads as weather more than the stone does.</summary>
        public Rgb RoadDamp;
        /// <summary>Cobbles per metre. Bigger stones read as older, coarser ground.</summary>
        public float RoadTiling = 1.6f;
        public float RoadMortarWidth = 0.075f;
        public float RoadStoneVariation = 0.45f;
        public float RoadWetness = 0.55f;
        public float RoadGloss = 8f;

        // --- the sky -------------------------------------------------------
        public Rgb SkyZenith;
        public Rgb SkyHorizon;
        /// <summary>The band of light on the horizon. The strongest single cue of "where am I".</summary>
        public Rgb SkyGlow;
        public float SkyZenithFalloff = 0.4f;
        public float SkyGroundFalloff = 0.35f;
        public float SkyGlowPower = 8f;
        public float SkyGlowHeight = 16f;
        public float SkyStars = 1.6f;
        /// <summary>
        /// Which way the ember band sits, in degrees off the road ahead. DarkSky's
        /// _GlowDirection was never written from a theme, so all eight worlds glowed straight
        /// down +Z at the same height — the single most recognisable feature of the sky was
        /// identical everywhere. Kept inside +/-40 so the glow stays something you run toward
        /// rather than something behind you.
        /// </summary>
        public float SkyGlowYaw;

        // --- the weather ---------------------------------------------------
        public Rgb Fog;
        public float FogStart = 70f;
        public float FogEnd = 170f;

        // --- the light -----------------------------------------------------
        public Rgb LightColor;
        public float LightIntensity = 1.1f;
        public float LightPitch = 32f;
        public float LightYaw = 250f;

        // --- the land ------------------------------------------------------
        // Before this there was no land. TrackController built ground to x = +/-4.158 and
        // nothing else existed laterally, so every prop from 5 m outward hovered over the
        // skybox. Snow, bog, cinder and bone sand differ from each other in a way eight
        // palettes over the same black void never could.
        public Rgb Ground;
        /// <summary>The patch colour clumped through the base one, at GroundPatchScale.</summary>
        public Rgb GroundAlt;
        /// <summary>Patches per metre. Smaller means larger, slower-changing ground.</summary>
        public float GroundPatchScale = 0.09f;
        public float GroundSpeckle = 0.30f;
        /// <summary>Standing water and ice glint; ash and bone do not.</summary>
        public Rgb GroundSheen;
        public float GroundSheenStrength = 0.15f;

        // --- what stands beside the road -----------------------------------
        /// <summary>The imported scenery: verge, field and landmarks. See SceneryPalette.</summary>
        public SceneryPalette Scenery = new SceneryPalette();

        /// <summary>
        /// The original ten procedural props. Kept, not replaced: they are the only meshes in
        /// the game authored FOR this game, they cost nothing, and mixing them through the
        /// verge stops an imported kit from looking like an imported kit.
        /// </summary>
        public PropKind[] Props = Array.Empty<PropKind>();
        /// <summary>Roughly how many props per 100 m of one verge.</summary>
        public float PropDensity = 9f;
        public Rgb PropStone;
        /// <summary>A world's signature colour: prop rim light and ambient bounce.</summary>
        public Rgb Accent;

        // --- derived, so a world stays coherent without authoring it twice --

        /// <summary>Below the horizon. Always a darker zenith — the sky does not invert.</summary>
        public Rgb SkyGround => SkyZenith.Scaled(0.6f);

        /// <summary>Rails pick up the road's stone, lifted so they read as separate.</summary>
        public Rgb RailBase => Rgb.Lerp(RoadStone, new Rgb(0.30f, 0.29f, 0.28f), 0.55f);
        public Rgb RailEmission => Rgb.Lerp(Accent, new Rgb(0.34f, 0.34f, 0.38f), 0.62f);

        /// <summary>Lane markings must stay brighter than the stone or the lanes stop reading.</summary>
        public Rgb MarkingBase => RoadStone.Scaled(0.55f);
        public Rgb MarkingEmission => Rgb.Lerp(RoadStone.Scaled(1.5f), Accent, 0.25f);

        public Rgb AmbientSky => SkyZenith.Scaled(5.5f);
        public Rgb AmbientEquator => Rgb.Lerp(SkyHorizon.Scaled(1.6f), Accent, 0.35f);
        public Rgb AmbientGround => RoadStone.Scaled(0.32f);

        /// <summary>
        /// What the fog colour WOULD be if it followed the sky exactly.
        ///
        /// docs/10-look.md records fog as `horizon + ~0.76 * glow`. Solving the shipped values
        /// per channel gives r = 0.710, g = 0.697, b = 0.457 — red and green fit one factor to
        /// within 0.005, blue does not, because the shipped fog is deliberately warmer than the
        /// rule. So fog is AUTHORED per world and this is the guard rail: FogDrift measures how
        /// far a world's red and green have wandered, and a test refuses a world that would
        /// draw a visible seam where the road meets the sky.
        /// </summary>
        public const float FogGlowFactor = 0.70f;

        public Rgb FogFromSky =>
            new Rgb(SkyHorizon.R + FogGlowFactor * SkyGlow.R,
                    SkyHorizon.G + FogGlowFactor * SkyGlow.G,
                    SkyHorizon.B + FogGlowFactor * SkyGlow.B);

        // --- the grade, derived, so a world stops fighting its own palette --
        //
        // The post-processing stack was FIXED across all eight worlds: bloom tinted
        // (1.0, 0.86, 0.72), colour filter (1.0, 0.96, 0.90), warm highlights over cool
        // shadows, saturation -4. Every bright pixel in the green world bloomed orange and
        // every world was pulled back toward the same warm grey — which is a large part of
        // why eight authored palettes read as one. These derive from what the world already
        // declares, so a new world cannot forget to grade itself.
        //
        // Checked against the shipped constants: world 0's derived bloom tint is
        // (1.000, 0.841, 0.709) against the authored (1.0, 0.86, 0.72), and its filter is
        // (1.000, 0.936, 0.884) against (1.0, 0.96, 0.90). The Ashen Road keeps the look it
        // shipped with; the other seven stop borrowing it.

        /// <summary>Bloom carries the world's colour, held mostly neutral so it tints rather than dyes.</summary>
        public Rgb BloomTint => Accent.Normalized.TowardWhite(0.55f);

        /// <summary>A whisper of the accent over the whole frame.</summary>
        public Rgb GradeFilter => Accent.Normalized.TowardWhite(0.82f);

        /// <summary>
        /// Saturation, in ColorAdjustments units. The shipped -4 was actively removing the
        /// colour the worlds are made of; a chromatic world now earns more of it back than an
        /// ashen one. This is the single value most likely to want a device pass.
        /// </summary>
        public float GradeSaturation => 2f + 12f * Accent.Chroma;

        /// <summary>Corners tinted toward the world's own night rather than a fixed violet.</summary>
        public Rgb VignetteColor => SkyZenith.Normalized.Scaled(0.06f);

        /// <summary>Shadows take the sky, highlights take the ember band. Mean-normalised so tinting never changes exposure.</summary>
        public Rgb GradeShadows => SkyZenith.Normalized.TowardWhite(0.72f).MeanNormalized;
        public Rgb GradeHighlights => SkyGlow.Normalized.TowardWhite(0.70f).MeanNormalized;

        /// <summary>Largest red/green departure from the sky-derived fog. Blue is free.</summary>
        public float FogDrift
        {
            get
            {
                Rgb ideal = FogFromSky;
                return Math.Max(Math.Abs(Fog.R - ideal.R), Math.Abs(Fog.G - ideal.G));
            }
        }
    }
}
