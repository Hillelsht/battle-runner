using BattleRunner.Gameplay;
using BattleRunner.Gameplay.Combat;
using BattleRunner.Gameplay.Crowd;
using UnityEngine;

namespace BattleRunner.Gameplay.Vfx
{
    /// <summary>
    /// An actual barrier over the army.
    ///
    /// ShieldWard already made the block window visible by recolouring the crowd's own
    /// emission, and on device it plainly fires — the army lights cyan and the HUD reads
    /// SHIELDED. But the player's report was still "no shield effect", and they were right
    /// about the thing that matters: recolouring the army reads as THE ARMY CHANGING
    /// COLOUR, not as a shield. A defensive verb needs a defensive object.
    ///
    /// So this draws the shell, and the ward stays as the secondary cue. Two channels for
    /// one event is not redundancy here: the dome says "you are protected", the ward keeps
    /// the units readable underneath it while the player is still steering.
    ///
    /// Additive plus fresnel is the whole trick. The surface facing the camera contributes
    /// almost nothing, so the army is never hidden; the turning edge lights up and
    /// describes the sphere. A dome that filled in would be worse than no dome at all,
    /// because it would cover the one thing the player is looking at.
    /// </summary>
    public sealed class ShieldDome : MonoBehaviour
    {
        // HDR and over the 0.85 bloom threshold at the rim, where fresnel concentrates it.
        private static readonly Color Held = new Color(0.55f, 1.15f, 1.70f);
        private static readonly Color Impact = new Color(1.70f, 1.85f, 2.00f);

        private const float RiseSeconds = 0.09f;
        private const float FallSeconds = 0.28f;
        private const float FlashSeconds = 0.22f;

        // The crowd is a wide, shallow blob, so a true half-sphere would tower over it.
        // Squashing to 0.62 keeps the dome hugging the army instead of becoming scenery.
        private const float HeightRatio = 0.62f;
        private const float Margin = 1.15f;
        private const float MinRadius = 2.2f;

        private ShieldSystem _shield;
        private CrowdController _crowd;
        private Transform _dome;
        private MeshRenderer _renderer;
        private MaterialPropertyBlock _block;
        private float _level;
        private float _flash;
        private bool _visible;

        public void Initialize(ShieldSystem shield, CrowdController crowd, Material vfxMaterial)
        {
            _shield = shield;
            _crowd = crowd;

            // Same rule as VfxSystem: no material, no dome, never a magenta one.
            if (vfxMaterial == null || vfxMaterial.shader == null || !vfxMaterial.shader.isSupported)
            {
                Debug.LogWarning("[Vfx] Shield dome disabled — additive material unusable here.");
                return;
            }

            var go = new GameObject("ShieldDome", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            go.GetComponent<MeshFilter>().sharedMesh = ProceduralMeshes.Dome;
            _renderer = go.GetComponent<MeshRenderer>();
            _renderer.sharedMaterial = vfxMaterial;
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            _renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            go.SetActive(false);

            _dome = go.transform;
            _block = new MaterialPropertyBlock();
        }

        /// <summary>A blow the shield actually ate — the dome takes the hit, not the army.</summary>
        public void FlashBlock() => _flash = 1f;

        public void Clear()
        {
            _level = 0f;
            _flash = 0f;
            if (_dome != null) _dome.gameObject.SetActive(false);
            _visible = false;
        }

        private void LateUpdate()
        {
            if (_dome == null || _shield == null || _crowd == null) return;

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            // Rise fast, fall slow, exactly as the ward does — a barrier that snapped off
            // would read as a dropped frame rather than as a window that expired.
            float target = _shield.IsActive ? 1f : 0f;
            float rate = target > _level ? 1f / RiseSeconds : 1f / FallSeconds;
            _level = Mathf.MoveTowards(_level, target, dt * rate);
            _flash = Mathf.Max(0f, _flash - dt / FlashSeconds);

            if (_level <= 0f && _flash <= 0f)
            {
                if (_visible) { _dome.gameObject.SetActive(false); _visible = false; }
                return;
            }

            // Sized to the army it is protecting, not to a constant: the envelope grows
            // from a handful of units to a few hundred, and a fixed dome would swallow the
            // small crowd and clip through the large one.
            float radius = Mathf.Max(MinRadius,
                Mathf.Max(_crowd.HalfWidth, (_crowd.FrontZ - _crowd.RearZ) * 0.5f) + Margin);

            // Pops in oversized and settles, so raising it reads as an act rather than as
            // a fade. The overshoot is on the rise only; the fall is a straight shrink.
            float pop = 1f + 0.18f * (1f - _level) * (target > 0f ? 1f : 0f);
            _dome.position = new Vector3(_crowd.CenterX, 0.02f, (_crowd.FrontZ + _crowd.RearZ) * 0.5f);
            _dome.localScale = new Vector3(radius * pop, radius * HeightRatio * pop, radius * pop);

            Color tint = Color.Lerp(Held, Impact, _flash);
            // The impact spike is added to the fade, not just the colour: a blocked blow
            // should brighten the whole shell for two frames, not merely shift its hue.
            float fade = Mathf.Clamp01(_level + _flash * 0.8f);

            _block.SetColor("_TintColor", tint);
            _block.SetFloat("_Fade", fade);
            _block.SetFloat("_Band", 0f);
            _block.SetFloat("_Fresnel", 1f);
            // Tighter fresnel on impact, so the flash reads as the shell being STRUCK
            // rather than as the whole dome simply turning up its brightness.
            _block.SetFloat("_FresnelPower", Mathf.Lerp(2.2f, 1.1f, _flash));
            _renderer.SetPropertyBlock(_block);

            if (!_visible) { _dome.gameObject.SetActive(true); _visible = true; }
        }
    }
}
