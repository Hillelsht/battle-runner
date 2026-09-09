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
        /// How close a landmark's OWN EDGE may come to the road centre.
        ///
        /// The old rule clamped the CENTRE to stay outside a 26 m line, so a castle keep —
        /// footprint 11 m at landmark scale — could never stand closer than 37 m, and at that
        /// distance in a world that fogs out at 105 m the largest structures in the game were
        /// silhouettes. The constraint that actually matters is that the building must not
        /// overhang the road, and that is a constraint on its EDGE. The same keep now stands
        /// at 19 m and reads as a building the player is running past.
        /// </summary>
        private const float LandmarkRoadClearance = 8.5f;
        /// <summary>The band is 70 m; a landmark must stand entirely on it, not off the end.</summary>
        private const float LandmarkFarLimit = 64.0f;

        /// <summary>
        /// Fallback settlement spacing, for a world with no scenery palette. Real worlds
        /// author their own and the radius is derived from it.
        /// </summary>
        private const float DefaultSettlementSpacing = 110f;
        /// <summary>A level is ~400 m and settlements sit on both sides, so this is plenty.</summary>
        private const int MaxSettlements = 24;

        /// <summary>
        /// How much harder the field is stepped before the settlement field throws most of it
        /// away. Rejection sampling delivers `step x mean density`, and the mean density over
        /// a level that is about half open country is roughly a half — so without this the
        /// clustered field would come out with 40% fewer pieces than the even scatter it
        /// replaces, which is a thinner world rather than a differently-arranged one.
        /// Simulated across all eight worlds, 2.2 lands the total within 1% of the old count,
        /// so this redistributes the instance budget rather than spending more of it. Over the
        /// same simulation the busiest quarter of the road now holds 42-54% of the field
        /// pieces, against the 25% an even scatter puts there by definition.
        /// </summary>
        private const float FieldOversample = 2.2f;

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

        /// <summary>
        /// Where the hamlets are. Planned once per round before anything is placed, because
        /// both the landmarks and the field pieces have to agree about where the village is —
        /// a castle in one place and the carts and fences in another is the even scatter this
        /// replaces, with an extra step.
        /// </summary>
        private readonly Settlement[] _settlements = new Settlement[MaxSettlements];
        private int _settlementCount;
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
            // VergeStone, not PropStone: three worlds authored a prop stone below 0.17 luma,
            // and both the ten procedural props and the whole imported verge are painted with
            // it. See WorldTheme.MinVergeLuma for the measurement that made this a floor.
            Color stone = ThemePalette.Shifted(theme.VergeStone, hue);
            _propMaterial.SetColorSafe("_BaseColor", stone);
            // The accent, well below the bloom knee. Scenery should catch the world's colour
            // on its edges, not become a light source competing with the gates.
            Color rim = ThemePalette.Shifted(theme.Accent.Scaled(0.22f), hue);
            _propMaterial.SetColorSafe("_EmissionColor", rim);

            SceneryPalette palette = theme.Scenery;
            bool useScenery = _sceneryReady && palette != null && palette.IsComplete;
            if (useScenery) TintZones(palette, stone, rim);

            uint rng = Seed(roundIndex);
            PlanSettlements(useScenery ? palette : null, ref rng, fromZ, toZ);

            for (int side = -1; side <= 1; side += 2)
            {
                // The procedural ten always run, at their original density, so the verge
                // never becomes purely imported.
                float propDensity = Mathf.Clamp(theme.PropDensity * variant.PropDensity, 2f, 40f);
                ScatterProps(theme, ref rng, side, fromZ, toZ, 100f / propDensity);

                if (!useScenery) continue;
                // The verge does NOT cluster. A hedgerow, a fence line and the stones on the
                // shoulder run the length of a road whether or not there is a village there,
                // and clustering the one zone the player passes at arm's length would leave
                // long stretches of bare kerb — the opposite of the problem being fixed.
                ScatterPieces(palette.Verge, SceneryZone.Verge, palette.VergeScale, 0.75f, 1.35f,
                    ref rng, side, fromZ, toZ,
                    100f / Mathf.Max(1f, palette.VergeDensity * variant.PropDensity),
                    VergeInner, VergeOuter, clustered: false);
                // The field is the zone that clusters. Stepped at the SETTLEMENT density and
                // then thinned back out to Settlements.BackgroundDensity in open country, so
                // the total count is close to what it was and the distribution is not.
                ScatterPieces(palette.Field, SceneryZone.Field, palette.FieldScale, 0.70f, 1.45f,
                    ref rng, side, fromZ, toZ,
                    100f / Mathf.Max(1f, palette.FieldDensity * variant.PropDensity * FieldOversample),
                    FieldInner, FieldOuter, clustered: true);
                ScatterLandmarks(palette, ref rng, side);
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

        /// <summary>
        /// Lay out the hamlets for a round, before anything is placed.
        ///
        /// EACH SIDE GETS ITS OWN SEQUENCE. The first version alternated sides down a single
        /// sequence, which reads well on paper — a settlement facing another across the road
        /// is a street — and halves the count per verge. Simulated over a 400 m level it
        /// produced four hamlets in total and left 79% of the road as open country, which is
        /// not "clustered", it is "mostly empty". Each side is now walked separately and a
        /// third of them are PAIRED across the road, so the street still happens on purpose
        /// rather than as the only thing that can happen.
        /// </summary>
        private void PlanSettlements(SceneryPalette palette, ref uint rng, float fromZ, float toZ)
        {
            _settlementCount = 0;
            // The world's authored LandmarkSpacing now spaces the SETTLEMENTS — it used to
            // space landmarks directly, and the landmarks now stand inside these. Floored by
            // Settlements.Spacing so a world that authors a short spacing cannot merge its
            // hamlets back into the even scatter this replaces.
            float spacing = palette != null && palette.LandmarkSpacing > 1f
                ? palette.LandmarkSpacing
                : DefaultSettlementSpacing;
            // The gap ratio is the invariant; the world chooses the scale. See
            // Settlements.RadiusFor — flooring the spacing instead pinned seven of the eight
            // worlds to one number and threw away what each of them authored.
            float baseRadius = Settlements.RadiusFor(spacing);

            for (int side = -1; side <= 1; side += 2)
            {
                float z = fromZ + spacing * (0.2f + Next01(ref rng) * 0.5f);
                while (z < toZ && _settlementCount < MaxSettlements)
                {
                    // Radius varies, but never so much that the gap the spacing bought is eaten.
                    float radius = baseRadius * Mathf.Lerp(0.80f, 1.12f, Next01(ref rng));
                    // Reaching in toward the road matters more than reaching out: a village
                    // the player runs THROUGH is worth several the player runs past.
                    float x = Mathf.Lerp(FieldInner + 4f, FieldOuter - 6f, Next01(ref rng) * 0.7f);
                    float yaw = Next01(ref rng) * 360f;
                    _settlements[_settlementCount++] = new Settlement(z, x, side, radius, yaw);

                    // A third of them face a twin across the road. Both halves share the
                    // street's orientation, which is what makes it read as one place with a
                    // road through it rather than as two villages that happened to collide.
                    if (Next01(ref rng) < 0.34f && _settlementCount < MaxSettlements)
                    {
                        float twinX = Mathf.Lerp(FieldInner + 4f, FieldOuter - 6f,
                            Next01(ref rng) * 0.5f);
                        _settlements[_settlementCount++] = new Settlement(
                            z + (Next01(ref rng) - 0.5f) * radius * 0.5f, twinX, -side,
                            radius * 0.85f, yaw);
                    }

                    z += spacing * Mathf.Lerp(0.85f, 1.30f, Next01(ref rng));
                }
            }
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
            float step, float inner, float outer, bool clustered)
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
                    float x = side * Mathf.Lerp(inner, outer, t);

                    // Rejection sampling against the settlement field. Stepping at the
                    // settlement density and throwing most of it away out in open country is
                    // both simpler and better distributed than trying to walk a variable
                    // step: a variable step cannot produce two buildings a metre apart, and
                    // two buildings a metre apart is what a village IS.
                    bool keep = true;
                    if (clustered)
                        keep = Next01(ref rng)
                               < Settlements.DensityAt(_settlements, _settlementCount, x, z);

                    if (keep)
                    {
                        // Inside a settlement the yaw comes from the SETTLEMENT, jittered.
                        // Buildings on a street share an orientation; individually random
                        // yaws are the other half of why a cluster still reads as scatter.
                        float yaw = Next01(ref rng) * 360f;
                        if (clustered)
                        {
                            int at = NearestSettlement(x, z);
                            if (at >= 0)
                                yaw = _settlements[at].Yaw + (Next01(ref rng) - 0.5f) * 40f;
                        }
                        Add(ProceduralKinds + piece, x, z,
                            baseScale * Mathf.Lerp(minJitter, maxJitter, Next01(ref rng)),
                            yaw, ref rng);
                    }
                }
                z += step * Mathf.Lerp(0.55f, 1.55f, Next01(ref rng));
            }
        }

        /// <summary>The settlement a point actually belongs to, or -1 for open country.</summary>
        private int NearestSettlement(float x, float z)
        {
            int best = -1;
            float bestWeight = 0.05f;
            for (int i = 0; i < _settlementCount; i++)
            {
                float w = _settlements[i].Weight(x, z);
                if (w > bestWeight) { bestWeight = w; best = i; }
            }
            return best;
        }

        /// <summary>
        /// Landmarks stand IN the settlements, not on a spacing of their own.
        ///
        /// They used to walk the level on `LandmarkSpacing` while the field pieces walked it
        /// on theirs, which meant the castle and the carts and fences around it agreed about
        /// nothing. A structure with a hamlet gathered around it is a place; the same
        /// structure alone in an evenly-dressed field is a prop that happens to be large.
        /// </summary>
        private void ScatterLandmarks(SceneryPalette palette, ref uint rng, int side)
        {
            if (palette.Landmarks == null || palette.Landmarks.Length == 0) return;
            float scale = palette.LandmarkScale;

            for (int i = 0; i < _settlementCount; i++)
            {
                Settlement town = _settlements[i];
                if (town.Side != side) continue;

                // One anchor structure, and sometimes a second outbuilding. Three castles in
                // one hamlet is a skyline, not a village.
                int count = Next01(ref rng) < 0.42f ? 2 : 1;
                for (int n = 0; n < count; n++)
                {
                    Landmark mark = Landmarks.ByName(
                        palette.Landmarks[(int)(NextUInt(ref rng) % (uint)palette.Landmarks.Length)]);
                    if (mark == null) continue;

                    float reach = mark.Radius * scale;
                    // Offset within the settlement, then clamped so the EDGE clears the road
                    // and stays on the land. Clamping the edge rather than the centre is what
                    // brings the big structures in: the old rule kept a castle's centre
                    // outside 26 m, which put its walls at 37 m and its silhouette in the fog.
                    float spread = Mathf.Max(0f, town.Radius * 0.55f - reach);
                    float x = town.X + (Next01(ref rng) - 0.5f) * 2f * spread;
                    x = Mathf.Clamp(x, LandmarkRoadClearance + reach,
                        Mathf.Max(LandmarkRoadClearance + reach, LandmarkFarLimit - reach));
                    float z = town.Z + (Next01(ref rng) - 0.5f) * town.Radius * 1.1f;

                    // Facing the road, jittered around the settlement's own orientation so
                    // the buildings agree with each other as well as with the street.
                    float toRoad = side < 0 ? 90f : 270f;
                    float yaw = Mathf.LerpAngle(toRoad, town.Yaw, 0.35f)
                                + (Next01(ref rng) - 0.5f) * 35f;
                    PlaceLandmark(mark, new Vector3(side * x, 0f, z), yaw, scale);
                }
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

            // Wide enough to contain the far edge of the land on both sides plus the reach
            // of the largest landmark standing on it. An instanced draw whose bounds do not
            // contain its instances is culled as a whole, so this errs outward on purpose.
            var bounds = new Bounds(new Vector3(0f, 12f, centre),
                new Vector3(LandmarkFarLimit * 2.6f, 60f, (WindowAhead + WindowBehind) * 1.1f));

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
