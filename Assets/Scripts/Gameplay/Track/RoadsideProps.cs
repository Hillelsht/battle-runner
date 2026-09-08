using System.Collections.Generic;
using BattleRunner.Core.Progression;
using BattleRunner.Core.World;
using BattleRunner.Gameplay.Crowd;
using UnityEngine;

namespace BattleRunner.Gameplay.Track
{
    /// <summary>
    /// Whatever is standing beside the road.
    ///
    /// THE BIGGEST SINGLE REASON EVERY ROUND LOOKED THE SAME IS THAT THERE WAS NO BACKGROUND.
    /// Sampled from nine device frames, either side of the road read (10, 8, 12) — black —
    /// in every one. `SpawnGroundStrip` builds a ground box, four lane lines, two rails and
    /// rung decals, and nothing whatsoever exists beyond x = +/-4.16 m. A different sky over
    /// the same void is still a void.
    ///
    /// TWO BANDS. A near band from 5.2 to 9 m carries detail the player reads as they pass;
    /// a far band from 10.5 to 26 m carries mass, at larger scales and lower density, and
    /// does most of the work of making the road feel like it runs THROUGH somewhere. Both are
    /// outside the rails, so nothing here can ever be mistaken for something to steer at.
    ///
    /// ONE INSTANCED DRAW PER KIND, exactly as CrowdRenderer does it — a verge of three
    /// hundred props costs about four draw calls against doc 04's budget of under sixty on
    /// the lowest tier. Placement is generated once per round from a hash of the round index,
    /// so a round is dressed identically every time it is played, and the visible slice is
    /// re-gathered only when the player has moved eight metres rather than every frame.
    ///
    /// NOTHING HERE CASTS A SHADOW. The rails are the only static casters today and the
    /// shadow pass is where a field of props would actually cost milliseconds; props sit
    /// outside the road where their shadows would mostly fall on unlit ground anyway.
    /// </summary>
    public sealed class RoadsideProps : MonoBehaviour
    {
        /// <summary>Inside edge of the near band. Outside the rails at 3.76 m, with room to spare.</summary>
        private const float NearInner = 5.2f;
        private const float NearOuter = 9.0f;
        private const float FarInner = 10.5f;
        private const float FarOuter = 26.0f;

        /// <summary>How far ahead props are gathered. Just past the furthest any world fogs out.</summary>
        private const float WindowAhead = 195f;
        private const float WindowBehind = 25f;

        /// <summary>Metres the crowd must travel before the visible slice is re-gathered.</summary>
        private const float RebuildStepMeters = 8f;

        private const int MaxPerKind = 192;

        private struct Placement
        {
            public int Kind;
            public float Z;
            public Matrix4x4 Trs;
        }

        private static readonly int KindCount = System.Enum.GetValues(typeof(PropKind)).Length;

        private CrowdController _crowd;
        private Material _material;
        private bool _instancingSupported;

        private readonly List<Placement> _placements = new List<Placement>(512);
        private Matrix4x4[][] _buckets;
        private int[] _counts;
        private Mesh[] _meshes;

        private float _lastGatherZ = float.NegativeInfinity;
        private bool _dressed;

        public void Initialize(CrowdController crowd, Material baseMaterial)
        {
            _crowd = crowd;

            _material = ShaderSafety.CreateMaterial(baseMaterial);
            // Not the crowd's material and not its settings. _BobAmount would make a
            // graveyard march, and _ToneSpread decodes its per-unit phase from INSTANCE
            // SCALE inside a 0.44-0.50 window — props are scaled 0.8 to 3.2, so every one of
            // them would pin to the top of that curve and come out ~30% brighter than
            // intended. Both are pinned off here rather than trusted to a default.
            _material.SetFloatSafe("_BobAmount", 0f);
            _material.SetFloatSafe("_ToneSpread", 0f);
            _material.SetFloatSafe("_RimPower", 4f);
            _material.SetFloatSafe("_RimStrength", 0.30f);
            _material.SetFloatSafe("_EmissionFlat", 0.02f);

            _buckets = new Matrix4x4[KindCount][];
            _counts = new int[KindCount];
            _meshes = new Mesh[KindCount];
            for (int k = 0; k < KindCount; k++)
            {
                _buckets[k] = new Matrix4x4[MaxPerKind];
                _meshes[k] = ProceduralMeshes.Prop((PropKind)k);
            }

            _instancingSupported = SystemInfo.supportsInstancing
                                   && _material != null && _material.enableInstancing;
        }

