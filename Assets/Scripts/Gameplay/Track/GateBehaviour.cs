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

        /// <summary>
        /// True when this gate is drawn as a CROWD OF SOLDIERS rather than as an arch.
        ///
        /// "instead of doors, add soldiers, a crowd, like some of them join you". An add gate
        /// is a reinforcement, and a reinforcement is people — so the arch is hidden and
        /// SquadRenderer draws `Value` allied soldiers standing in the lane, who break and run
        /// into the army when it reaches them. Multiply keeps the arch, because multiplication
        /// has no crowd metaphor: there is no number of men that IS "times three".
        /// </summary>
        public bool DrawAsCrowd { get; private set; }

        /// <summary>Seconds since this gate was consumed, for the run-and-join animation.</summary>
        public float SinceConsumed { get; private set; } = -1f;

        /// <summary>How long the joining takes. Short — it is a flourish, not an event.</summary>
        public const float JoinSeconds = 0.55f;

        private TextMesh _label;
        private MeshRenderer _labelRenderer;
        private Transform _labelPivot;
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

            // 0.70, down from 1.15. The three colours above are already over white and
            // they are Color properties in a LINEAR project, so they are gamma-EXPANDED on
            // upload: the brightest channel arrives at 1.49 / 1.78 / 1.23 before the flat
            // term multiplies it. At 1.15 a face-on frame reached 1.7-2.0 linear against a
            // bloom threshold of 0.85, and the uprights' grazing inner faces — the ones
            // that read white-hot in a device screenshot — hit 3.16. At 0.70 the dimmest
            // gate colour still clears the threshold face-on (0.86), so every gate keeps
            // blooming; the peak just stops being four times over it.
            _addFrame = Tint(baseMaterial, add, emissionFlat: 0.70f, baseScale: 0.35f);
            _multiplyFrame = Tint(baseMaterial, multiply, emissionFlat: 0.70f, baseScale: 0.35f);
            _subtractFrame = Tint(baseMaterial, subtract, emissionFlat: 0.70f, baseScale: 0.35f);

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
            // Gates never set these, so they inherited the shader's WIDE default lobe
            // (_RimPower 2.5, _RimStrength 0.9) — which on hard-normal boxes is not an
            // edge term but a per-face constant that floods any face far off the view
            // axis. On the near gate that is the uprights' inner sides, adding +0.62 to
            // the emission factor on exactly the surface that reads brightest. The rails
            // were given this treatment already; the gates were missed.
            mat.SetFloatSafe("_RimPower", 5f);
            mat.SetFloatSafe("_RimStrength", 0.35f);
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
            // AN ARCH, NOT A DOOR FRAME. "The doors with +-* look bad" was fair: four thin
            // bars and an infill plate is a diagram of a gate, not a thing standing in a
            // road. Thick tapered piers, a stepped lintel and a keystone give it a
            // silhouette — and a silhouette is what reads at fifty metres through fog,
            // which is where the player actually has to make the decision.
            gate._renderers = new MeshRenderer[6];
            gate._renderers[0] = Bar(go.transform, new Vector3(-0.86f, 1.05f, 0f), new Vector3(0.34f, 2.10f, 0.40f));
            gate._renderers[1] = Bar(go.transform, new Vector3(0.86f, 1.05f, 0f), new Vector3(0.34f, 2.10f, 0.40f));
            // The lintel overhangs the piers, which is what makes it read as resting ON them
            // rather than as a third bar of the same frame.
            gate._renderers[2] = Bar(go.transform, new Vector3(0f, 2.26f, 0f), new Vector3(2.16f, 0.32f, 0.48f));
            gate._renderers[3] = Bar(go.transform, new Vector3(0f, 2.58f, 0f), new Vector3(0.46f, 0.34f, 0.44f));
            // Two springers where the piers meet the lintel: two small boxes that turn a
            // rectangle into an arch for almost nothing.
            gate._renderers[4] = Bar(go.transform, new Vector3(-0.60f, 2.06f, 0f), new Vector3(0.30f, 0.22f, 0.38f));
            gate._renderers[5] = Bar(go.transform, new Vector3(0.60f, 2.06f, 0f), new Vector3(0.30f, 0.22f, 0.38f));

            // Billboarded, via a pivot. The fixed 12-degree pitch this had assumed the
            // camera never leaves -Z, which stopped being true the moment the rig gained
            // dynamic pitch and shake.
            var pivotGo = new GameObject("LabelPivot");
            pivotGo.transform.SetParent(go.transform, false);
            pivotGo.transform.localPosition = new Vector3(0f, 1.45f, -0.09f);
            gate._labelPivot = pivotGo.transform;

            var labelGo = new GameObject("Label", typeof(TextMesh));
            labelGo.transform.SetParent(pivotGo.transform, false);
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
            SinceConsumed = -1f;
            // An ADD gate is a reinforcement, and a reinforcement is people. The arch is
            // hidden and SquadRenderer draws Value allied soldiers here instead.
            DrawAsCrowd = op == GateOp.Add;
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null)
                    _renderers[i].enabled = !DrawAsCrowd;
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

            // The keystone takes the PLATE material and everything else the frame's. The
            // plate used to fill the aperture — a 1.6 x 2.36 m slab that was 77% of the
            // gate's projected area, which is why a gate read as a solid coloured rectangle
            // with no visible frame. The arch has no infill at all: you run THROUGH it, and
            // being able to see the road on the far side is most of why it reads as a gate.
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
        /// <summary>Face the camera, and advance the run-and-join clock.</summary>
        public void Tick(float dt, Vector3 cameraPosition)
        {
            if (SinceConsumed >= 0f && SinceConsumed < JoinSeconds) SinceConsumed += dt;
            if (_labelPivot == null) return;
            Vector3 toCamera = cameraPosition - _labelPivot.position;
            toCamera.y = 0f;
            if (toCamera.sqrMagnitude > 1e-4f)
                _labelPivot.rotation = Quaternion.LookRotation(-toCamera, Vector3.up);
        }

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
            SinceConsumed = 0f;
            // The arch has no infill plate to open any more — you already run through it. An
            // add gate's crowd, however, now breaks and runs into the army; SinceConsumed is
            // the clock SquadRenderer animates that on.
        }

        /// <summary>The crowd has drawn level with this gate; it scores now or never.</summary>
        public void Resolve() => Resolved = true;

        public void OnSpawned() => SetLabelVisible(true);
        public void OnDespawned() { }
    }
}
