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
        /// <summary>True once this gate scored the crowd.</summary>
        public bool Consumed { get; private set; }

        /// <summary>
        /// True once the crowd has passed this gate's plane, whether or not it scored.
        /// Distinct from <see cref="Consumed"/>: a gate in another lane resolves without
        /// consuming, and must keep drawing until it is behind the camera.
        /// </summary>
        public bool Resolved { get; private set; }

        private TextMesh _label;
        private MeshRenderer _labelRenderer;
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

            // A gate must fit INSIDE its own lane. At 2.34 m wide on a 2.2 m lane pitch the
            // frames of adjacent lanes overlapped by 0.14 m, so a three-lane row spanned
            // 6.74 m of a 7.07 m frame and read as one solid wall rather than three choices.
            // 1.92 m leaves a 0.28 m gap between neighbours, and the 1.60 m aperture is wide
            // enough for the 1.54 m crowd to visibly pass through.
            gate._renderers = new MeshRenderer[4];
            gate._renderers[0] = Bar(go.transform, new Vector3(-0.88f, 1.30f, 0f), new Vector3(0.16f, 2.60f, 0.20f));
            gate._renderers[1] = Bar(go.transform, new Vector3(0.88f, 1.30f, 0f), new Vector3(0.16f, 2.60f, 0.20f));
            gate._renderers[2] = Bar(go.transform, new Vector3(0f, 2.60f, 0f), new Vector3(1.92f, 0.16f, 0.20f));
            gate._renderers[3] = Bar(go.transform, new Vector3(0f, 1.30f, 0.02f), new Vector3(1.60f, 2.36f, 0.06f));

            var labelGo = new GameObject("Label", typeof(TextMesh));
            labelGo.transform.SetParent(go.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 1.45f, -0.09f);
            // A TextMesh is legible from its LOCAL -Z side, and the camera already sits
            // at -Z looking toward +Z. The old 180-degree spin showed its back, which
            // the font material's Cull Off rendered as mirrored text.
            labelGo.transform.localRotation = Quaternion.Euler(12f, 0f, 0f);
            gate._label = labelGo.GetComponent<TextMesh>();
            gate._label.font = font;
            gate._label.fontSize = 64;
            gate._label.characterSize = 0.14f;
            gate._label.anchor = TextAnchor.MiddleCenter;
            gate._labelRenderer = gate._label.GetComponent<MeshRenderer>();
            gate._labelRenderer.sharedMaterial = font.material;
            gate._labelRenderer.sortingOrder = 1; // never z-fight the infill plate
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
            Resolved = false;
            if (_renderers.Length > 3 && _renderers[3] != null) _renderers[3].enabled = true;
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

        /// <summary>
        /// Labels use the built-in font material, which draws with ZTest Always — every gate
        /// in the level otherwise paints its number through all geometry at once, piling the
        /// far ones into the unreadable stack on the horizon. The track hides distant labels.
        /// </summary>
        public void SetLabelVisible(bool visible)
        {
            if (_labelRenderer != null && _labelRenderer.enabled != visible)
                _labelRenderer.enabled = visible;
        }

        /// <summary>
        /// Taken. Opens the aperture by dropping the infill plate — which is opaque
        /// (CrowdInstanced is Queue=Geometry with alpha forced to 1), so a 1.60 x 2.36 m
        /// slab would otherwise sweep backwards through the whole army as the gate slides
        /// past. Opening it also makes a taken gate read differently from a missed one.
        /// </summary>
        public void Consume()
        {
            Consumed = true;
            if (_renderers.Length > 3 && _renderers[3] != null) _renderers[3].enabled = false;
        }

        /// <summary>The crowd has drawn level with this gate; it scores now or never.</summary>
        public void Resolve() => Resolved = true;

        public void OnSpawned() => SetLabelVisible(true);
        public void OnDespawned() { }
    }
}
