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
        // A gate is a FRAME and a PLATE, and they must not look the same. The plate is
        // 1.60 x 2.36 m against four bars only 0.16 m thick — 77% of the gate's projected
        // area — so painting both with one material makes the gate a solid coloured
        // rectangle with no visible frame at all. Frame and plate are now separate.
        private static Material _addFrame, _addPlate;
        private static Material _multiplyFrame, _multiplyPlate;
        private static Material _subtractFrame, _subtractPlate;

        public static void SetSharedMaterials(Material baseMaterial)
        {
            var add = new Color(0.30f, 0.55f, 1.2f);
            var multiply = new Color(1.3f, 0.85f, 0.25f);
            var subtract = new Color(1.1f, 0.2f, 0.2f);

            _addFrame = Tint(baseMaterial, add, emissionFlat: 1.15f, baseScale: 0.35f);
            _multiplyFrame = Tint(baseMaterial, multiply, emissionFlat: 1.15f, baseScale: 0.35f);
            _subtractFrame = Tint(baseMaterial, subtract, emissionFlat: 1.15f, baseScale: 0.35f);

            _addPlate = Tint(baseMaterial, add, emissionFlat: 0.16f, baseScale: 0.18f);
            _multiplyPlate = Tint(baseMaterial, multiply, emissionFlat: 0.16f, baseScale: 0.18f);
            _subtractPlate = Tint(baseMaterial, subtract, emissionFlat: 0.16f, baseScale: 0.18f);
        }

        /// <summary>
        /// A gate's bars face the camera dead-on, so the shader's rim term is ~0 on every
        /// visible surface — the entire glow has to come from the flat emission term. At
        /// the old constant 0.15 a gate landed below URP's bloom knee and contributed
        /// EXACTLY NOTHING to the bloom pass, which is why gates read as flat plates even
        /// after bloom was switched on. The frame is pushed well over the knee; the plate
        /// is deliberately left under it, so the bars glow and the aperture stays a hole.
        /// </summary>
        private static Material Tint(Material baseMaterial, Color emissive,
            float emissionFlat, float baseScale)
        {
            Material mat = ShaderSafety.CreateMaterial(baseMaterial);
            mat.SetColorSafe("_BaseColor", emissive * baseScale);
            mat.SetColorSafe("_EmissionColor", emissive);
            mat.SetFloatSafe("_EmissionFlat", emissionFlat);
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

            Material frame = op switch
            {
                GateOp.Add => _addFrame,
                GateOp.Multiply => _multiplyFrame,
                _ => _subtractFrame
            };
            Material plate = op switch
            {
                GateOp.Add => _addPlate,
                GateOp.Multiply => _multiplyPlate,
                _ => _subtractPlate
            };

            // Renderers 0-2 are the bars; 3 is the infill plate that fills the aperture.
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null)
                    _renderers[i].sharedMaterial = i == 3 ? plate : frame;

            string symbol = op switch
            {
                GateOp.Add => "+",
                GateOp.Multiply => "x",
                _ => "-"
            };
            _label.text = $"{symbol}{value}";
            _label.color = frame.GetColorSafe("_EmissionColor", Color.white);
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
