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
            if (GraphicsSettings.defaultRenderPipeline != null) return;

            if (!AssetDatabase.IsValidFolder("Assets/Settings"))
                AssetDatabase.CreateFolder("Assets", "Settings");

            var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
            rendererData.name = "URP_Renderer";
            rendererData.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(
                "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
            AssetDatabase.CreateAsset(rendererData, "Assets/Settings/URP_Renderer.asset");

            UniversalRenderPipelineAsset pipeline = UniversalRenderPipelineAsset.Create(rendererData);
            pipeline.name = "URP";

            // Mobile tuning per doc 04: no HDR on the base tier, modest shadows, no extra textures.
            pipeline.supportsHDR = false;
            pipeline.msaaSampleCount = 1;
            pipeline.shadowDistance = 30f;
            pipeline.supportsCameraDepthTexture = false;
            pipeline.supportsCameraOpaqueTexture = false;
            pipeline.renderScale = 1f;
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
    }
}
