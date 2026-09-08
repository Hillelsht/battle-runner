using System.Collections.Generic;
using BattleRunner.Core.Art;
using BattleRunner.Core.Progression;
using BattleRunner.Core.World;
using BattleRunner.Gameplay.Art;
using BattleRunner.Gameplay.Crowd;
using UnityEngine;

namespace BattleRunner.Gameplay.Track
{
    /// <summary>
    /// Everything standing beside the road: the verge you pass at arm's length, the fields
    /// behind it, and the castles, mills and mausoleums on the skyline.
    ///
    /// WHAT THIS REPLACED. The old RoadsideProps drew ten procedural meshes, all under 1.36
    /// units tall, in one grey per world, in two fixed bands, at one scale range. Two worlds
    /// could differ only in WHICH THREE of the ten they drew — which is what a player looking
    /// at round 1 and round 3 correctly called "minor colors only". The ten meshes are still
    /// here, mixed through the verge: they are the only scenery authored for this game, they
    /// cost nothing, and they stop an imported kit from reading as an imported kit.
    ///
    /// THREE ZONES, AND THEY ARE A BUDGET RATHER THAN A LABEL.
    ///   verge     5.2 to 12 m   small things, dragged hard toward the world's own stone
    ///   field      12 to 40 m   trees, fences, carts — the middle distance being lived in
    ///   landmark   26 to 52 m   structures, in Kenney's own colour, on the land
    ///
    /// A LANDMARK IS EXPANDED, NOT DRAWN. Landmarks.Keep is twenty-nine parts, and every one
    /// of them goes into the same per-piece instancing bucket as the identical part of every
    /// other castle in the level. Six castles cost the same three draw calls as one; a baked
    /// composite would have cost six draws of seven thousand vertices.
    ///
    /// ONE INSTANCED DRAW PER PIECE, as CrowdRenderer does it. Placement is generated once
    /// per round from a hash of the round index, so a round is dressed identically every time
    /// it is played, and the visible slice is re-gathered only after eight metres of travel.
    /// </summary>
    public sealed class SceneryField : MonoBehaviour
    {
        private const float VergeInner = 5.2f;
        private const float VergeOuter = 12.0f;
        private const float FieldInner = 12.0f;
        private const float FieldOuter = 40.0f;
        /// <summary>
        /// Landmarks sit between these. The far edge is inside TrackController's 70 m of land
        /// with room for the largest footprint (3.3 local units at landmark scale) to stand on
        /// it; the near edge keeps the same footprint clear of the road by about nine metres.
        /// </summary>
        private const float LandmarkInner = 26.0f;
        private const float LandmarkOuter = 52.0f;

        private const float WindowAhead = 195f;
        private const float WindowBehind = 25f;
        private const float RebuildStepMeters = 8f;

        // Per-zone caps. The verge is dense and small, landmarks are rare and enormous; one
        // shared cap would either starve the verge or reserve memory for two hundred castles.
        private const int VergeCap = 144;
        private const int FieldCap = 96;
        private const int LandmarkCap = 64;

        /// <summary>Kinds 0..9 are the procedural props; everything above is a pack piece.</summary>
        private const int ProceduralKinds = 10;

        private struct Placement
        {
            public int Kind;
            public float Z;
            public Matrix4x4 Trs;
        }

        private CrowdController _crowd;

        // Four materials: the procedural props keep the crowd shader they always used, and
        // each scenery zone gets its own instance of the scenery shader so the three tints
        // are three uniform values rather than three passes over one material per frame.
        private Material _propMaterial;
        private Material[] _zoneMaterials;
        private bool _instancingSupported;
        private bool _sceneryReady;

        private readonly List<Placement> _placements = new List<Placement>(1024);
        private Matrix4x4[][] _buckets;
        private int[] _counts;
        private int[] _zoneOf;
        private Mesh[] _meshes;
        private int _kindCount;

        private float _lastGatherZ = float.NegativeInfinity;
        private bool _dressed;

