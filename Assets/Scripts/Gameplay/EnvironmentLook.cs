using BattleRunner.Core.Progression;
using BattleRunner.Core.World;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BattleRunner.Gameplay
{
    /// <summary>
    /// Everything that decides how the game LOOKS rather than how it plays: sky, fog,
    /// ambient, the key light, and the post-processing stack.
    ///
    /// It is built in code for the same reason the rest of the project is — a hand-written
    /// VolumeProfile is another serialized asset to keep valid, and this one changes every
    /// time the art direction moves. A profile created at runtime cannot drift from the
    /// scene, cannot be half-migrated by a Unity upgrade, and shows the whole look as a
    /// readable list of numbers.
    ///
    /// The stack is the fix for the greybox complaint. HDR plus bloom is what turns an
    /// emissive value of 1.4 from "flat bright paint" into a light source; tonemapping is
    /// what stops the result looking washed; and cool shadows against warm highlights is
    /// the entire Diablo palette in one component.
    /// </summary>
    public static class EnvironmentLook
    {
        private static VolumeProfile _profile;

        // Handles kept so a world can be applied per round. None of these existed before:
        // the sky was used straight off the Resources asset, and the key light was created
        // and its reference dropped on the floor.
        private static Material _sky;
        private static Light _keyLight;

        // The grade components, kept for the same reason the sky and the light are. The
        // stack was built once and its handles dropped, so all eight worlds were graded
        // identically: every bright pixel in the green world bloomed warm orange, and a
        // fixed warm colour filter and saturation of -4 pulled all of them back toward the
        // same ash. Eight authored palettes cannot survive one shared grade on top.
        private static Bloom _bloom;
        private static ColorAdjustments _color;
        private static ShadowsMidtonesHighlights _grade;
        private static Vignette _vignette;
        private static WhiteBalance _whiteBalance;

        public static void Apply()
        {
            ApplySky();
            ApplyAtmosphere();
            ApplyKeyLight();
        }

        private static void ApplySky()
        {
            // Resources, not Shader.Find: a shader referenced only by name is stripped from
            // the Android build and renders magenta, which is exactly how v0.1.0 shipped.
            //
            // isSupported alone is not the test. It reports whether a shader COMPILED, not
            // whether any SubShader matches the active pipeline — a URP-tagged shader in a
            // Built-in player compiles clean and still renders magenta, which is the other
            // half of how v0.1.0 shipped. So the pipeline is checked explicitly, and a sky
            // that cannot render is left off in favour of the flat clear colour.
            if (GraphicsSettings.currentRenderPipeline == null)
            {
                Debug.LogWarning("[Look] No SRP active — a URP sky would render magenta. " +
                                 "Keeping the flat clear colour.");
                return;
            }

            var sky = Resources.Load<Material>("DarkSky");
            if (sky == null || sky.shader == null || !sky.shader.isSupported)
            {
                Debug.LogWarning("[Look] DarkSky material unavailable — keeping the flat clear colour.");
                return;
            }

            // INSTANCED, not the asset. RenderSettings.skybox holds whatever it is given, and
            // a per-round retint of the loaded asset would write straight back into
            // Resources/DarkSky.mat — changing a committed file on disk every time the editor
            // played a round. A copy costs one material and removes that entirely.
            _sky = new Material(sky);
            RenderSettings.skybox = _sky;
        }

        private static void ApplyAtmosphere()
        {
            // These values only reach the device because Main.unity's own RenderSettings
            // now enable fog in the SAME mode. Unity's default fog stripping is Automatic:
            // it keeps a FOG_* shader variant only if some scene in the build enables that
            // mode in its lighting settings. The scene had m_Fog: 0, so every fog variant
            // was stripped, `#pragma multi_compile_fog` only ever compiled the no-fog
            // branch, and MixFog was a no-op in the player. Everything below was dead code
            // on device. If the mode here changes, Main.unity:16-21 must change with it.
            RenderSettings.fog = true;

            // LINEAR, not Exponential. The job is to bury the end of the road, and the road
            // has to be fully gone before the far clip at 222.6 m slices it in the open.
            // Exponential at 0.014 needs ~280 m to reach 98% — past the clip — and the
            // density that would reach it by 170 m washes half the contrast out of the road
            // at 30 m, where the game is actually played. Linear puts a hard wall exactly
            // where it is wanted and leaves everything nearer untouched: gates read at
            // ~90 m, labels at 34 m (TrackController.LabelVisibleMeters).
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 70f;
            RenderSettings.fogEndDistance = 170f;

            // Warm, and much brighter than the old (0.10, 0.07, 0.12). That value matched
            // DarkSky's _HorizonColor but NOT what the road actually meets: the sky adds
            // _GlowColor on top of that band, and looking down the road is exactly where
            // that glow is at full strength. Fog three stops darker than the sky behind it
            // cannot dissolve an edge — it draws one. This is _HorizonColor plus ~0.76 of
            // the glow, which is what the horizon in front of the player really is.
            RenderSettings.fogColor = new Color(0.44f, 0.30f, 0.23f);

            // Trilight, not flat. Flat ambient lights every surface identically, which is
            // why untextured boxes read as cardboard: a face pointing at the sky and a face
            // pointing at the ground came back the same colour. A gradient gives free
            // shading on geometry that has no texture to carry it.
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.20f, 0.19f, 0.30f);
            RenderSettings.ambientEquatorColor = new Color(0.24f, 0.18f, 0.22f);
            RenderSettings.ambientGroundColor = new Color(0.10f, 0.08f, 0.09f);
        }

        private static void ApplyKeyLight()
        {
            var lightGo = new GameObject("Moonlight");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;

            // Cool key against the warm ember horizon in the skybox. The contrast is what
            // gives an untextured silhouette its shape.
            light.color = new Color(0.75f, 0.78f, 0.95f);
            light.intensity = 1.1f;

            // Doc 04 said no realtime shadows on mobile. That was the right call for a
            // 300-unit crowd of individual renderers; the crowd is one instanced draw, so
            // the shadow pass is one more instanced draw, and the alternative is an army
            // that visibly hovers above the road.
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.72f;

            // shadowBias / shadowNormalBias are deliberately NOT set here. URP ignores a
            // Light's own bias unless it carries a UniversalAdditionalLightData with
            // usePipelineSettings == false, and a runtime AddComponent<Light>() has none —
            // so the two lines that used to sit here were inert, and silently contradicted
            // the real values. Shadow bias lives in UrpBootstrap.Tune().

            // This was Euler(55, -35, 0), whose forward is (-0.329, -0.819, +0.470). The
            // horizontal part points +Z — the SAME way the camera looks — so every shadow
            // was cast directly away from the viewer, landing behind its own caster and
            // fully hidden by it. A caster of height h at distance d hides d*h/(H-h) of
            // road behind itself; at H = 5.5 that is 2.1-2.9 m for the army, and the
            // shadow only reached 0.55 m. The feature was working and invisible.
            //
            // Now the light comes from ahead and to the right, so shadows rake ACROSS the
            // road toward the camera where the road itself displays them. 32 degrees of
            // elevation rather than 55 makes them 1.6x the caster's height instead of
            // 0.7x, which is what makes a crowd read as standing on something.
            lightGo.transform.rotation = Quaternion.Euler(32f, 250f, 0f);

            // Kept, so a world can retint and re-aim it. The old code dropped this reference
            // and there was no way to reach the light again for the life of the app.
            _keyLight = light;
        }

        /// <summary>
        /// Dress the world for one round: sky, weather, ambient and the angle of the light.
        ///
        /// Called from RunLoadingState once the level is known — the only per-round moment
        /// that holds the round index before anything is on screen. Everything here is a
        /// property that already existed on shaders that already ship; nothing new compiles.
        ///
        /// THE FOG MODE IS NOT TOUCHED, and that is not an oversight. Unity's Automatic
        /// variant stripping keeps a FOG_* shader variant only if some scene enables that mode
        /// in its lighting settings, and Main.unity declares Linear. A world that switched to
        /// exponential would compile fine, run fine in the editor, and have no fog at all on
        /// the device — which is exactly how fog shipped broken once already.
        /// </summary>
        public static void ApplyTheme(WorldTheme theme, ThemeVariant variant)
        {
            if (theme == null) return;
            float hue = variant.HueShift;

            if (_sky != null)
            {
                _sky.SetColorSafe("_ZenithColor", ThemePalette.Shifted(theme.SkyZenith, hue));
                _sky.SetColorSafe("_HorizonColor", ThemePalette.Shifted(theme.SkyHorizon, hue));
                _sky.SetColorSafe("_GroundColor", ThemePalette.Shifted(theme.SkyGround, hue));
                _sky.SetColorSafe("_GlowColor", ThemePalette.Shifted(theme.SkyGlow, hue));
                _sky.SetFloatSafe("_ZenithFalloff", theme.SkyZenithFalloff);
                _sky.SetFloatSafe("_GroundFalloff", theme.SkyGroundFalloff);
                _sky.SetFloatSafe("_GlowPower", theme.SkyGlowPower);
                _sky.SetFloatSafe("_GlowHeight", theme.SkyGlowHeight);
                _sky.SetFloatSafe("_StarStrength", Mathf.Max(0f, theme.SkyStars * variant.StarScale));

                // _GlowDirection was never written from a theme, so the ember band — the
                // single most recognisable feature of the sky — sat straight down +Z at the
                // same height in all eight worlds. The variant's azimuth rides on top of the
                // world's own yaw, so the glow drifts between rounds as well.
                float glowYaw = (theme.SkyGlowYaw + variant.LightAzimuth * 0.35f) * Mathf.Deg2Rad;
                _sky.SetVectorSafe("_GlowDirection",
                    new Vector4(Mathf.Sin(glowYaw), 0f, Mathf.Cos(glowYaw), 0f));
            }

            RenderSettings.fogColor = ThemePalette.Shifted(theme.Fog, hue);
            // Clamped to the distance the ground strip is actually built to cover. A world
            // whose fog ended past the road would show the player the edge of the world
            // instead of hiding it.
            float end = Mathf.Min(WorldThemes.MaxFogEnd, theme.FogEnd * variant.FogScale);
            float start = Mathf.Clamp(theme.FogStart * variant.FogScale, 5f, end - 20f);
            RenderSettings.fogStartDistance = start;
            RenderSettings.fogEndDistance = end;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = ThemePalette.Shifted(theme.AmbientSky, hue);
            RenderSettings.ambientEquatorColor = ThemePalette.Shifted(theme.AmbientEquator, hue);
            RenderSettings.ambientGroundColor = ThemePalette.Shifted(theme.AmbientGround, hue);

            ApplyGrade(theme, hue);

            if (_keyLight != null)
            {
                _keyLight.color = ThemePalette.Shifted(theme.LightColor, hue);
                _keyLight.intensity = Mathf.Max(0.1f, theme.LightIntensity);
                // The azimuth swing is the cheapest change that alters every shadow on the
                // road at once, which is why the variation spends most of its budget here.
                _keyLight.transform.rotation =
                    Quaternion.Euler(theme.LightPitch, theme.LightYaw + variant.LightAzimuth, 0f);
            }
        }

        /// <summary>
        /// Grade the frame for one world.
        ///
        /// Every value here is derived on WorldTheme from colours the world already
        /// declares, so a new world cannot forget to grade itself and no second table can
        /// drift out of step with the first. World 0 reproduces the shipped constants to
        /// within 0.02 per channel — the Ashen Road keeps the look it shipped with, and the
        /// other seven stop borrowing it.
        ///
        /// The hue shift is applied here too. It already moves the sky, the fog, the road and
        /// the props; a grade left un-shifted would drag every round of an act back toward
        /// the act's un-drifted colour, which is the same mistake at a smaller scale.
        /// </summary>
        private static void ApplyGrade(WorldTheme theme, float hue)
        {
            if (_bloom != null)
                _bloom.tint.value = ThemePalette.Shifted(theme.BloomTint, hue);

            if (_color != null)
            {
                _color.saturation.value = Mathf.Clamp(theme.GradeSaturation, -20f, 40f);
                _color.colorFilter.value = ThemePalette.Shifted(theme.GradeFilter, hue);
            }

            if (_grade != null)
            {
                Color shadows = ThemePalette.Shifted(theme.GradeShadows, hue);
                Color highlights = ThemePalette.Shifted(theme.GradeHighlights, hue);
                // The fourth channel is ShadowsMidtonesHighlights' own offset, not alpha, and
                // it must stay 0 — writing a colour's alpha into it lifts or crushes the
                // whole range rather than tinting it.
                _grade.shadows.value = new Vector4(shadows.r, shadows.g, shadows.b, 0f);
                _grade.highlights.value = new Vector4(highlights.r, highlights.g, highlights.b, 0f);
            }

            if (_whiteBalance != null)
            {
                _whiteBalance.temperature.value = Mathf.Clamp(theme.GradeTemperature, -100f, 100f);
                _whiteBalance.tint.value = Mathf.Clamp(theme.GradeTint, -100f, 100f);
            }

            if (_vignette != null)
                _vignette.color.value = ThemePalette.Shifted(theme.VignetteColor, hue);
        }

        /// <summary>
        /// Attach the post stack to the game camera. Safe to call more than once.
        /// </summary>
        public static void AttachPostProcessing(UnityEngine.Camera camera)
        {
            if (camera == null) return;

            camera.clearFlags = RenderSettings.skybox != null
                ? CameraClearFlags.Skybox
                : CameraClearFlags.SolidColor;
            camera.allowHDR = true;

            UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
            if (data != null)
            {
                data.renderPostProcessing = true;
                // MSAA in the pipeline asset already resolves edges; a second AA pass would
                // cost a full-screen blit to soften what is already resolved.
                data.antialiasing = AntialiasingMode.None;
            }

            if (_profile != null) return;

            _profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _profile.name = "BattleRunnerLook";
            BuildStack(_profile);

            var volumeGo = new GameObject("PostFX");
            var volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 0f;
            volume.weight = 1f;
            volume.sharedProfile = _profile;
            Object.DontDestroyOnLoad(volumeGo);
        }

        private static void BuildStack(VolumeProfile profile)
        {
            // Neutral, not ACES. ACES crushes the low end hard, and this game is almost
            // entirely low end — the blacks it would eat are the road and the sky.
            var tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.value = TonemappingMode.Neutral;

            // The single biggest change. Threshold sits just under white so only the
            // emissive accents — gates, spell, rim light — bloom, and the dark 90% of the
            // frame stays crisp.
            var bloom = _bloom = profile.Add<Bloom>(true);
            bloom.threshold.value = 0.85f;
            bloom.intensity.value = 1.15f;
            bloom.scatter.value = 0.72f;
            bloom.tint.value = new Color(1.0f, 0.86f, 0.72f);
            bloom.highQualityFiltering.value = false;   // mobile: half-res filtering is enough

            // maxIterations, not skipIterations: the latter was removed in URP 2023.1 and
            // is an obsolete-as-ERROR, not a warning. One under the default of six keeps a
            // wide soft glow while dropping the largest, cheapest-to-lose mip.
            bloom.maxIterations.value = 5;

            var color = _color = profile.Add<ColorAdjustments>(true);
            color.postExposure.value = 0.20f;
            color.contrast.value = 22f;
            color.saturation.value = -4f;
            color.colorFilter.value = new Color(1.0f, 0.96f, 0.90f);

            // Cool shadows, warm highlights. This one component is most of what reads as
            // "dark fantasy" rather than "dark".
            var grade = _grade = profile.Add<ShadowsMidtonesHighlights>(true);
            grade.shadows.value = new Vector4(0.86f, 0.92f, 1.18f, 0f);
            grade.midtones.value = new Vector4(1.00f, 1.00f, 1.00f, 0f);
            grade.highlights.value = new Vector4(1.12f, 1.02f, 0.86f, 0f);

            // WHITE BALANCE, which was not in the stack at all. It is a different operator
            // from the colour filter above: the filter MULTIPLIES the frame by a colour, so
            // it tints everything toward that colour, while this rotates the white POINT, so
            // the road and the fog and the props move together and the frame reads as being
            // lit differently rather than as having a gel over it. It folds into the grading
            // LUT, so per-world temperature is the cheapest difference between two rounds
            // that exists — it costs nothing per pixel.
            //
            // The rest of what the stack is missing is declined on cost rather than by
            // oversight: DepthOfField is a second full-screen pass on a fill-rate-bound
            // mobile renderer, FilmGrain is a texture read per pixel, and SplitToning would
            // duplicate what ShadowsMidtonesHighlights above already does.
            var whiteBalance = _whiteBalance = profile.Add<WhiteBalance>(true);
            whiteBalance.temperature.value = 0f;
            whiteBalance.tint.value = 0f;

            // Pulls the eye to the centre of a tall portrait frame and hides the point
            // where the fog meets the screen edge.
            var vignette = _vignette = profile.Add<Vignette>(true);
            vignette.color.value = new Color(0.02f, 0.01f, 0.04f);
            vignette.intensity.value = 0.34f;
            vignette.smoothness.value = 0.45f;
            vignette.rounded.value = false;
        }
    }
}