        /// <summary>
        /// Dress a round's verges. Called once from RunLoadingState with the same round index
        /// the world was chosen from, so the placement is part of the round's identity.
        /// </summary>
        public void Build(WorldTheme theme, ThemeVariant variant, int roundIndex,
            float fromZ, float toZ)
        {
            Clear();
            if (theme == null || theme.Props == null || theme.Props.Length == 0) return;
            if (_material == null) return;

            _material.SetColorSafe("_BaseColor", ThemePalette.Shifted(theme.PropStone, variant.HueShift));
            // The accent, well below the bloom knee. Props are scenery: they should catch the
            // world's colour on their edges, not become light sources competing with the gates.
            _material.SetColorSafe("_EmissionColor",
                ThemePalette.Shifted(theme.Accent.Scaled(0.22f), variant.HueShift));

            float density = Mathf.Clamp(theme.PropDensity * variant.PropDensity, 2f, 40f);
            uint rng = Seed(roundIndex);

            for (int s = -1; s <= 1; s += 2)
            {
                Scatter(theme, ref rng, s, fromZ, toZ, 100f / density,
                    NearInner, NearOuter, 0.80f, 1.70f);
                // The far band is sparser and larger: mass at distance, not detail.
                Scatter(theme, ref rng, s, fromZ, toZ, 100f / (density * 0.55f),
                    FarInner, FarOuter, 1.50f, 3.20f);
            }

            _placements.Sort((a, b) => a.Z.CompareTo(b.Z));
            _dressed = _placements.Count > 0;
            _lastGatherZ = float.NegativeInfinity;
        }

        public void Clear()
        {
            _placements.Clear();
            _dressed = false;
            for (int k = 0; k < _counts.Length; k++) _counts[k] = 0;
        }

        private void Scatter(WorldTheme theme, ref uint rng, int side, float fromZ, float toZ,
            float step, float inner, float outer, float minScale, float maxScale)
        {
            float z = fromZ + Next01(ref rng) * step;
            while (z < toZ)
            {
                var kind = theme.Props[(int)(NextUInt(ref rng) % (uint)theme.Props.Length)];
                float x = side * Mathf.Lerp(inner, outer, Next01(ref rng));
                float scale = Mathf.Lerp(minScale, maxScale, Next01(ref rng));
                float yaw = Next01(ref rng) * 360f;
                // A slight lean off vertical. Nothing beside a road this old stands straight,
                // and a field of perfectly upright boxes reads as a fence rather than a place.
                float tiltX = (Next01(ref rng) - 0.5f) * 9f;
                float tiltZ = (Next01(ref rng) - 0.5f) * 9f;

                _placements.Add(new Placement
                {
                    Kind = (int)kind,
                    Z = z,
                    Trs = Matrix4x4.TRS(new Vector3(x, 0f, z),
                        Quaternion.Euler(tiltX, yaw, tiltZ), Vector3.one * scale)
                });

                z += step * Mathf.Lerp(0.55f, 1.55f, Next01(ref rng));
            }
        }

        private void LateUpdate()
        {
            if (!_dressed || _crowd == null || _material == null) return;

            float centre = _crowd.CenterZ;
            if (Mathf.Abs(centre - _lastGatherZ) >= RebuildStepMeters)
            {
                _lastGatherZ = centre;
                Gather(centre);
            }

            var rp = new RenderParams(_material)
            {
                worldBounds = new Bounds(new Vector3(0f, 4f, centre),
                    new Vector3(FarOuter * 2.4f, 20f, (WindowAhead + WindowBehind) * 1.1f)),
                shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
                receiveShadows = true
            };

            for (int k = 0; k < KindCount; k++)
            {
                int n = _counts[k];
                if (n <= 0) continue;
                if (_instancingSupported)
                {
                    Graphics.RenderMeshInstanced(rp, _meshes[k], 0, _buckets[k], n);
                    continue;
                }

                // Without instancing this is a lot of draws, so the verge is dropped rather
                // than allowed to cost the frame. The crowd falls back because the game is
                // unplayable without it; scenery is not.
                return;
            }
        }

        /// <summary>Collect the props inside the visible window into per-kind buckets.</summary>
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
                int n = _counts[p.Kind];
                if (n >= MaxPerKind) continue;
                _buckets[p.Kind][n] = p.Trs;
                _counts[p.Kind] = n + 1;
            }
        }

        // A small xorshift rather than UnityEngine.Random: the verge must be identical every
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
