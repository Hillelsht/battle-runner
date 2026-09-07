using BattleRunner.Gameplay;
using BattleRunner.Core.Boss;
using BattleRunner.Data.Definitions;
using UnityEngine;

namespace BattleRunner.Gameplay.Combat
{
    /// <summary>
    /// The boss's body: ProceduralMeshes.Boss with a telegraph pulse. Encounter LOGIC
    /// lives in BossEncounterState via BossSim — this class is pure presentation, so
    /// the encounter stays drivable from a hand-authored RunResult (R9).
    /// </summary>
    public sealed class BossView : MonoBehaviour
    {
        private Transform _body;
        private MeshFilter _filter;
        private Material _material;
        private Color _baseEmission;
        private Color _telegraphColor = new Color(1.4f, 0.5f, 0.2f);
        private float _telegraphPulse;
        private float _baseScale = 6f;
        private float _hitFlash;

        private Transform _ward;
        private MeshRenderer _wardRenderer;
        private MaterialPropertyBlock _wardBlock;
        private float _wardLevel;
        private float _wardFlash;

        /// <summary>
        /// On-screen height each archetype should stand, in metres.
        ///
        /// The six meshes are deliberately NOT the same height in object space — a
        /// four-legged hound is 0.66 units tall and 1.34 long, a lich is 1.34 tall and
        /// almost flat — so a single 6x scale would put them on screen at 4 m and 8 m. The
        /// scale is derived from these targets instead, which keeps the roster's variety in
        /// the shapes where it reads and out of the sizes where it just looks like a bug.
        /// The hound is the shortest on purpose: it is long rather than tall, and its mass
        /// is horizontal.
        /// </summary>
        private static float TargetHeight(BossArchetype archetype) => archetype switch
        {
            BossArchetype.Volley => 7.4f,     // lich: tallest, staff above the head
            BossArchetype.Warded => 6.6f,     // warden: squat but very wide
            BossArchetype.Drain => 6.2f,      // leech: low and reaching forward
            BossArchetype.Summoner => 7.1f,   // shepherd: thin column under a halo
            BossArchetype.Enrage => 5.2f,     // hound: horizontal mass, ~10 m long
            _ => 6.7f                          // colossus
        };

        public void Initialize(Material baseMaterial, Material vfxMaterial)
        {
            var body = new GameObject("BossMesh", typeof(MeshFilter), typeof(MeshRenderer));
            body.transform.SetParent(transform, false);
            // Identity, not a 180-degree yaw. ProceduralMeshes.Boss is built facing -Z
            // like every other mesh here, which is toward the camera and the oncoming
            // army. The old yaw was inherited from when the boss WAS the unit mesh —
            // symmetric about both axes, so turning it around changed nothing. It would
            // now show the player the boss's back.
            body.transform.localRotation = Quaternion.identity;
            // The mesh is chosen per boss in Show, not pinned here: the roster is six
            // different silhouettes and this view is the one thing that draws all of them.
            _filter = body.GetComponent<MeshFilter>();
            _material = ShaderSafety.CreateMaterial(baseMaterial);
            _material.SetFloatSafe("_BobAmount", 0f); // at 6x scale the run-bob would look absurd
            // A TIGHT rim, not a wide one. Widening the lobe to 1.8 was the wrong move:
            // rim is pow(1 - dot(V,N), k), and on the faces the camera actually sees most
            // of (torso, head, pauldron fronts) dot(V,N) is ~0.97, so the term evaluates to
            // ~0.004 no matter how wide the lobe or how strong the multiplier. Widening it
            // only lit the profile faces the camera barely sees. Shape comes from the
            // wrapped diffuse term in CrowdInstanced instead; the rim goes back to being
            // what it is good at — a bright edge on the true silhouette.
            _material.SetFloatSafe("_RimPower", 3.5f);
            _material.SetFloatSafe("_RimStrength", 0.55f);
            var renderer = body.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            // Casts: the boss is the largest silhouette in the game.
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            _body = body.transform;
            BuildWard(vfxMaterial);
            gameObject.SetActive(false);
        }

