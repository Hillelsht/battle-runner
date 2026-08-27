using UnityEngine;
using UnityEngine.Rendering;

namespace BattleRunner.Gameplay
{
    /// <summary>
    /// Last line of defence against shipping the magenta error shader.
    ///
    /// Shader.isSupported only reports whether a shader COMPILED — not whether any of
    /// its SubShaders match the active pipeline. A URP-tagged shader in a Built-in
    /// player compiles fine and still renders magenta, which is exactly what v0.1.0 did.
    /// So the pipeline is checked explicitly and a stock shader substituted if needed.
    /// </summary>
    public static class ShaderSafety
    {
        private const string Preferred = "BattleRunner/CrowdInstanced";

        private static Shader _shader;
        private static bool _resolved;

        public static bool UsingFallback { get; private set; }

        public static Shader Resolve()
        {
            if (_resolved) return _shader;
            _resolved = true;

            bool srpActive = GraphicsSettings.currentRenderPipeline != null;
            if (!srpActive)
            {
                Debug.LogError("[ShaderSafety] No scriptable render pipeline is active. URP-tagged " +
                               "shaders cannot render here — falling back to a built-in shader.");
            }

            if (srpActive)
            {
                Shader preferred = Shader.Find(Preferred);
                if (preferred != null && preferred.isSupported)
                {
                    _shader = preferred;
                    return _shader;
                }
                Debug.LogError($"[ShaderSafety] '{Preferred}' is unusable on this device.");

                Shader lit = Shader.Find("Universal Render Pipeline/Lit");
                if (lit != null && lit.isSupported)
                {
                    _shader = lit;
                    UsingFallback = true;
                    return _shader;
                }
            }

            Shader legacy = Shader.Find("Diffuse");
            if (legacy == null || !legacy.isSupported) legacy = Shader.Find("Sprites/Default");
            _shader = legacy;
            UsingFallback = true;
            Debug.LogError($"[ShaderSafety] Falling back to '{(legacy != null ? legacy.name : "none")}'.");
            return _shader;
        }

        /// <summary>Copy of a template material, or a fresh one when the template's shader is unusable.</summary>
        public static Material CreateMaterial(Material template)
        {
            Shader resolved = Resolve();
            Material m = (template != null && template.shader == resolved)
                ? new Material(template)
                : new Material(resolved);
            m.enableInstancing = true;
            return m;
        }

        /// <summary>Setting a property a fallback shader lacks logs an error every call; route through these.</summary>
        public static void SetColorSafe(this Material m, string name, Color value)
        {
            if (m == null) return;
            if (m.HasProperty(name)) { m.SetColor(name, value); return; }
            if (name == "_BaseColor" && m.HasProperty("_Color")) m.SetColor("_Color", value);
        }

        public static void SetFloatSafe(this Material m, string name, float value)
        {
            if (m != null && m.HasProperty(name)) m.SetFloat(name, value);
        }

        public static Color GetColorSafe(this Material m, string name, Color fallback)
        {
            if (m == null) return fallback;
            if (m.HasProperty(name)) return m.GetColor(name);
            if (name == "_BaseColor" && m.HasProperty("_Color")) return m.GetColor("_Color");
            return fallback;
        }
    }
}
