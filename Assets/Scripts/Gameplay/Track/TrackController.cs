using System;
using System.Collections.Generic;
using BattleRunner.Core.Crowd;
using BattleRunner.Core.Progression;
using BattleRunner.Core.Run;
using BattleRunner.Data.Definitions;
using BattleRunner.Gameplay.Crowd;
using UnityEngine;

namespace BattleRunner.Gameplay.Track
{
    /// <summary>
    /// Lays out a finite level from pooled chunk content and resolves gate/enemy/finish
    /// crossings against the crowd centroid — arithmetic in lane space, no physics (R3).
    /// </summary>
    public sealed class TrackController : MonoBehaviour
    {
        // Both carry the WORLD POSITION of the thing that resolved. The run loop needs it
        // to put a shockwave where the gate was rather than where the crowd is: at 10 m/s
        // the two are metres apart by the time the event is handled, and an effect that
        // does not land on its cause reads as an unrelated flash.
        public event Action<GateOp, int, Vector3> GateApplied;
        public event Action<int, Vector3> EnemyContact;
        public event Action FinishReached;

        private ObjectPool<GateBehaviour> _gatePool;
        private ObjectPool<EnemyPackBehaviour> _enemyPool;
        private readonly List<GateBehaviour> _activeGates = new List<GateBehaviour>();
        private readonly List<EnemyPackBehaviour> _activeEnemies = new List<EnemyPackBehaviour>();
        private Material _enemyMaterial;
        private SquadRenderer _squadRenderer;

        /// <summary>
        /// The instanced squad renderer, so the boss encounter can borrow its fighter bucket.
        ///
        /// A boss round has no enemy squads on the level, so that array of 512 matrices sits
        /// idle for the whole encounter — filling it with the skirmish line is what makes a
        /// line of thirty men fighting a boss cost zero additional draw calls.
        /// </summary>
        public SquadRenderer Squads => _squadRenderer;
        private Transform _cameraTransform;

        /// <summary>
        /// How far in front of the army's leading plane a squad is held while it is being
        /// fought. The road is still moving at 10 m/s under both of them, so without this the
        /// army would simply walk through the squad it is supposedly fighting.
        /// </summary>
        private const float EngageGap = 2.6f;

        /// <summary>
        /// Where to point the headcounts. Resolved lazily because the runtime camera is built
        /// by GameBootstrap after this component exists, and cached because Camera.main is a
        /// tagged-object search.
        /// </summary>
        private Vector3 CameraPosition
        {
            get
            {
                if (_cameraTransform == null)
                {
                    UnityEngine.Camera main = UnityEngine.Camera.main;
                    if (main != null) _cameraTransform = main.transform;
                }
                return _cameraTransform != null
                    ? _cameraTransform.position
                    : new Vector3(0f, 6f, -12f);
            }
        }

        /// <summary>
        /// Squads are drawn instanced, and the renderer needs the crowd to know where the
        /// army's front rank is. Called once, from GameBootstrap, after both exist.
        /// </summary>
        public void AttachSquadRenderer(BattleRunner.Gameplay.Crowd.CrowdController crowd,
            Material allyMaterial)
        {
            if (_squadRenderer == null)
                _squadRenderer = gameObject.AddComponent<SquadRenderer>();
            _squadRenderer.Initialize(crowd, _enemyMaterial, allyMaterial, _activeEnemies, _activeGates);
        }
        private readonly List<GameObject> _groundStrips = new List<GameObject>();

        /// <summary>Gates beyond this hide their label; 45 m chunks put the next decision well inside it.</summary>
        private const float LabelVisibleMeters = 34f;

        /// <summary>Extra clearance past the camera before a passed gate is recycled.</summary>
        private const float DespawnMarginMeters = 4f;

        private Transform _trackRoot;
        private float _laneWidth = 2.2f;
        private float _finishZ;
        private bool _finishRaised;
        private Material _groundMaterial;
        private Material _finishMaterial;
        private Material _markingMaterial;
        private Material _railMaterial;
        private Material _terrainMaterial;

        public float FinishZ => _finishZ;

