using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine.Rendering;

namespace BattleRunner.Editor
{
    /// <summary>
    /// Guarantees a render pipeline is assigned BEFORE a player build compiles shaders.
    ///
    /// v0.1.0 shipped every object magenta: the repo carries no GraphicsSettings.asset
    /// and no URP asset, so the CI build ran on the Built-in pipeline. URP's scriptable
    /// stripper then found no URP asset and removed 100% of our UniversalForward
    /// variants ("After scriptable stripping: 0"), leaving a shader with no eligible
    /// SubShader — i.e. the error shader. UrpBootstrap alone was not enough because its
    /// EditorApplication.delayCall never fires in -batchmode.
    /// </summary>
    public sealed class PipelineGuard : IPreprocessBuildWithReport
    {
        // Must run before URP's own ShaderBuildPreprocessor decides what to strip.
        public int callbackOrder => -10000;

        public void OnPreprocessBuild(BuildReport report)
        {
            UrpBootstrap.EnsurePipeline();

            if (GraphicsSettings.defaultRenderPipeline == null)
            {
                throw new BuildFailedException(
                    "No render pipeline asset is assigned. This player would ship on the Built-in " +
                    "pipeline, every UniversalPipeline-tagged shader would be stripped, and the game " +
                    "would render as magenta error shader. Run BattleRunner > Setup Project (URP + Content).");
            }
        }
    }
}
