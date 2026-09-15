using BattleRunner.Core.World;
using UnityEngine;

namespace BattleRunner.Gameplay.Track
{
    /// <summary>
    /// The road stops being a road.
    ///
    /// THE REPORT WAS "the difference in graphics between before the boss and after is minimal
    /// - pretty much only color - turns to green from the blue", and reading the encounter
    /// says exactly why. `BossEncounterState.Enter` shows a mesh, adds a HUD bar and switches
    /// the music. It changes NOTHING about the place: the same cobbles, the same two lane
    /// lines, the same two rails running off to the same horizon, now standing still because
    /// the run speed is gone. A boss fight was a still frame of whatever road the round
    /// happened to end on.
    ///
    /// A DISC, AND THAT IS THE WHOLE IDEA. The shape is what carries it — a wider road is
    /// still a road, and a player who has run down a 6.6 m strip for forty seconds reads a
    /// circle as somewhere they have ARRIVED. The ring of stones around it replaces the rails
    /// rather than sitting beside them, so the boundary changes from two parallel lines to a
    /// closed one.
    ///
    /// FOUR EXTRA DRAW CALLS, and only while a boss is on screen: the floor, the threshold,
    /// the stones in one instanced batch and the beacons in another. Doc 04 budgets under 60
    /// on the lowest tier and the whole road costs four, so this is affordable — but it is
    /// affordable because the ring is instanced. Twelve separate stone objects would have been
    /// twelve draws for one decoration.
    /// </summary>
    public sealed class BossArena : MonoBehaviour
    {
        /// <summary>How far the floor reaches. Wider than the road by a factor of four.</summary>
        private const float Radius = 13.5f;
        private const int StoneCount = 12;
        /// <summary>Every fourth stone is a beacon, so the ring has a rhythm rather than a count.</summary>
        private const int BeaconEvery = 4;
        private const float StoneScale = 2.6f;
        private const float BeaconScale = 3.4f;
        /// <summary>Seconds for the arena to rise. Fast, because the boss is already here.</summary>
        private const float RiseSeconds = 0.55f;

        private GameObject _floor;
        private GameObject _threshold;
        private Material _floorMaterial;
        private Material _stoneMaterial;
        private Material _beaconMaterial;
        private Mesh _stoneMesh;

        private readonly Matrix4x4[] _stones = new Matrix4x4[StoneCount];
        private readonly Matrix4x4[] _beacons = new Matrix4x4[StoneCount];
        private int _stoneCount;
        private int _beaconCount;

        private Vector3 _centre;
        private float _rise;
        private bool _active;

        public void Initialize(Material baseMaterial)
        {
            if (baseMaterial == null) return;

            _floorMaterial = ShaderSafety.CreateMaterial(baseMaterial);
            _stoneMaterial = ShaderSafety.CreateMaterial(baseMaterial);
            _beaconMaterial = ShaderSafety.CreateMaterial(baseMaterial);
            // No walk cycle on any of them. _BobAmount is 0.12 by default on this shader and
            // the arena is scenery — a standing stone that marched on the spot is the same
            // bug the enemy squads had before SquadRenderer split its material.
            foreach (Material m in new[] { _floorMaterial, _stoneMaterial, _beaconMaterial })
            {
                m.SetFloatSafe("_BobAmount", 0f);
                m.SetFloatSafe("_ToneSpread", 0f);
            }
            _stoneMaterial.enableInstancing = true;
            _beaconMaterial.enableInstancing = true;
            _stoneMesh = ProceduralMeshes.StandingStone;

            _floor = Build("ArenaFloor", ProceduralMeshes.ArenaFloor, _floorMaterial);
            _threshold = Build("ArenaThreshold", ProceduralMeshes.BuildBox(Vector3.zero,
                new Vector3(1f, 0.18f, 1f)), _floorMaterial);
            Hide();
        }

        private GameObject Build(string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            // Receives, never casts. It is a floor: its own shadow would be a no-op, and it
            // is the surface the boss's and the army's shadows have to land on.
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            return go;
        }