        /// <summary>
        /// The cobbled road, or the old flat slab if it cannot render.
        ///
        /// Same rule as the sky: the material lives in Resources so the shader is never
        /// stripped from the Android build, and the pipeline is checked explicitly because
        /// Shader.isSupported reports whether a shader COMPILED, not whether any of its
        /// SubShaders match the active pipeline — a URP shader in a Built-in player passes
        /// that check and renders magenta.
        /// </summary>
        private static Material LoadRoadMaterial(Material fallbackBase)
        {
            if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null
                && !ShaderSafety.UsingFallback)
            {
                var road = Resources.Load<Material>("Road");
                // INSTANCED, not the asset. Every world retints the road, and writing those
                // tints into the loaded asset would edit Resources/Road.mat on disk each time
                // the editor played a round.
                if (road != null && road.shader != null && road.shader.isSupported)
                    return new Material(road);
            }

            Debug.LogWarning("[Track] Road material unavailable — falling back to a flat slab.");
            Material flat = ShaderSafety.CreateMaterial(fallbackBase);
            flat.SetColorSafe("_BaseColor", new Color(0.10f, 0.09f, 0.12f));
            flat.SetColorSafe("_EmissionColor", Color.black);
            flat.SetFloatSafe("_BobAmount", 0f); // static geometry must not run-bob
            return flat;
        }

        /// <summary>
        /// The land either side of the road. Same instancing discipline as the road: the
        /// asset is a template and every world writes into a COPY, or playing a round in the
        /// editor would rewrite Resources/Terrain.mat on disk.
        ///
        /// The fallback is deliberately not the crowd shader with a flat colour. If the
        /// terrain shader is unavailable the band would be 65 m of unbroken single-tone
        /// polygon filling the lower half of the frame, which is worse than the void it
        /// replaced — so on failure there is simply no band, and the game looks exactly as it
        /// did before this existed.
        /// </summary>
        private static Material LoadTerrainMaterial()
        {
            if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null
                || ShaderSafety.UsingFallback)
                return null;

            var terrain = Resources.Load<Material>("Terrain");
            if (terrain != null && terrain.shader != null && terrain.shader.isSupported)
                return new Material(terrain);