        /// <summary>
        /// The Grave Warden's shell. Same hemisphere and same additive material as the
        /// player's own shield dome, on purpose: the player has already been taught what
        /// that shape means, and the fight is asking them to break one instead of raise one.
        /// </summary>
        private void BuildWard(Material vfxMaterial)
        {
            // Same rule as VfxSystem and ShieldDome: no material, no shell, never a
            // magenta one. A boss with no ward VISUAL still has a ward.
            if (vfxMaterial == null || vfxMaterial.shader == null || !vfxMaterial.shader.isSupported)
                return;

            var go = new GameObject("BossWard", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            go.GetComponent<MeshFilter>().sharedMesh = ProceduralMeshes.Dome;
            _wardRenderer = go.GetComponent<MeshRenderer>();
            _wardRenderer.sharedMaterial = vfxMaterial;
            _wardRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _wardRenderer.receiveShadows = false;
            _wardRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            _wardRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            go.SetActive(false);
            _ward = go.transform;
            _wardBlock = new MaterialPropertyBlock();
        }

        public void Show(BossDefinition def, Vector3 position)
        {
            Color tint = def.TintColor;
            // NOT tint * 0.5f. These are sRGB values in a linear project, so halving in
            // sRGB is a 0.234x cut in linear — the gamma curve charges for it twice. The
            // old (0.55, 0.50, 0.45) tint arrived as a 5% reflectance albedo: darker than
            // the road the boss stands on, and the reason a thing called Bone Colossus
            // rendered as a #3a3a3a cutout. Any future darkening belongs in linear.
            _material.SetColorSafe("_BaseColor", tint);
            _baseEmission = tint * 0.5f;
            _material.SetColorSafe("_EmissionColor", _baseEmission);
            _telegraphColor = def.TelegraphColor;
            transform.position = position;

            Mesh mesh = ProceduralMeshes.Boss(def.Archetype);
            _filter.sharedMesh = mesh;
            float height = Mathf.Max(0.01f, mesh.bounds.size.y);
            _baseScale = TargetHeight(def.Archetype) / height;
            _body.localScale = Vector3.one * _baseScale;

            SetWard(0f);
            _telegraphPulse = 0f;
            // Otherwise a flash still decaying when the last boss was hidden resumes
            // on the next one.
            _hitFlash = 0f;
            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);

        /// <summary>How much of the boss's ward is left, 0..1. Anything above zero draws the shell.</summary>
        public void SetWard(float fraction)
        {
            _wardLevel = Mathf.Clamp01(fraction);
            if (_ward != null && _wardLevel <= 0f) _ward.gameObject.SetActive(false);
        }

        /// <summary>The ward just ate a hit, or just shattered.</summary>
        public void FlashWard() => _wardFlash = 1f;

        /// <summary>0 = calm; ramps to 1 across the telegraph window before an attack lands.</summary>
        public void SetTelegraph(float intensity)
        {
            _telegraphPulse = Mathf.Clamp01(intensity);
        }

        /// <summary>The boss took a hit. This is the player's only confirmation it landed.</summary>
        public void FlashHit()
        {
            if (_body == null) return;
            _hitFlash = 1f;
            // 0.96 was a 0.23-unit dip on the old 5.7-unit figure, recovered in 2.4
            // frames at the existing rate of 6/s — literally invisible. 0.90 is 0.67
            // units on the 6.7-unit boss mesh, over about 7 frames, which reads as a
            // flinch.
            _body.localScale = Vector3.one * (_baseScale * 0.90f);
        }

        private void Update()
        {
            if (_body == null) return;
            float pulse = 1f + _telegraphPulse * 0.12f * Mathf.Sin(Time.time * 22f);
            float recover = Mathf.MoveTowards(_body.localScale.x, _baseScale * pulse, Time.deltaTime * 6f);
            _body.localScale = Vector3.one * recover;

            // LINEAR decay, not squared. A squared falloff at this rate is above half
            // intensity for barely two frames — the same "gone before you see it"
            // failure as the old scale dip. Linear holds it for about seven.
            _hitFlash = Mathf.Max(0f, _hitFlash - Time.deltaTime * 4.5f);

            Color emission = Color.Lerp(_baseEmission, _telegraphColor, _telegraphPulse);
            emission += new Color(1.10f, 0.75f, 0.40f) * _hitFlash;
            _material.SetColorSafe("_EmissionColor", emission);

            // The gate that makes the flash actually land. CrowdInstanced adds
            // _EmissionColor * (rim * _RimStrength + _EmissionFlat), and the faces
            // pointed most directly at the camera — still most of the torso and head —
            // have rim near 0. Without driving the view-independent flat term too, the
            // flash arrives on those faces at 15% strength and is lost.
            // 0.03 at rest, not 0.15. The flat term is view- and normal-independent — it
            // is paint, not light — and at 0.15 against a 5% albedo it WAS the boss: two
            // thirds of every pixel, identical on the horns, the head and the cleaver. Now
            // that the albedo is real, the resting term drops to the crowd's own value and
            // the hit flash goes from a 4x jump over it to a 19x one.
            // The TELEGRAPH drives this too, not just the hit flash. Dropping the resting
            // term from 0.15 to 0.03 is right — at a real albedo it is paint, not light —
            // but the telegraph is the only warning the player gets before a blow lands,
            // and its whole signal is this term multiplying an orange emission colour. At
            // 0.03 a fully wound-up boss brightened by a quarter; at 0.33 it goes over the
            // bloom threshold and visibly heats up, which is what a wind-up should do.
            _material.SetFloatSafe("_EmissionFlat",
                0.03f + 0.30f * _telegraphPulse + 0.55f * _hitFlash);

            UpdateWard();
        }

        private void UpdateWard()
        {
            if (_ward == null) return;

            _wardFlash = Mathf.Max(0f, _wardFlash - Time.deltaTime * 4f);
            if (_wardLevel <= 0f && _wardFlash <= 0f)
            {
                if (_ward.gameObject.activeSelf) _ward.gameObject.SetActive(false);
                return;
            }

            // Sized from the BODY's actual bounds rather than from a constant, because the
            // six meshes differ in shape by more than they differ in height — a shell fitted
            // to the lich would leave the hound's flanks outside it.
            Bounds b = _filter.sharedMesh != null ? _filter.sharedMesh.bounds : new Bounds(Vector3.zero, Vector3.one);
            float radius = Mathf.Max(b.extents.x, b.extents.z) * _baseScale + 1.4f;
            float height = b.size.y * _baseScale * 0.72f;

            _ward.localPosition = new Vector3(0f, 0.05f, 0f);
            _ward.localScale = new Vector3(radius, Mathf.Max(radius * 0.6f, height), radius);

            // The shell DIMS as it is worn down, so the bar is not the only place the
            // player can read how close they are to breaking it.
            float fade = Mathf.Clamp01(0.28f + 0.62f * _wardLevel + _wardFlash * 0.8f);
            _wardBlock.SetColor("_TintColor", Color.Lerp(WardHeld, WardStruck, _wardFlash));
            _wardBlock.SetFloat("_Fade", fade);
            _wardBlock.SetFloat("_Band", 0f);
            _wardBlock.SetFloat("_Fresnel", 1f);
            _wardBlock.SetFloat("_FresnelPower", Mathf.Lerp(2.4f, 1.1f, _wardFlash));
            _wardRenderer.SetPropertyBlock(_wardBlock);

            if (!_ward.gameObject.activeSelf) _ward.gameObject.SetActive(true);
        }

        // Cold and pale where the player's own dome is cyan — near enough to read as the
        // same kind of object, far enough that nobody mistakes whose it is.
        private static readonly Color WardHeld = new Color(0.72f, 0.80f, 1.55f);
        private static readonly Color WardStruck = new Color(1.85f, 1.70f, 1.30f);
    }
}