        public void Initialize(CrowdController crowd, Material baseMaterial)
        {
            _crowd = crowd;

            _propMaterial = ShaderSafety.CreateMaterial(baseMaterial);
            // Not the crowd's settings. _BobAmount would make a graveyard march, and
            // _ToneSpread decodes a per-unit phase from INSTANCE SCALE inside a 0.44-0.50
            // window — props are scaled far outside it and would every one pin to the top of
            // that curve. Pinned here rather than trusted to a default.
            _propMaterial.SetFloatSafe("_BobAmount", 0f);
            _propMaterial.SetFloatSafe("_ToneSpread", 0f);
            _propMaterial.SetFloatSafe("_RimPower", 4f);
            _propMaterial.SetFloatSafe("_RimStrength", 0.30f);
            _propMaterial.SetFloatSafe("_EmissionFlat", 0.02f);

            SceneryMeshes.Load();
            _sceneryReady = SceneryMeshes.Available;
            _zoneMaterials = LoadZoneMaterials();
            if (_zoneMaterials == null) _sceneryReady = false;

            _kindCount = ProceduralKinds + (_sceneryReady ? SceneryPieces.Count : 0);
            _buckets = new Matrix4x4[_kindCount][];
            _counts = new int[_kindCount];
            _zoneOf = new int[_kindCount];
            _meshes = new Mesh[_kindCount];

            for (int k = 0; k < ProceduralKinds; k++)
            {
                _meshes[k] = ProceduralMeshes.Prop((PropKind)k);
                _zoneOf[k] = (int)SceneryZone.Verge;
            }
            if (_sceneryReady)
            {
                for (int i = 0; i < SceneryPieces.Count; i++)
                {
                    _meshes[ProceduralKinds + i] = SceneryMeshes.At(i);
                    _zoneOf[ProceduralKinds + i] = (int)SceneryPieces.Zones[i];
                }
            }

            _instancingSupported = SystemInfo.supportsInstancing
                                   && _propMaterial != null && _propMaterial.enableInstancing;
        }

        /// <summary>
        /// Three instances of the scenery material, one per zone. Null when the shader is
        /// unavailable, which costs the player imported scenery and nothing else — the
        /// procedural props still draw.
        /// </summary>
        private static Material[] LoadZoneMaterials()
        {
            if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null
                || ShaderSafety.UsingFallback)
                return null;

            var template = Resources.Load<Material>("Scenery");
            if (template == null || template.shader == null || !template.shader.isSupported)
            {
                Debug.LogWarning("[Scenery] Scenery material unavailable — imported meshes will not draw.");
                return null;
            }

            var zones = new Material[3];
            for (int i = 0; i < zones.Length; i++)
            {
                zones[i] = new Material(template);
                zones[i].enableInstancing = true;
            }
            return zones;
        }

        /// <summary>
        /// Dress a round. Called once from RunLoadingState with the round index the world was
        /// chosen from, so the layout is part of that round's identity rather than a reroll.
        /// </summary>
        public void Build(WorldTheme theme, ThemeVariant variant, int roundIndex,
            float fromZ, float toZ)
        {
            Clear();
            if (theme == null || _propMaterial == null) return;

            float hue = variant.HueShift;
            Color stone = ThemePalette.Shifted(theme.PropStone, hue);
            _propMaterial.SetColorSafe("_BaseColor", stone);
            // The accent, well below the bloom knee. Scenery should catch the world's colour
            // on its edges, not become a light source competing with the gates.
            Color rim = ThemePalette.Shifted(theme.Accent.Scaled(0.22f), hue);
            _propMaterial.SetColorSafe("_EmissionColor", rim);

            SceneryPalette palette = theme.Scenery;
            bool useScenery = _sceneryReady && palette != null && palette.IsComplete;
            if (useScenery) TintZones(palette, stone, rim);

            uint rng = Seed(roundIndex);

            for (int side = -1; side <= 1; side += 2)
            {
                // The procedural ten always run, at their original density, so the verge
                // never becomes purely imported.
                float propDensity = Mathf.Clamp(theme.PropDensity * variant.PropDensity, 2f, 40f);
                ScatterProps(theme, ref rng, side, fromZ, toZ, 100f / propDensity);

                if (!useScenery) continue;
                ScatterPieces(palette.Verge, SceneryZone.Verge, palette.VergeScale, 0.75f, 1.35f,
                    ref rng, side, fromZ, toZ,
                    100f / Mathf.Max(1f, palette.VergeDensity * variant.PropDensity),
                    VergeInner, VergeOuter);
                ScatterPieces(palette.Field, SceneryZone.Field, palette.FieldScale, 0.70f, 1.45f,
                    ref rng, side, fromZ, toZ,
                    100f / Mathf.Max(1f, palette.FieldDensity * variant.PropDensity),
                    FieldInner, FieldOuter);
                ScatterLandmarks(palette, ref rng, side, fromZ, toZ);
            }

            _placements.Sort((a, b) => a.Z.CompareTo(b.Z));
            AllocateBuckets();
            _dressed = _placements.Count > 0;
            _lastGatherZ = float.NegativeInfinity;
        }