            Debug.LogWarning("[Track] Terrain material unavailable — the verge stays empty.");
            return null;
        }

        public void Initialize(Material baseMaterial, Material enemyMaterial, Mesh unitMesh, Font font, float laneWidth)
        {
            _laneWidth = laneWidth;
            _trackRoot = new GameObject("TrackRoot").transform;
            _trackRoot.SetParent(transform, false);

            var poolRoot = new GameObject("TrackPools").transform;
            poolRoot.SetParent(transform, false);

            _enemyMaterial = enemyMaterial;
            GateBehaviour.SetSharedMaterials(baseMaterial);
            _gatePool = new ObjectPool<GateBehaviour>(() => GateBehaviour.Build(font), poolRoot);
            _enemyPool = new ObjectPool<EnemyPackBehaviour>(
                () => EnemyPackBehaviour.Build(unitMesh, enemyMaterial, font), poolRoot);
            // Prewarmed for the WORST round the generator can produce, not the average one.
            // Doc 04 bans mid-run instantiation and ObjectPool.Get silently CREATES on an
            // empty pool, so the size is derived from the two constants that bound it rather
            // than from a number that would quietly go stale the next time a shape is added.
            _gatePool.Prewarm(RoundPlan.MaxChunkCount * ChunkLayouts.MaxGatesPerChunk);
            _enemyPool.Prewarm(RoundPlan.MaxChunkCount * ChunkLayouts.MaxPacksPerChunk);

            _groundMaterial = LoadRoadMaterial(baseMaterial);
            _terrainMaterial = LoadTerrainMaterial();

            // Same unrestrained rim the gates had. The finish line is a full-width slab
            // whose only visible face points straight up, at ~80 degrees off the view axis
            // where the shader's DEFAULT lobe reads 0.64 — so it ran at near-full emission
            // over a colour already above white, and during the boss fight (which happens
            // past it) it filled the bottom of the frame with saturated gold.
            _finishMaterial = ShaderSafety.CreateMaterial(baseMaterial);
            _finishMaterial.SetColorSafe("_BaseColor", new Color(0.9f, 0.65f, 0.2f) * 0.5f);
            _finishMaterial.SetColorSafe("_EmissionColor", new Color(1.2f, 0.85f, 0.25f));
            _finishMaterial.SetFloatSafe("_RimPower", 4f);
            _finishMaterial.SetFloatSafe("_RimStrength", 0.30f);
            _finishMaterial.SetFloatSafe("_EmissionFlat", 0.12f);
            _finishMaterial.SetFloatSafe("_RimUpMask", 1f);
            _finishMaterial.SetFloatSafe("_BobAmount", 0f);

            // Lane lines and speed rungs are 2 cm decals whose only visible face points
            // straight up — which puts them at ~80 degrees off the view axis, where the
            // shader's wide DEFAULT rim lobe reads 0.64, not 0. They were 86% pure
            // emission at a blue/red ratio of 3.5, and they cover the road in a dense
            // grid, so the lavender cast the road was blamed for was substantially these
            // markings painted over it. Neutral hue, tighter lobe, and the flat term cut.
            _markingMaterial = ShaderSafety.CreateMaterial(baseMaterial);
            // The hue fix was right, the level was not: at 0.34 emission and a 0.08 flat
            // term these landed at ~0.9x the road's own luminance, i.e. DARKER than the
            // stone they are painted on, and on device the lane lines all but vanished on
            // the dark early stretch of a level. In a three-lane game the lane read is not
            // decoration. Back up to ~1.5x the road, still neutral in hue.
            _markingMaterial.SetColorSafe("_BaseColor", new Color(0.17f, 0.17f, 0.18f));
            _markingMaterial.SetColorSafe("_EmissionColor", new Color(0.46f, 0.45f, 0.44f));
            _markingMaterial.SetFloatSafe("_RimPower", 4f);
            _markingMaterial.SetFloatSafe("_RimStrength", 0.35f);
            _markingMaterial.SetFloatSafe("_EmissionFlat", 0.10f);
            // _RimUpMask. The comment on the crowd material in GameBootstrap says the road
            // markings "keep the wide default because their top face IS their only lit
            // surface" — and that is exactly backwards. The rim term is strongest at GRAZING
            // angles, and a 2 cm decal on the ground is seen at nothing but grazing angles:
            // its up-facing normal sits ~80 degrees off the view axis, where even the tight
            // power-4 lobe reads ~0.64. So the markings ran at near-full rim down the entire
            // length of the road. The mask kills exactly that term on up-facing normals and
            // leaves _EmissionFlat, which is view-independent, to carry the lane read.
            _markingMaterial.SetFloatSafe("_RimUpMask", 1f);
            _markingMaterial.SetFloatSafe("_BobAmount", 0f);

            // The rails were reading as lit blue plastic rather than as stone kerbs.
            // They are ~83% pure emission: the shader's flat term applies to every pixel
            // regardless of angle, and the wide default rim lobe floods their grazing side
            // faces on top of it. Now bloom is on, that all became light.
            //
            // The emission is cut and the flat term nearly removed, but NOT to zero — the
            // rails are the player's peripheral cue for where the road ends, so they have
            // to stay visible. A tight rim keeps a bright edge on the silhouette while the
            // faces go dark, which is what a stone kerb catching moonlight looks like.
            _railMaterial = ShaderSafety.CreateMaterial(baseMaterial);
            // The tight rim was right; the strength and the hue were not. A rail's large
            // visible face is its INNER side, whose normal is perpendicular to the view
            // axis — so even at _RimPower 5 the term reads 0.52 at 30 m and 0.79 at 80 m.
            // Multiplied by 0.7 against an emission at a blue/red ratio of 4.0, the rails
            // were a self-lit periwinkle bar running the length of the frame, and they,
            // not the road, were the lavender in the device screenshots.
            _railMaterial.SetColorSafe("_BaseColor", new Color(0.30f, 0.29f, 0.28f));
            _railMaterial.SetColorSafe("_EmissionColor", new Color(0.34f, 0.34f, 0.38f));
            _railMaterial.SetFloatSafe("_RimPower", 5f);
            _railMaterial.SetFloatSafe("_RimStrength", 0.25f);
            _railMaterial.SetFloatSafe("_EmissionFlat", 0.02f);
            // A rail is read by its INNER side, whose normal is horizontal, so the mask (which
            // only touches normals pointing at the sky) leaves the silhouette cue untouched and
            // costs the rail only its top strip.
            _railMaterial.SetFloatSafe("_RimUpMask", 1f);
            _railMaterial.SetFloatSafe("_BobAmount", 0f);
        }

        /// <summary>
        /// How far the ground runs past the finish line.
        ///
        /// It was 180, chosen to clear a fog end fixed at 170. Worlds now choose their own
        /// weather and the widest-open of them fogs out at WorldThemes.MaxFogEnd, so the road
        /// has to outrun THAT instead. 200 m past the finish is ~210 m from a camera sitting
        /// 10 m behind the crowd, which is still inside the 220 m far clip — so the seam is
        /// buried in fog rather than sliced by the clip plane.
        /// </summary>
        private const float GroundOverrunMeters = 200f;

        /// <summary>
        /// How far out the land goes. 70 m is chosen against two numbers that already exist:
        /// the scenery's far band reaches 26 m and the camera's far clip is 220 m, so the
        /// band has to outrun everything standing on it while still ending well inside fog.
        /// At WorldThemes.MaxFogEnd = 185 the far edge is solid fog colour, which is what
        /// turns a finite box into a horizon.
        /// </summary>
        private const float TerrainHalfWidth = 70f;

        /// <summary>
        /// Textures loaded once and shared. Resources.Load is a dictionary lookup after the
        /// first call, but ApplyTheme runs on every round transition and there is no reason
        /// to ask sixteen times for something that never changes.
        /// </summary>
        private static readonly System.Collections.Generic.Dictionary<string, Texture2D> SurfaceCache =
            new System.Collections.Generic.Dictionary<string, Texture2D>();

        /// <summary>
        /// Bind one generated surface to a material.
        ///
        /// A texture that fails to load is LEFT UNBOUND rather than bound to null, because
        /// SetTexture(name, null) makes the sampler read Unity's black default while an
        /// unbound property reads the "gray" and "bump" the shader's Properties block asks
        /// for. The difference is a road that degrades to the flat slab it used to be versus
        /// one that degrades to a black hole in the middle of the frame.
        /// </summary>
        private static void BindSurface(Material material,
            BattleRunner.Core.World.RoadSurface surface, float repeatsPerMetre)
        {
            if (material == null || surface == null) return;
            material.SetTextureSafe("_Surface", LoadSurface(surface.MaskResource));
            material.SetTextureSafe("_SurfaceNormal", LoadSurface(surface.NormalResource));
            material.SetFloatSafe("_SurfaceTiling", repeatsPerMetre);
            material.SetFloatSafe("_NormalStrength", surface.NormalStrength);
            material.SetFloatSafe("_Cavity", surface.Cavity);
        }

        private static Texture2D LoadSurface(string resource)
        {
            if (SurfaceCache.TryGetValue(resource, out Texture2D cached)) return cached;
            var texture = Resources.Load<Texture2D>(resource);
            if (texture == null)
                Debug.LogWarning($"[Track] Surface texture '{resource}' is missing — the "
                                 + "shader falls back to a flat surface. "
                                 + "Run: python3 tooling/gen_surfaces.py");
            SurfaceCache[resource] = texture;
            return texture;
        }

        /// <summary>
        /// Repaint the road, its kerbs and its markings for a world.
        ///
        /// The four track materials were built once in Initialize from constants and never
        /// touched again, which is most of why every level looked the same. They are still
        /// built there — the shapes and the rim tuning are hard-won and world-independent —
        /// but their colours now come from the theme.
        ///
        /// Call before BuildLevel: the statics take the material by reference, so a retint
        /// after the strip is spawned is fine too, but doing it first keeps the first frame
        /// of a round correct.
        /// </summary>
        public void ApplyTheme(BattleRunner.Core.World.WorldTheme theme,
            BattleRunner.Core.Progression.ThemeVariant variant)
        {
            if (theme == null) return;
            float hue = variant.HueShift;

            if (_groundMaterial != null)
            {
                _groundMaterial.SetColorSafe("_BaseColor", ThemePalette.Shifted(theme.RoadStone, hue));
                _groundMaterial.SetColorSafe("_MortarColor", ThemePalette.Shifted(theme.RoadMortar, hue));
                _groundMaterial.SetColorSafe("_DampColor", ThemePalette.Shifted(theme.RoadDamp, hue));
                // The surface texture, per world, with NO shader keyword: Road.shader is
                // already 128 forward variants from its fog and shadow multi_compiles, and a
                // six-way surface keyword would take it to 768 — every one of which has to
                // compile on the device. Eight worlds bind eight different TEXTURES into one
                // variant instead, which costs nothing.
                BindSurface(_groundMaterial, theme.Surface,
                    theme.Surface.TileRepeatsPerMetre(theme.RoadTiling));
                _groundMaterial.SetFloatSafe("_MortarWidth", theme.RoadMortarWidth);
                _groundMaterial.SetFloatSafe("_StoneVariation", theme.RoadStoneVariation);
                _groundMaterial.SetFloatSafe("_GrimeContrast",
                    Mathf.Clamp(theme.RoadGrimeContrast, 0f, 0.6f));
                // Wetness is the cheapest thing on the road that reads as weather, so the
                // per-round variation spends here as well as on the fog.
                _groundMaterial.SetFloatSafe("_Wetness",
                    Mathf.Clamp01(theme.RoadWetness + variant.WetnessShift));
                _groundMaterial.SetFloatSafe("_Gloss", Mathf.Max(1f, theme.RoadGloss));
            }

            if (_terrainMaterial != null)
            {
                _terrainMaterial.SetColorSafe("_GroundColor", ThemePalette.Shifted(theme.Ground, hue));
                _terrainMaterial.SetColorSafe("_GroundColorAlt", ThemePalette.Shifted(theme.GroundAlt, hue));
                _terrainMaterial.SetColorSafe("_SheenColor", ThemePalette.Shifted(theme.GroundSheen, hue));
                _terrainMaterial.SetFloatSafe("_PatchScale", Mathf.Max(0.005f, theme.GroundPatchScale));
                // A DIFFERENT surface from the road's. A plank boardwalk over mud and a mosaic
                // floor over dust are both worlds this game has; sharing one texture across the
                // kerb would turn the verge into pavement, and the kerb is the one edge in the
                // frame the eye is guaranteed to find.
                BindSurface(_terrainMaterial, theme.GroundSurface,
                    Mathf.Clamp(theme.GroundSurfaceTiling, 0.01f, 4f));
                _terrainMaterial.SetFloatSafe("_Speckle", Mathf.Clamp01(theme.GroundSpeckle));
                // Wetness spends on the land as well as the road, so a round that reads as
                // rain reads that way all the way out to the treeline.
                _terrainMaterial.SetFloatSafe("_Sheen",
                    Mathf.Clamp01(theme.GroundSheenStrength + variant.WetnessShift * 0.5f));
                // Terrain.shader has always had a _Gloss and nothing ever wrote it, so ice
                // and dry ash caught the key light identically in all eight worlds.
                _terrainMaterial.SetFloatSafe("_Gloss", Mathf.Max(1f, theme.GroundGloss));
            }

            if (_railMaterial != null)
            {
                _railMaterial.SetColorSafe("_BaseColor", ThemePalette.Shifted(theme.RailBase, hue));
                _railMaterial.SetColorSafe("_EmissionColor", ThemePalette.Shifted(theme.RailEmission, hue));
            }

            if (_markingMaterial != null)
            {
                _markingMaterial.SetColorSafe("_BaseColor", ThemePalette.Shifted(theme.MarkingBase, hue));
                _markingMaterial.SetColorSafe("_EmissionColor",
                    ThemePalette.Shifted(theme.MarkingEmission, hue));
            }
        }

        public void BuildLevel(System.Collections.Generic.IReadOnlyList<ChunkLayout> layouts)
        {
            ClearLevel();
            _finishRaised = false;

            float z = 12f; // breathing room before the first chunk
            if (layouts != null)
            {
                foreach (ChunkLayout layout in layouts)
                {
                    if (layout == null) continue;
                    SpawnLayout(layout, z);
                    z += ChunkLayouts.ChunkMeters;
                }
            }

            _finishZ = z + 8f;
            // +180, not +40. At +40 the world simply STOPPED about 62 m in front of the
            // camera by the end of a level: a hard horizontal seam across the road with
            // black void past it, both rails cut off in mid air, visible in every device
            // screenshot of a late run. 180 m puts the seam past the fog wall
            // (EnvironmentLook fogEndDistance 170) at every camera position, so the road
            // walks into haze instead of ending. It costs nothing — the ground, the four
            // lane lines and the two rails are one 24-vertex box each at ANY length.
            SpawnGroundStrip(-6f, _finishZ + GroundOverrunMeters);
            SpawnFinishLine(_finishZ);
        }

        /// <summary>
        /// One chunk, from a layout Core generated for this round.
        ///
        /// The road used to be built from ScriptableObject chunks baked once at boot, six
        /// levels' worth, cycled forever — which is why round eight replayed round two's
        /// gates. Layouts now come from ChunkLayouts per round, so a round's shape is part of
        /// its identity rather than an index into a fixed list.
        /// </summary>
        private void SpawnLayout(ChunkLayout layout, float startZ)
        {
            foreach (PlannedGate spec in layout.Gates)
            {
                GateBehaviour gate = _gatePool.Get(_trackRoot);
                gate.Setup(spec.Op, spec.Value, spec.Lane,
                    new Vector3(spec.Lane * _laneWidth, 0f, startZ + spec.Position));
                _activeGates.Add(gate);
            }

            foreach (PlannedPack spec in layout.Packs)
            {
                EnemyPackBehaviour pack = _enemyPool.Get(_trackRoot);
                pack.Setup(spec.ForceCost, spec.Lane,
                    new Vector3(spec.Lane * _laneWidth, 0f, startZ + spec.Position));
                _activeEnemies.Add(pack);
            }
        }

        private void SpawnGroundStrip(float fromZ, float toZ)
        {
            float length = toZ - fromZ;
            float midZ = fromZ + length * 0.5f;

            // Every dimension below comes off the lane pitch, so the road the player reads
            // is exactly the partition CrowdMath.LaneIndex scores against. The old version
            // used loose multiples of lane width (ground 4.8w, rails 2.3w) and drew lane
            // lines only at +/-0.5w, which left the outer lanes bounded by the rails at
            // 2.3w: the centre lane rendered 2.20 m and the outer two 3.96 m each.
            float roadHalf = CrowdMath.RoadHalfWidth(_laneWidth);   // 3.30 at a 2.2 m lane
            float shoulder = _laneWidth * 0.14f;                    // visible verge
            float railHalfThickness = 0.15f;
            float railCentre = roadHalf + shoulder + railHalfThickness;
            float groundHalf = railCentre + railHalfThickness + 0.25f;

            SpawnStatic("Ground", new Vector3(0f, -0.1f, midZ),
                new Vector3(groundHalf * 2f, 0.2f, length), _groundMaterial);

            // THE LAND. One box per side, from the edge of the road out to TerrainHalfWidth.
            // Two draw calls and 48 vertices for the entire world beside the road, which is
            // the cheapest large thing in the game by a wide margin.
            //
            // Its top sits 3 cm BELOW the road so the road edge reads as a kerb rather than
            // as two coplanar surfaces z-fighting along 400 m — and the rails at +/-3.758
            // stand over the join anyway, so the step is never actually visible.
            //
            // It receives shadows (SpawnStatic leaves the renderer default) and casts none:
            // it is flat, so its own shadow would be a no-op, and it is the only surface
            // large enough to show the rails' shadows falling across it.
            if (_terrainMaterial != null)
            {
                float innerEdge = groundHalf;
                float bandWidth = TerrainHalfWidth - innerEdge;
                float bandCentre = innerEdge + bandWidth * 0.5f;
                for (int side = -1; side <= 1; side += 2)
                {
                    SpawnStatic("Terrain", new Vector3(side * bandCentre, -0.13f, midZ),
                        new Vector3(bandWidth, 0.2f, length), _terrainMaterial);
                }
            }

            // All FOUR lane edges, so each of the three lanes is bounded by a real line and
            // they read as equal. Without the outer pair the road has no visible edge and
            // the eye takes the rails as the boundary instead.
            for (int edge = -1; edge <= 1; edge += 2)
            {
                SpawnStatic("LaneLineInner", new Vector3(edge * _laneWidth * 0.5f, 0.005f, midZ),
                    new Vector3(0.07f, 0.02f, length), _markingMaterial);
                SpawnStatic("LaneLineOuter", new Vector3(edge * roadHalf, 0.005f, midZ),
                    new Vector3(0.07f, 0.02f, length), _markingMaterial);

                SpawnStatic("Rail", new Vector3(edge * railCentre, 0.45f, midZ),
                    new Vector3(railHalfThickness * 2f, 0.9f, length), _railMaterial,
                    castsShadow: true);
            }

            // THE SPEED RUNGS ARE GONE. There were 101-176 of them, one every 6 m across the
            // full 6.6 m road, on the same emissive marking material — which measured at
            // 1.6-2.2x the road's own luminance on device. Together with the four lane lines
            // that IS the grid the road kept reading as: a bright regular lattice laid over
            // whatever stone the world had chosen, flattening its range from the 178 the
            // texture actually carries down to a measured 21 on screen. Whatever a surface
            // does, it cannot compete with a brighter thing drawn on top of it every 6 m.
            //
            // They were also the largest remaining draw-call item in SpawnGroundStrip by an
            // order of magnitude (everything else here is one stretched box).
            //
            // Their job was a speed cue. That belongs to the scenery streaming past and to
            // the surface's own detail, not to a lattice bright enough to erase the surface.
        }

        /// <summary>
        /// castsShadow is off by default because most of this is flat road decal — lane
        /// lines and rungs are 2 cm tall and their shadows would be noise on the surface
        /// they are painted on. The rails have real height and earn one.
        /// </summary>
        private void SpawnStatic(string name, Vector3 position, Vector3 size, Material material,
            bool castsShadow = false)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(_trackRoot, false);
            go.transform.position = position;
            go.GetComponent<MeshFilter>().sharedMesh = ProceduralMeshes.BuildBox(Vector3.zero, size);
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = castsShadow
                ? UnityEngine.Rendering.ShadowCastingMode.On
                : UnityEngine.Rendering.ShadowCastingMode.Off;
            _groundStrips.Add(go);
        }

        private void SpawnFinishLine(float z)
        {
            var finish = new GameObject("FinishLine", typeof(MeshFilter), typeof(MeshRenderer));
            finish.transform.SetParent(_trackRoot, false);
            finish.transform.position = new Vector3(0f, 0.05f, z);
            finish.GetComponent<MeshFilter>().sharedMesh =
                ProceduralMeshes.BuildBox(Vector3.zero, new Vector3(_laneWidth * 3.4f, 0.1f, 1.2f));
            var renderer = finish.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = _finishMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _groundStrips.Add(finish);
        }

        public void ClearLevel()
        {
            foreach (GateBehaviour gate in _activeGates) _gatePool.Release(gate);
            _activeGates.Clear();
            foreach (EnemyPackBehaviour pack in _activeEnemies) _enemyPool.Release(pack);
            _activeEnemies.Clear();
            foreach (GameObject strip in _groundStrips) Destroy(strip);
            _groundStrips.Clear();
        }

        /// <summary>
        /// Crossing checks against the crowd's leading plane. Called once per frame during RunnerLoop.
        ///
        /// Resolution and despawn are deliberately separate events. They used to be the same
        /// one, so a gate in another lane correctly did not score but also POPPED out of
        /// existence level with the player instead of sliding past them. A gate now scores
        /// (or does not) at the crowd's plane and keeps drawing until it is behind the camera.
        /// </summary>
        /// <param name="magnetism">
        /// Extra metres of reach toward a beneficial gate in a NEIGHBOURING lane, from the
        /// Zealot tree. Zero restores the exact nearest-lane rule; it never widens the reach
        /// of an enemy pack or a subtract gate, because a talent that makes the player worse
        /// at dodging is a bug however it is worded.
        /// </param>
        public void Tick(CrowdController crowd, float magnetism = 0f)
        {
            // Resolve where the player can SEE the crowd touching things, not at an
            // arbitrary offset from the centroid.
            float frontZ = crowd.FrontZ;
            Vector3 camera = CameraPosition;
            int crowdLane = CrowdMath.LaneIndex(crowd.CenterX, _laneWidth);
            float despawnZ = TrackVisibility.DespawnPlane(
                crowd.CenterZ, CameraRig.SetbackMeters, DespawnMarginMeters);

            for (int i = _activeGates.Count - 1; i >= 0; i--)
            {
                GateBehaviour gate = _activeGates[i];
                float z = gate.transform.position.z;

                // Resolved is a latch, and it has to be: FrontZ is derived from the crowd's
                // envelope, which SHRINKS when force drops, so a big subtract gate can pull
                // the leading plane backwards up to 1.8 m in one frame against an anchor that
                // advances 0.167 m. Without the latch a spent gate slides back in front of the
                // plane and its label pops on again.
                if (!gate.Resolved && z > frontZ)
                {
                    // Every gate in the level exists from BuildLevel onward and its label
                    // draws through all geometry, so without this the far ones stack into
                    // an unreadable pile on the horizon.
                    gate.SetLabelVisible(z - frontZ <= LabelVisibleMeters);
                    continue;
                }

                if (!gate.Resolved && z <= frontZ)
                {
                    gate.Resolve();
                    // The label reads through everything, so a passed gate would otherwise
                    // paint its number over the crowd on the way by.
                    gate.SetLabelVisible(false);
                    bool reaches = gate.Lane == crowdLane
                        || (Talents.IsBeneficial(gate.Op, gate.Value)
                            && CrowdMath.LaneReaches(crowd.CenterX, gate.Lane, _laneWidth, magnetism));
                    if (reaches)
                    {
                        gate.Consume();
                        GateApplied?.Invoke(gate.Op, gate.Value, gate.transform.position);
                    }
                }

                // EVERY gate ticks, resolved or not: the billboard has to keep facing the
                // camera on the way past, and an add gate's crowd runs into the army AFTER
                // it is consumed — ticking only the unresolved ones would freeze the join
                // animation on the exact frame it is supposed to start.
                gate.Tick(Time.deltaTime, camera);

                if (z < despawnZ)
                {
                    _activeGates.RemoveAt(i);
                    _gatePool.Release(gate);
                }
            }

            for (int i = _activeEnemies.Count - 1; i >= 0; i--)
            {
                EnemyPackBehaviour pack = _activeEnemies[i];
                float z = pack.transform.position.z;

                if (!pack.Resolved && z > frontZ)
                {
                    pack.SetLabelVisible(z - frontZ <= LabelVisibleMeters);
                    pack.FaceCamera(camera);
                    continue;
                }

                if (!pack.Resolved && z <= frontZ)
                {
                    pack.Resolve();
                    if (pack.Lane == crowdLane)
                    {
                        // THE CLASH STARTS HERE AND TAKES A SECOND. The force is still
                        // subtracted in full on this frame — every tuned difficulty number in
                        // the game depends on that — but the squad now stands and fights while
                        // its count drains, instead of being deleted on the frame it is
                        // touched. See Core/Run/Melee and SquadRenderer.
                        pack.BeginFight(crowd.ForceCount);
                        EnemyContact?.Invoke(pack.ForceCost, pack.transform.position);
                    }
                    else
                    {
                        // Dodged. It keeps its count and slides past, like a gate does.
                        pack.SetLabelVisible(false);
                    }
                }

                // A squad mid-clash is pinned to the army's front so the two lines stay in
                // contact while they fight; the road is still moving under both of them.
                if (pack.Fighting)
                {
                    pack.TickFight(Time.deltaTime);
                    Vector3 held = pack.transform.position;
                    held.z = frontZ + EngageGap;
                    pack.transform.position = held;
                }

                pack.FaceCamera(camera);

                // A squad the crowd fought goes once the clash is over, not on contact.
                if ((pack.Defeated && !pack.Fighting) || z < despawnZ)
                {
                    _activeEnemies.RemoveAt(i);
                    _enemyPool.Release(pack);
                }
            }

            if (!_finishRaised && frontZ >= _finishZ)
            {
                _finishRaised = true;
                FinishReached?.Invoke();
            }
        }

        /// <summary>
        /// Metres from <paramref name="fromZ"/> to the nearest enemy pack still ahead, for the
        /// tutorial to time its prompt. Negative when nothing is ahead.
        /// </summary>
        public float DistanceToNextEnemy(float fromZ)
        {
            float best = -1f;
            foreach (EnemyPackBehaviour pack in _activeEnemies)
            {
                // Spent packs linger until they are behind the camera, and a retreating
                // FrontZ can briefly put one "ahead" again — which would mis-time a prompt.
                if (pack.Resolved) continue;
                float ahead = pack.transform.position.z - fromZ;
                if (ahead <= 0f) continue;
                if (best < 0f || ahead < best) best = ahead;
            }
            return best;
        }

        /// <summary>Metres to the nearest unconsumed gate still ahead; negative when none.</summary>
        public float DistanceToNextGate(float fromZ)
        {
            float best = -1f;
            foreach (GateBehaviour gate in _activeGates)
            {
                if (gate.Resolved) continue;
                float ahead = gate.transform.position.z - fromZ;
                if (ahead <= 0f) continue;
                if (best < 0f || ahead < best) best = ahead;
            }
            return best;
        }

        /// <summary>Spell effect in the runner phase: destroys enemy packs within range ahead.</summary>
        public int ClearEnemiesAhead(float fromZ, float rangeMeters)
        {
            int cleared = 0;
            for (int i = _activeEnemies.Count - 1; i >= 0; i--)
            {
                EnemyPackBehaviour pack = _activeEnemies[i];
                // A pack the crowd already passed lingers in the list until it is behind the
                // camera; a spell must not reach back and pop it.
                if (pack.Resolved) continue;
                float z = pack.transform.position.z;
                if (z < fromZ || z > fromZ + rangeMeters) continue;
                _activeEnemies.RemoveAt(i);
                _enemyPool.Release(pack);
                cleared++;
            }
            return cleared;
        }
    }
}
