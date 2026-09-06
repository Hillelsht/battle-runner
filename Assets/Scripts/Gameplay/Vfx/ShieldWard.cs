using BattleRunner.Gameplay;
using BattleRunner.Gameplay.Combat;
using UnityEngine;

namespace BattleRunner.Gameplay.Vfx
{
    /// <summary>
    /// The block window, made visible ON THE ARMY — where the player is already looking.
    ///
    /// Until now the shield was a pure timing mechanic with no feedback whatsoever: you
    /// flicked down and nothing on screen changed, so there was no way to learn whether
    /// the flick registered, and no way to see the window close.
    ///
    /// This recolours the crowd's own emission rather than drawing a translucent dome.
    /// The project ships exactly three shaders and all are opaque, so a shell would need
    /// a fourth in Resources — the shader-stripping path that shipped v0.1.0 as solid
    /// magenta. Recolouring costs no new mesh, material, shader, asset or draw call.
    ///
    /// It lights the SILHOUETTE rather than filling the bodies, because CrowdInstanced
    /// adds <c>_EmissionColor * (rim * _RimStrength + _EmissionFlat)</c> and the crowd
    /// runs a tight rim with almost no flat term. That matters: the player is still
    /// steering while the ward is up, so the army has to stay readable through it.
    /// Bloom turns the result into actual light for free.
    /// </summary>
    public sealed class ShieldWard : MonoBehaviour
    {
        // Absolute targets, not multipliers: the rim peak lands well above the 0.85 bloom
        // knee so the ward reads as light rather than as pale blue paint.
        private static readonly Color Ward = new Color(0.90f, 1.80f, 3.20f);
        private static readonly Color Block = new Color(3.00f, 3.20f, 3.40f);

        private const float RiseSeconds = 0.10f;
        private const float FallSeconds = 0.30f;
        private const float FlashSeconds = 0.20f;

        private ShieldSystem _shield;
        private Material _crowdMaterial;
        private Material _heroMaterial;
        private Color _crowdRest;
        private Color _heroRest;
        private float _level;
        private float _flash;
        private bool _tinted;

        public void Initialize(ShieldSystem shield, Material crowdMaterial, Material heroMaterial)
        {
            _shield = shield;
            _crowdMaterial = crowdMaterial;
            _heroMaterial = heroMaterial;

            // Read the rest colours back rather than hard-coding them, so a change in the
            // bootstrap's palette cannot leave the ward restoring a stale tint.
            _crowdRest = crowdMaterial != null
                ? crowdMaterial.GetColorSafe("_EmissionColor", Color.black) : Color.black;
            _heroRest = heroMaterial != null
                ? heroMaterial.GetColorSafe("_EmissionColor", Color.black) : Color.black;
        }

        /// <summary>A blow the shield actually ATE. The one frame the player must not miss.</summary>
        public void FlashBlock() => _flash = 1f;

        /// <summary>Put the army back to its resting colour and forget any pending flash.</summary>
        public void Clear()
        {
            _level = 0f;
            _flash = 0f;
            Apply(0f);
        }

        private void LateUpdate()
        {
            if (_shield == null || _crowdMaterial == null || _heroMaterial == null) return;

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            // Rise fast, fall slow. A window that snapped off would read as a dropped
            // frame; a window that fades tells the player it EXPIRED rather than failed.
            float target = _shield.IsActive ? 1f : 0f;
            float rate = target > _level ? 1f / RiseSeconds : 1f / FallSeconds;
            _level = Mathf.MoveTowards(_level, target, dt * rate);

            _flash = Mathf.Max(0f, _flash - dt / FlashSeconds);

            // Skip the material writes entirely once everything has settled back to rest.
            if (_level <= 0f && _flash <= 0f)
            {
                if (_tinted) { Apply(0f); _tinted = false; }
                return;
            }

            Apply(_level);
            _tinted = true;
        }

        private void Apply(float level)
        {
            Color crowd = Color.Lerp(_crowdRest, Ward, level);
            Color hero = Color.Lerp(_heroRest, Ward, level);
            if (_flash > 0f)
            {
                crowd = Color.Lerp(crowd, Block, _flash);
                hero = Color.Lerp(hero, Block, _flash);
            }

            _crowdMaterial.SetColorSafe("_EmissionColor", crowd);
            _heroMaterial.SetColorSafe("_EmissionColor", hero);
        }
    }
}