        /// <summary>
        /// "Bright landmarks, dark verge", applied. Kenney's palette is cheerful and
        /// saturated; the roadside the player actually looks down has to stay grim or this
        /// stops being dark fantasy. The horizon keeps its colour, because the contrast
        /// between a grim verge and a lit castle is worth more than either alone.
        /// </summary>
        private void TintZones(SceneryPalette palette, Color stone, Color rim)
        {
            float[] amounts = { palette.VergeTint, palette.FieldTint, palette.LandmarkTint };
            for (int i = 0; i < _zoneMaterials.Length; i++)
            {
                Material m = _zoneMaterials[i];
                m.SetColorSafe("_TintColor", stone);
                m.SetFloatSafe("_TintAmount", Mathf.Clamp01(amounts[i]));
                m.SetColorSafe("_EmissionColor", rim);
                // The verge is close enough for a rim to read as a highlight; a castle at
                // 50 m only gets a halo out of it, so the term falls off with the zone.
                m.SetFloatSafe("_RimStrength", i == 0 ? 0.30f : (i == 1 ? 0.20f : 0.12f));
                m.SetFloatSafe("_EmissionFlat", 0.02f);
                m.SetColorSafe("_BaseColor", Color.white);
            }
        }

        public void Clear()
        {
            _placements.Clear();
            _dressed = false;
            if (_counts != null)
                for (int k = 0; k < _counts.Length; k++) _counts[k] = 0;
        }

        private void ScatterProps(WorldTheme theme, ref uint rng, int side,
            float fromZ, float toZ, float step)
        {
            if (theme.Props == null || theme.Props.Length == 0) return;
            float z = fromZ + Next01(ref rng) * step;
            while (z < toZ)
            {
                var kind = theme.Props[(int)(NextUInt(ref rng) % (uint)theme.Props.Length)];
                Add((int)kind, side * Mathf.Lerp(VergeInner, VergeOuter, Next01(ref rng)), z,
                    Mathf.Lerp(0.80f, 1.70f, Next01(ref rng)), Next01(ref rng) * 360f, ref rng);
                z += step * Mathf.Lerp(0.55f, 1.55f, Next01(ref rng));
            }
        }

        private void ScatterPieces(string[] names, SceneryZone zone, float baseScale,
            float minJitter, float maxJitter, ref uint rng, int side, float fromZ, float toZ,
            float step, float inner, float outer)
        {
            if (names == null || names.Length == 0) return;
            float z = fromZ + Next01(ref rng) * step;
            while (z < toZ)
            {
                int piece = SceneryPieces.IndexOf(names[(int)(NextUInt(ref rng) % (uint)names.Length)]);
                if (piece >= 0)
                {
                    // Distance is biased outward by squaring: a uniform draw across a 28 m
                    // band puts as much in the first metre as the last, which crowds the
                    // near edge and leaves the far one bare.
                    float t = Next01(ref rng);
                    if (zone == SceneryZone.Field) t = t * t;
                    Add(ProceduralKinds + piece, side * Mathf.Lerp(inner, outer, t), z,
                        baseScale * Mathf.Lerp(minJitter, maxJitter, Next01(ref rng)),
                        Next01(ref rng) * 360f, ref rng);
                }
                z += step * Mathf.Lerp(0.55f, 1.55f, Next01(ref rng));
            }
        }

        private void ScatterLandmarks(SceneryPalette palette, ref uint rng, int side,
            float fromZ, float toZ)
        {
            float spacing = Mathf.Max(40f, palette.LandmarkSpacing);
            float z = fromZ + Next01(ref rng) * spacing;
            while (z < toZ)
            {
                Landmark mark = Landmarks.ByName(
                    palette.Landmarks[(int)(NextUInt(ref rng) % (uint)palette.Landmarks.Length)]);
                if (mark != null)
                {
                    float scale = palette.LandmarkScale;
                    float reach = mark.Radius * scale;
                    // Keep the whole footprint inside the band. Clamping the CENTRE rather
                    // than the edge is what would put a castle wall over the road.
                    float x = side * Mathf.Lerp(LandmarkInner + reach,
                        Mathf.Max(LandmarkInner + reach, LandmarkOuter - reach), Next01(ref rng));
                    // Roughly facing the road, so a gate or a door is something the player
                    // sees rather than something on the far side of the building.
                    float yaw = (side < 0 ? 90f : 270f) + (Next01(ref rng) - 0.5f) * 60f;
                    PlaceLandmark(mark, new Vector3(x, 0f, z), yaw, scale);
                }
                z += spacing * Mathf.Lerp(0.7f, 1.35f, Next01(ref rng));
            }
        }

        /// <summary>
        /// Expand a landmark into its parts. Local positions scale with the whole structure;
        /// each part's own scale multiplies only its mesh, so a doubled column gets bigger
        /// without moving.
        /// </summary>
        private void PlaceLandmark(Landmark mark, Vector3 origin, float yaw, float scale)
        {
            Quaternion turn = Quaternion.Euler(0f, yaw, 0f);
            foreach (LandmarkPart part in mark.Parts)
            {
                int piece = SceneryPieces.IndexOf(part.Piece);
                if (piece < 0) continue;
                Vector3 local = new Vector3(part.X, part.Y, part.Z) * scale;
                _placements.Add(new Placement
                {
                    Kind = ProceduralKinds + piece,
                    Z = origin.z,
                    Trs = Matrix4x4.TRS(origin + turn * local,
                        turn * Quaternion.Euler(0f, part.Yaw, 0f),
                        Vector3.one * (scale * part.Scale))
                });
            }
        }