        /// <summary>
        /// Open the arena between the army and the boss.
        ///
        /// Centred on the MIDPOINT rather than on the boss, because the fight happens in the
        /// gap: a disc centred on the boss puts the army's front rank on its rim, standing on
        /// the old road looking in at somewhere it has not got to.
        /// </summary>
        public void Show(WorldTheme theme, float armyZ, float bossZ)
        {
            if (_floor == null || theme == null) return;

            _centre = new Vector3(0f, 0f, (armyZ + bossZ) * 0.5f);
            _rise = 0f;
            _active = true;

            // The arena is the world's own stone, DARKENED, plus the world's accent on the
            // beacons. Deriving it rather than authoring it per world is what keeps a world
            // added later from arriving with a grey courtyard in the middle of it.
            Color floor = ThemePalette.ToColor(theme.RoadStone.Scaled(0.72f));
            _floorMaterial.SetColorSafe("_BaseColor", floor);
            _floorMaterial.SetColorSafe("_EmissionColor", Color.black);
            _stoneMaterial.SetColorSafe("_BaseColor",
                ThemePalette.ToColor(theme.PropStone.Scaled(0.85f)));
            _stoneMaterial.SetColorSafe("_EmissionColor", Color.black);
            _beaconMaterial.SetColorSafe("_BaseColor",
                ThemePalette.ToColor(theme.PropStone.Scaled(0.55f)));
            // The one lit thing in the ring. Emission on this shader is
            // _EmissionColor * (rim * _RimStrength + _EmissionFlat), so a flat term is what
            // makes a beacon read as a light rather than as an outlined rock.
            _beaconMaterial.SetColorSafe("_EmissionColor",
                ThemePalette.ToColor(theme.Accent.Scaled(1.6f)));
            _beaconMaterial.SetFloatSafe("_EmissionFlat", 0.55f);

            LayOutRing();
            _floor.SetActive(true);
            _threshold.SetActive(true);
        }

        public void Hide()
        {
            _active = false;
            _rise = 0f;
            if (_floor != null) _floor.SetActive(false);
            if (_threshold != null) _threshold.SetActive(false);
        }

        private void LayOutRing()
        {
            _stoneCount = 0;
            _beaconCount = 0;
            for (int i = 0; i < StoneCount; i++)
            {
                float angle = i * 2f * Mathf.PI / StoneCount;
                // Just OUTSIDE the floor's rim, so the stones stand on the old ground and the
                // disc reads as something laid down between them rather than as a lid.
                var at = new Vector3(_centre.x + Mathf.Cos(angle) * (Radius + 0.6f), 0f,
                                     _centre.z + Mathf.Sin(angle) * (Radius + 0.6f));
                bool beacon = i % BeaconEvery == 0;
                float scale = beacon ? BeaconScale : StoneScale;
                // Facing inward, with a per-stone twist off the index so a ring of identical
                // meshes does not read as a fence. Hashed rather than random: an arena that
                // rearranged itself between two looks at the same fight would be a bug.
                float yaw = -angle * Mathf.Rad2Deg + 90f + ((i * 37) % 23 - 11);
                var trs = Matrix4x4.TRS(at, Quaternion.Euler(0f, yaw, 0f),
                    new Vector3(scale, scale * (0.85f + 0.05f * (i % 3)), scale));
                if (beacon) _beacons[_beaconCount++] = trs;
                else _stones[_stoneCount++] = trs;
            }
        }

        private void LateUpdate()
        {
            if (!_active || _floor == null) return;

            // THE ARENA RISES. Appearing whole on one frame reads as a pop-in rather than as
            // arrival, and this is the one moment in the game the place itself changes.
            _rise = Mathf.Min(1f, _rise + Time.deltaTime / RiseSeconds);
            float eased = 1f - (1f - _rise) * (1f - _rise);

            float radius = Radius * eased;
            // THE TOP LANDS AT y = 0.02, WHICH IS THE NUMBER THAT MATTERS. The road's surface
            // is exactly y = 0 and every unit in the game stands on it, so an arena floor
            // 13 cm thick laid on top would bury the army and the boss to the ankles. The mesh
            // is 0.10 thick, so the object sits at -0.08 and only 2 cm of it is above the
            // road: enough to cover the 1.5 cm lane markings, not enough to stand in.
            _floor.transform.position = new Vector3(_centre.x, -0.08f, _centre.z);
            _floor.transform.localScale = new Vector3(radius, 1f, radius);

            // A bar across the road where the arena begins. This is the THRESHOLD — without a
            // line to cross, a disc drawn under a standing army is just different paving. It
            // is the one part allowed to stand proud, because a kerb is what a threshold is.
            _threshold.transform.position =
                new Vector3(0f, 0.02f, _centre.z - radius + 0.4f);
            _threshold.transform.localScale = new Vector3(radius * 2f * 0.55f, 1f, 0.55f);

            if (_stoneMesh == null) return;
            var stoneParams = new RenderParams(_stoneMaterial)
            {
                shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On,
                receiveShadows = true,
                worldBounds = new Bounds(_centre, new Vector3(Radius * 3f, 12f, Radius * 3f))
            };
            var beaconParams = new RenderParams(_beaconMaterial)
            {
                shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On,
                receiveShadows = true,
                worldBounds = stoneParams.worldBounds
            };
            // Only once the floor has arrived under them. Stones standing around nothing for
            // half a second would read as the floor having failed to draw.
            if (_rise < 0.35f) return;
            if (_stoneCount > 0)
                Graphics.RenderMeshInstanced(stoneParams, _stoneMesh, 0, _stones, _stoneCount);
            if (_beaconCount > 0)
                Graphics.RenderMeshInstanced(beaconParams, _stoneMesh, 0, _beacons, _beaconCount);
        }
    }
}
