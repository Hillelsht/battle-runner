using BattleRunner.Data.Definitions;
using UnityEngine;

namespace BattleRunner.Gameplay.Combat
{
    /// <summary>
    /// The boss's body: a hulking unit mesh with a telegraph pulse. Encounter LOGIC
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

        public void Initialize(Mesh unitMesh, Material baseMaterial)
        {
            var body = new GameObject("BossMesh", typeof(MeshFilter), typeof(MeshRenderer));
            body.transform.SetParent(transform, false);
            body.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            body.GetComponent<MeshFilter>().sharedMesh = unitMesh;
            _material = ShaderSafety.CreateMaterial(baseMaterial);
            _material.SetFloatSafe("_BobAmount", 0f); // at 6x scale the run-bob would look absurd
            var renderer = body.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
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
            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);

        /// <summary>0 = calm; ramps to 1 across the telegraph window before an attack lands.</summary>
        public void SetTelegraph(float intensity)
        {
            _telegraphPulse = Mathf.Clamp01(intensity);
        }

        public void FlashHit()
        {
            _body.localScale = Vector3.one * (_baseScale * 0.96f);
        }

        private void Update()
        {
            if (_body == null) return;
            float pulse = 1f + _telegraphPulse * 0.12f * Mathf.Sin(Time.time * 22f);
            float recover = Mathf.MoveTowards(_body.localScale.x, _baseScale * pulse, Time.deltaTime * 6f);
            _body.localScale = Vector3.one * recover;
            _material.SetColorSafe("_EmissionColor",
                Color.Lerp(_baseEmission, new Color(1.4f, 0.5f, 0.2f), _telegraphPulse));
        }
    }
}
