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
        private Material _material;
        private Color _baseEmission;
        private float _telegraphPulse;
        private float _baseScale = 6f;
        private float _hitFlash;

        public void Initialize(Mesh bossMesh, Material baseMaterial)
        {
            var body = new GameObject("BossMesh", typeof(MeshFilter), typeof(MeshRenderer));
            body.transform.SetParent(transform, false);
            // Identity, not a 180-degree yaw. ProceduralMeshes.Boss is built facing -Z
            // like every other mesh here, which is toward the camera and the oncoming
            // army. The old yaw was inherited from when the boss WAS the unit mesh —
            // symmetric about both axes, so turning it around changed nothing. It would
            // now show the player the boss's back.
            body.transform.localRotation = Quaternion.identity;
            body.GetComponent<MeshFilter>().sharedMesh = bossMesh;
            _material = ShaderSafety.CreateMaterial(baseMaterial);
            _material.SetFloatSafe("_BobAmount", 0f); // at 6x scale the run-bob would look absurd
            // The mesh is deliberately not axis-aligned, so the rim term finally has
            // varied normals to work with. Widen the lobe a little to use them.
            _material.SetFloatSafe("_RimPower", 1.8f);
            _material.SetFloatSafe("_RimStrength", 1.15f);
            var renderer = body.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            // Casts: the boss is the largest silhouette in the game.
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            _body = body.transform;
            gameObject.SetActive(false);
        }

        public void Show(BossDefinition def, Vector3 position)
        {
            Color tint = def.TintColor;
            _material.SetColorSafe("_BaseColor", tint * 0.5f);
            _baseEmission = tint * 0.9f;
            _material.SetColorSafe("_EmissionColor", _baseEmission);
            transform.position = position;
            _body.localScale = Vector3.one * _baseScale;
            _telegraphPulse = 0f;
            // Otherwise a flash still decaying when the last boss was hidden resumes
            // on the next one.
            _hitFlash = 0f;
            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);

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

            Color emission = Color.Lerp(_baseEmission, new Color(1.4f, 0.5f, 0.2f), _telegraphPulse);
            emission += new Color(1.10f, 0.75f, 0.40f) * _hitFlash;
            _material.SetColorSafe("_EmissionColor", emission);

            // The gate that makes the flash actually land. CrowdInstanced adds
            // _EmissionColor * (rim * _RimStrength + _EmissionFlat), and the faces
            // pointed most directly at the camera — still most of the torso and head —
            // have rim near 0. Without driving the view-independent flat term too, the
            // flash arrives on those faces at 15% strength and is lost.
            _material.SetFloatSafe("_EmissionFlat", 0.15f + 0.55f * _hitFlash);
        }
    }
}
