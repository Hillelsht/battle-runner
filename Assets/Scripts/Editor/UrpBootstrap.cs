using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BattleRunner.Editor
{
    /// <summary>
    /// Creates and assigns the URP pipeline assets programmatically on first load, so
    /// the repo never ships hand-written pipeline YAML (validation report C). Runs via
    /// delayCall — never during the import-time static constructor — and is idempotent.
    /// </summary>
    [InitializeOnLoad]
    public static class UrpBootstrap
    {
        static UrpBootstrap()
        {
            // delayCall never fires under -batchmode -quit, which is how CI builds run.
            if (Application.isBatchMode) EnsurePipeline();
            else EditorApplication.delayCall += EnsurePipeline;
        }

        [MenuItem("BattleRunner/Setup Project (URP + Content)")]
        public static void SetupMenu()
        {
            EnsurePipeline();
            ContentBootstrap.EnsureContent();
        }

        public static void EnsurePipeline()
        {
            // An already-assigned pipeline is still TUNED, not just left alone. The asset is
            // generated once and then cached in the user's Library forever, so an early-return
            // meant a settings change here only ever reached a fresh clone — CI would render
            // with bloom while the machine that has to LOOK at it rendered without.
            if (GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset existing)
            {
                if (Tune(existing))
                {
                    EditorUtility.SetDirty(existing);
                    AssetDatabase.SaveAssets();
                    Debug.Log("[BattleRunner] URP pipeline re-tuned in place.");
                }
                return;
            }

            if (!AssetDatabase.IsValidFolder("Assets/Settings"))
                AssetDatabase.CreateFolder("Assets", "Settings");

            var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
            rendererData.name = "URP_Renderer";
            rendererData.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(
                "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
            AssetDatabase.CreateAsset(rendererData, "Assets/Settings/URP_Renderer.asset");

            UniversalRenderPipelineAsset pipeline = UniversalRenderPipelineAsset.Create(rendererData);
            pipeline.name = "URP";

            Tune(pipeline);
            AssetDatabase.CreateAsset(pipeline, "Assets/Settings/URP.asset");

            GraphicsSettings.defaultRenderPipeline = pipeline;

            int previousLevel = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(previousLevel, false);

            AssetDatabase.SaveAssets();
            Debug.Log("[BattleRunner] URP pipeline created and assigned (Assets/Settings).");
        }

        /// <summary>Far enough to include the boss at 26.1 m; see Tune for the arithmetic.</summary>
        private const float ShadowDistanceMeters = 28f;

        /// <summary>
        /// The look settings, in one place so a fresh asset and an existing one cannot drift.
        /// Returns true if anything actually changed.
        ///
        /// HDR is the load-bearing one. Every accent in this game is emissive — gates, spell,
        /// rim light on the crowd — and in LDR an emission of 1.4 is clamped to 1.0 and reads
        /// as flat bright paint. Bloom needs values above white to have anything to bloom, so
        /// without HDR the whole dark-fantasy palette renders as poster colours.
        ///
        /// MSAA is close to free on the tile-based GPUs this ships to, and this game is
        /// nothing but hard-edged boxes against a dark background — the worst case for
        /// aliasing, and where 4x buys the most.
        ///
        /// The shadow settings go through SerializedObject rather than properties on purpose.
        /// Several of URP's shadow properties expose only an internal setter, and which ones
        /// varies by URP version — a direct assignment that happens to be internal in 17.0.3
        /// is a compile error that only surfaces in CI, twenty minutes later. Serialized field
        /// names are stable across the 1x line and a rename degrades to a warning here instead.
        /// </summary>
        private static bool Tune(UniversalRenderPipelineAsset pipeline)
        {
            // shadowDistance is in the dirty test, not just assigned. Everything below is
            // written unconditionally, but only `changed` decides whether the asset is
            // saved — so on a machine that already generated this asset at the old
            // distance, a bump here would be applied in memory and thrown away.
            bool changed = !pipeline.supportsHDR || pipeline.msaaSampleCount != 4
                || !Mathf.Approximately(pipeline.shadowDistance, ShadowDistanceMeters);

            pipeline.supportsHDR = true;
            pipeline.msaaSampleCount = 4;
            pipeline.renderScale = 1f;
            // 45 m in ONE 1024 cascade is what erased the army. Unity wraps a single
            // cascade in a sphere around the whole frustum: at a 60 deg vertical FOV and a
            // 0.5625 portrait aspect that sphere is ~32 m in radius, so the shadow map
            // covered ~65 m across = ~63 mm per texel. URP scales BOTH biases by the texel
            // AND by the soft-shadow kernel radius, so m_ShadowNormalBias = 1.0 inset every
            // caster face by ~158 mm — against a unit torso only 110 mm in half-depth. The
            // box turned inside out and each body survived as a splinter a 5x5 PCF averaged
            // away to nothing.
            //
            // 24 m covered the army comfortably (the camera sits 10 m back and the
            // formation reaches ~3.7 m ahead of its centre) at ~17 mm per texel — but it
            // stopped 2 m short of the boss, which stands at CenterZ + 16 while the camera
            // sits at CenterZ - 10: 26.1 m away including the height difference. The
            // largest silhouette in the game was outside the shadow map entirely, casting
            // nothing and receiving nothing, and BossView has been asking for
            // ShadowCastingMode.On the whole time. 28 m reaches it with margin at ~20 mm
            // per texel, still five times finer than the 110 mm half-depth that decides
            // whether a caster turns inside out.
            pipeline.shadowDistance = ShadowDistanceMeters;

            // Still off: nothing here samples scene depth or colour, and both cost a full
            // extra pass on mobile.
            pipeline.supportsCameraDepthTexture = false;
            pipeline.supportsCameraOpaqueTexture = false;

            // One shadow-casting directional light, one cascade, short distance: enough to
            // put the army ON the road rather than hovering over it, which no amount of
            // colour grading can fake.
            var so = new SerializedObject(pipeline);
            changed |= SetBool(so, "m_MainLightShadowsSupported", true);
            changed |= SetInt(so, "m_MainLightRenderingMode", 1);      // PerPixel
            changed |= SetInt(so, "m_ShadowCascadeCount", 1);
            changed |= SetInt(so, "m_MainLightShadowmapResolution", 2048);
            changed |= SetFloat(so, "m_ShadowDepthBias", 0.6f);   // ~25 mm along the light
            changed |= SetFloat(so, "m_ShadowNormalBias", 0.35f); // ~15 mm vs a 110 mm half-depth
            changed |= SetBool(so, "m_SoftShadowsSupported", true);
            so.ApplyModifiedPropertiesWithoutUndo();

            return changed;
        }

        private static SerializedProperty Field(SerializedObject so, string name)
        {
            SerializedProperty property = so.FindProperty(name);
            if (property == null)
                Debug.LogWarning($"[BattleRunner] URP asset has no '{name}' — this URP version " +
                                 "renamed it; the shadow tuning for that field was skipped.");
            return property;
        }

        private static bool SetBool(SerializedObject so, string name, bool value)
        {
            SerializedProperty p = Field(so, name);
            if (p == null || p.boolValue == value) return false;
            p.boolValue = value;
            return true;
        }

        private static bool SetInt(SerializedObject so, string name, int value)
        {
            SerializedProperty p = Field(so, name);
            if (p == null || p.intValue == value) return false;
            p.intValue = value;
            return true;
        }

        private static bool SetFloat(SerializedObject so, string name, float value)
        {
            SerializedProperty p = Field(so, name);
            if (p == null || Mathf.Approximately(p.floatValue, value)) return false;
            p.floatValue = value;
            return true;
        }
    }
}
