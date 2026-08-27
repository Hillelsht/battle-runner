using BattleRunner.Core.Run;
using UnityEngine;


namespace BattleRunner.Gameplay.Track
{
    /// <summary>
    /// One math gate: emissive frame + glyph, no colliders — TrackController tests the
    /// crowd centroid against gate positions in lane space (doc 01, R3).
    /// </summary>
    public sealed class GateBehaviour : MonoBehaviour, IPoolable
    {
        public GateOp Op { get; private set; }
        public int Value { get; private set; }
        public int Lane { get; private set; }
        public bool Consumed { get; private set; }

        private TextMesh _label;
        private MeshRenderer[] _renderers;
        private static Material _addMat;
        private static Material _multiplyMat;
        private static Material _subtractMat;

        public static void SetSharedMaterials(Material baseMaterial)
        {
            _addMat = Tint(baseMaterial, new Color(0.30f, 0.55f, 1.2f));
            _multiplyMat = Tint(baseMaterial, new Color(1.3f, 0.85f, 0.25f));
            _subtractMat = Tint(baseMaterial, new Color(1.1f, 0.2f, 0.2f));
        }

        private static Material Tint(Material baseMaterial, Color emissive)
        {
            Material mat = ShaderSafety.CreateMaterial(baseMaterial);
            mat.SetColorSafe("_BaseColor", emissive * 0.35f);
            mat.SetColorSafe("_EmissionColor", emissive);
            mat.SetFloatSafe("_BobAmount", 0f); // gate frames must not run-bob
            return mat;
        }

        public static GateBehaviour Build(Font font)
        {
            var go = new GameObject("Gate");
            var gate = go.AddComponent<GateBehaviour>();

            // Thicker posts plus a filled infill plate: 0.16m bars projected to ~11px
            // at 22m, far below what reads on a phone. The plate carries the colour.
            gate._renderers = new MeshRenderer[4];
            gate._renderers[0] = Bar(go.transform, new Vector3(-1.05f, 1.5f, 0f), new Vector3(0.24f, 3.0f, 0.24f));
            gate._renderers[1] = Bar(go.transform, new Vector3(1.05f, 1.5f, 0f), new Vector3(0.24f, 3.0f, 0.24f));
            gate._renderers[2] = Bar(go.transform, new Vector3(0f, 3.0f, 0f), new Vector3(2.34f, 0.24f, 0.24f));
            gate._renderers[3] = Bar(go.transform, new Vector3(0f, 1.5f, 0.02f), new Vector3(1.86f, 2.7f, 0.06f));

            var labelGo = new GameObject("Label", typeof(TextMesh));
            labelGo.transform.SetParent(go.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 1.6f, -0.09f);
            // A TextMesh is legible from its LOCAL -Z side, and the camera already sits
            // at -Z looking toward +Z. The old 180-degree spin showed its back, which
            // the font material's Cull Off rendered as mirrored text.
            labelGo.transform.localRotation = Quaternion.Euler(12f, 0f, 0f);
            gate._label = labelGo.GetComponent<TextMesh>();
            gate._label.font = font;
            gate._label.fontSize = 64;
            gate._label.characterSize = 0.16f;
            gate._label.anchor = TextAnchor.MiddleCenter;
            MeshRenderer labelRenderer = gate._label.GetComponent<MeshRenderer>();
            labelRenderer.sharedMaterial = font.material;
            labelRenderer.sortingOrder = 1; // never z-fight the infill plate
            return gate;
        }

        private static MeshRenderer Bar(Transform parent, Vector3 position, Vector3 size)
        {
            var go = new GameObject("Bar", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.GetComponent<MeshFilter>().sharedMesh = ProceduralMeshes.BuildBox(Vector3.zero, size);
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return renderer;
        }

        public void Setup(GateOp op, int value, int lane, Vector3 worldPosition)
        {
            Op = op;
            Value = value;
            Lane = lane;
            Consumed = false;
            transform.position = worldPosition;

            Material mat = op switch
            {
                GateOp.Add => _addMat,
                GateOp.Multiply => _multiplyMat,
                _ => _subtractMat
            };
            foreach (MeshRenderer r in _renderers) r.sharedMaterial = mat;

            string symbol = op switch
            {
                GateOp.Add => "+",
                GateOp.Multiply => "x",
                _ => "-"
            };
            _label.text = $"{symbol}{value}";
            _label.color = mat.GetColorSafe("_EmissionColor", Color.white);
        }

        public void Consume() => Consumed = true;

        public void OnSpawned() { }
        public void OnDespawned() { }
    }
}
