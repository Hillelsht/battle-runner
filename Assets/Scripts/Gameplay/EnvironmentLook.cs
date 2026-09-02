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

            RenderSettings.skybox = sky;
        }

        private static void ApplyAtmosphere()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;

            // Slightly thinner than before and tinted toward the sky's horizon band, so the
            // road now fades INTO the sky instead of into a differently-coloured wall.
            RenderSettings.fogDensity = 0.014f;
            RenderSettings.fogColor = new Color(0.10f, 0.07f, 0.12f);

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
            light.shadowBias = 0.04f;
            light.shadowNormalBias = 0.5f;

            lightGo.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
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
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.value = 0.85f;
            bloom.intensity.value = 1.15f;
            bloom.scatter.value = 0.72f;
            bloom.tint.value = new Color(1.0f, 0.86f, 0.72f);
            bloom.highQualityFiltering.value = false;   // mobile: half-res filtering is enough
            bloom.skipIterations.value = 1;

            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.value = 0.20f;
            color.contrast.value = 22f;
            color.saturation.value = -4f;
            color.colorFilter.value = new Color(1.0f, 0.96f, 0.90f);

            // Cool shadows, warm highlights. This one component is most of what reads as
            // "dark fantasy" rather than "dark".
            var grade = profile.Add<ShadowsMidtonesHighlights>(true);
            grade.shadows.value = new Vector4(0.86f, 0.92f, 1.18f, 0f);
            grade.midtones.value = new Vector4(1.00f, 1.00f, 1.00f, 0f);
            grade.highlights.value = new Vector4(1.12f, 1.02f, 0.86f, 0f);

            // Pulls the eye to the centre of a tall portrait frame and hides the point
            // where the fog meets the screen edge.
            var vignette = profile.Add<Vignette>(true);
            vignette.color.value = new Color(0.02f, 0.01f, 0.04f);
            vignette.intensity.value = 0.34f;
            vignette.smoothness.value = 0.45f;
            vignette.rounded.value = false;
        }
    }
}