        private void Add(int kind, float x, float z, float scale, float yaw, ref uint rng)
        {
            // A slight lean off vertical. Nothing beside a road this old stands straight, and
            // a field of perfectly upright meshes reads as a fence rather than as a place.
            float tiltX = (Next01(ref rng) - 0.5f) * 7f;
            float tiltZ = (Next01(ref rng) - 0.5f) * 7f;
            _placements.Add(new Placement
            {
                Kind = kind,
                Z = z,
                Trs = Matrix4x4.TRS(new Vector3(x, 0f, z),
                    Quaternion.Euler(tiltX, yaw, tiltZ), Vector3.one * scale)
            });
        }

        /// <summary>
        /// One bucket per kind the round actually placed, sized by that kind's zone.
        ///
        /// Allocating for all 129 kinds up front would reserve 1.6 MB of matrices for a round
        /// that uses about thirty of them. This runs at round load, never mid-run, so doc 04's
        /// ban on run-time allocation is respected.
        /// </summary>
        private void AllocateBuckets()
        {
            for (int i = 0; i < _placements.Count; i++)
            {
                int kind = _placements[i].Kind;
                if (_buckets[kind] != null) continue;
                _buckets[kind] = new Matrix4x4[CapFor(_zoneOf[kind])];
            }
        }

        private static int CapFor(int zone) =>
            zone == (int)SceneryZone.Landmark ? LandmarkCap
                : (zone == (int)SceneryZone.Field ? FieldCap : VergeCap);

        private void LateUpdate()
        {
            if (!_dressed || _crowd == null || _propMaterial == null) return;
            // Without instancing this is thousands of draws, so the scenery is dropped rather
            // than allowed to cost the frame. The crowd falls back because the game is
            // unplayable without it; the verge is not.
            if (!_instancingSupported) return;

            float centre = _crowd.CenterZ;
            if (Mathf.Abs(centre - _lastGatherZ) >= RebuildStepMeters)
            {
                _lastGatherZ = centre;
                Gather(centre);
            }

            var bounds = new Bounds(new Vector3(0f, 12f, centre),
                new Vector3(LandmarkOuter * 2.6f, 60f, (WindowAhead + WindowBehind) * 1.1f));

            for (int k = 0; k < _kindCount; k++)
            {
                int n = _counts[k];
                if (n <= 0 || _meshes[k] == null) continue;

                int zone = _zoneOf[k];
                Material material = k < ProceduralKinds || _zoneMaterials == null
                    ? _propMaterial
                    : _zoneMaterials[zone];

                var rp = new RenderParams(material)
                {
                    worldBounds = bounds,
                    // Only landmarks cast. The verge is dense and its shadows fall on ground
                    // the player never looks at, and the shadow pass is exactly where a field
                    // of scenery would start costing milliseconds. A castle earns one because
                    // without it the largest object in the frame is not standing on anything.
                    shadowCastingMode = zone == (int)SceneryZone.Landmark
                        ? UnityEngine.Rendering.ShadowCastingMode.On
                        : UnityEngine.Rendering.ShadowCastingMode.Off,
                    receiveShadows = true
                };
                Graphics.RenderMeshInstanced(rp, _meshes[k], 0, _buckets[k], n);
            }
        }

        /// <summary>Collect what is inside the visible window into per-kind buckets.</summary>
        private void Gather(float centre)
        {
            for (int k = 0; k < _counts.Length; k++) _counts[k] = 0;

            float near = centre - WindowBehind;
            float far = centre + WindowAhead;

            for (int i = 0; i < _placements.Count; i++)
            {
                Placement p = _placements[i];
                if (p.Z < near) continue;
                if (p.Z > far) break;   // sorted by Z, so nothing further can qualify
                Matrix4x4[] bucket = _buckets[p.Kind];
                if (bucket == null) continue;
                int n = _counts[p.Kind];
                if (n >= bucket.Length) continue;
                bucket[n] = p.Trs;
                _counts[p.Kind] = n + 1;
            }
        }

        // A small xorshift rather than UnityEngine.Random: the layout must be identical every
        // time a round is played, and seeding the global generator would disturb everything
        // else that draws from it.
        private static uint Seed(int roundIndex)
        {
            unchecked
            {
                uint h = (uint)roundIndex * 2654435761u + 0x9E3779B9u;
                h ^= h >> 15;
                return h | 1u;
            }
        }

        private static uint NextUInt(ref uint state)
        {
            unchecked
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                return state;
            }
        }

        private static float Next01(ref uint state) =>
            (NextUInt(ref state) & 0xFFFFFFu) / (float)0x1000000;
    }
}
