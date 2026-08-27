using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BattleRunner.Editor
{
    /// <summary>
    /// Fails the build if our shader shipped as an empty stub.
    ///
    /// v0.1.0 built and "succeeded" while BattleRunner/CrowdInstanced had every variant
    /// stripped: it packed to 0.7 kb with "total internal programs: 0" and the player
    /// rendered entirely magenta. Nothing in the build reported an error. A healthy
    /// build packs it around 11 kb, so a tiny packed size is an unambiguous tell —
    /// and unlike grepping the log (9k lines, truncated by every log API we have)
    /// this is checkable in-process, every time.
    /// </summary>
    public sealed class ShippedShaderCheck : IPostprocessBuildWithReport
    {
        private const string ShaderPath = "Assets/Resources/CrowdInstanced.shader";
        private const ulong MinimumPackedBytes = 2048;

        public int callbackOrder => 0;

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report == null || report.packedAssets == null) return;

            bool found = false;
            ulong packedSize = 0;

            foreach (PackedAssets packed in report.packedAssets)
            {
                if (packed == null) continue;
                PackedAssetInfo[] contents = packed.contents;
                if (contents == null) continue;

                foreach (PackedAssetInfo info in contents)
                {
                    if (info.sourceAssetPath != ShaderPath) continue;
                    found = true;
                    packedSize += info.packedSize;
                }
            }

            if (!found)
            {
                // Not fatal on its own: asset packing detail varies by platform, and the
                // runtime ShaderSafety guard still covers us.
                Debug.LogWarning($"[ShippedShaderCheck] {ShaderPath} was not listed in the build report.");
                return;
            }

            var summary = new StringBuilder()
                .Append("[ShippedShaderCheck] ").Append(ShaderPath)
                .Append(" packed to ").Append(packedSize).Append(" bytes.");

            if (packedSize < MinimumPackedBytes)
            {
                throw new BuildFailedException(
                    summary + $" That is below {MinimumPackedBytes} bytes, which means its shader " +
                    "variants were stripped and the player would render magenta. Confirm a " +
                    "UniversalRenderPipelineAsset is assigned in Graphics and every Quality level.");
            }

            Debug.Log(summary.ToString());
        }
    }
}
