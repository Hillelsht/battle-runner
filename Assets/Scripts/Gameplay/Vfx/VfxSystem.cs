using System.Collections.Generic;
using BattleRunner.Gameplay;
using UnityEngine;

namespace BattleRunner.Gameplay.Vfx
{
    /// <summary>
    /// Every additive effect in the game: gate shockwaves, the spell's shock ring, debris
    /// from a pack that bit the army, and the boss's death beat.
    ///
    /// A shockwave is a cylinder WALL that expands and flattens, not a ring lying on the
    /// road. The first version was flat and never appeared on device while the debris on
    /// the same material did — see ProceduralMeshes.ShockWall for the two candidate
    /// reasons and why this shape retires both.
    ///
    /// Until this existed nothing HAPPENED when you passed a gate — the number changed and
    /// the camera nudged, and that was the whole of it. A gate-multiplier runner lives on
    /// the moment the crowd doubles, and that moment had no event.
    ///
    /// TWO RULES SHAPE THE IMPLEMENTATION.
    ///
    /// It fails to nothing. A fourth shader in Resources is the exact path that shipped
    /// v0.1.0 as solid magenta, so Initialize validates the material against the ACTIVE
    /// pipeline and, if anything is off, leaves Enabled false — after which every Play
    /// call is a no-op. A build with no effects is a disappointment; a build with magenta
    /// rectangles flashing across the road is unshippable.
    ///
    /// It never allocates during a run. Rings and motes are pooled and reused; a burst is
    /// a loop over already-built GameObjects. The crowd is one instanced draw call and the
    /// track is a handful of boxes — this is not the place to start generating garbage.
    /// </summary>
    public sealed class VfxSystem : MonoBehaviour
    {
        /// <summary>False when the additive material could not be resolved. Every Play is then inert.</summary>
        public bool Enabled { get; private set; }

        private const int RingCapacity = 12;
        private const int MoteCapacity = 48;
        private const int BoltCapacity = 4;

        // The wall's base sits just clear of the road so it does not z-fight the lane
        // decals at 0.005-0.02. Its height shrinks as it expands: a wave that spreads and
        // flattens, rather than an inflating cylinder.
        private const float RingBaseY = 0.03f;
        private const float WallHeightNear = 1.15f;
        private const float WallHeightFar = 0.22f;

        private sealed class Ring
        {
            public Transform Transform;
            public MeshRenderer Renderer;
            public float Age;
            public float Life;
            public float FromRadius;
            public float ToRadius;
            public Color Tint;
        }

        private sealed class Mote
        {
            public Transform Transform;
            public MeshRenderer Renderer;
            public float Age;
            public float Life;
            public Vector3 Velocity;
            public Vector3 Spin;
            public Color Tint;
        }

        /// <summary>
        /// A spell bolt in flight. The spell used to be instantaneous — a number left the
        /// boss's health bar and enemy packs stopped existing — so the flick had a cause
        /// and an effect with nothing in between. A travelling object gives it a path, and
        /// the path is what tells the player how far the spell actually reaches.
        /// </summary>
        private sealed class BoltState
        {
            public Transform Transform;
            public MeshRenderer Renderer;
            public float Age;
            public float Life;
            public Vector3 From;
            public Vector3 To;
            public Color Tint;
            /// <summary>Doubles as "inactive". A pool entry starts detonated so it is free.</summary>
            public bool Detonated;
        }

        private readonly List<Ring> _rings = new List<Ring>(RingCapacity);
        private readonly List<Mote> _motes = new List<Mote>(MoteCapacity);
        private readonly List<BoltState> _bolts = new List<BoltState>(BoltCapacity);
        private MaterialPropertyBlock _block;

        // Deterministic, not Random: the same gate should not flicker differently between
        // two runs of the same seed, and the run loop is otherwise reproducible.
        private int _scatter;

        public void Initialize(Material vfxMaterial)
        {
            if (vfxMaterial == null || vfxMaterial.shader == null || !vfxMaterial.shader.isSupported)
            {
                Debug.LogWarning("[Vfx] Additive material unusable here — effects are off for this session.");
                return;
            }

            _block = new MaterialPropertyBlock();

            for (int i = 0; i < RingCapacity; i++)
            {
                MeshRenderer renderer = Build("Shock", ProceduralMeshes.ShockWall, vfxMaterial);
                _rings.Add(new Ring { Transform = renderer.transform, Renderer = renderer });
            }
            for (int i = 0; i < MoteCapacity; i++)
            {
                MeshRenderer renderer = Build("Mote", ProceduralMeshes.Cube, vfxMaterial);
                _motes.Add(new Mote { Transform = renderer.transform, Renderer = renderer });
            }
            for (int i = 0; i < BoltCapacity; i++)
            {
                MeshRenderer renderer = Build("Bolt", ProceduralMeshes.Cube, vfxMaterial);
                // Detonated:true from birth. Without it a never-fired bolt has Life = 0, the
                // update divides Age by zero, clamps to 1 and "arrives" at the world origin
                // on the first frame of the game.
                _bolts.Add(new BoltState
                {
                    Transform = renderer.transform, Renderer = renderer, Detonated = true
                });
            }

            Enabled = true;
        }

        private MeshRenderer Build(string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            // Additive geometry with no depth write casts nothing and receives nothing;
            // asking the shadow pass to consider it is pure cost.
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            go.SetActive(false);
            return renderer;
        }

        /// <summary>A ring expanding outward on the ground. The workhorse: gates, spells, deaths.</summary>
        public void Shock(Vector3 position, Color tint, float fromRadius, float toRadius, float life)
        {
            if (!Enabled) return;

            Ring ring = null;
            for (int i = 0; i < _rings.Count; i++)
            {
                if (_rings[i].Age < _rings[i].Life) continue;
                ring = _rings[i];
                break;
            }
            // Every ring busy: steal the oldest rather than skip. A dropped effect on a
            // busy frame is exactly the frame the player most needs feedback on.
            if (ring == null) ring = Oldest();

            ring.Age = 0f;
            ring.Life = Mathf.Max(0.05f, life);
            ring.FromRadius = fromRadius;
            ring.ToRadius = toRadius;
            ring.Tint = tint;
            ring.Transform.position = new Vector3(position.x, RingBaseY, position.z);
            ring.Transform.localScale = new Vector3(fromRadius, WallHeightNear, fromRadius);
            ring.Transform.gameObject.SetActive(true);
            Paint(ring.Renderer, tint, 1f, band: 1f);
        }

        /// <summary>A scatter of embers thrown out of a point. Debris, not particles.</summary>
        public void Burst(Vector3 position, Color tint, int count, float speed, float life)
        {
            if (!Enabled) return;

            for (int n = 0; n < count; n++)
            {
                Mote mote = null;
                for (int i = 0; i < _motes.Count; i++)
                {
                    if (_motes[i].Age < _motes[i].Life) continue;
                    mote = _motes[i];
                    break;
                }
                // Motes are decoration, not information — unlike a ring, a burst that is a
                // few embers short reads identically, so a full pool just drops the rest.
                if (mote == null) return;

                // A cheap deterministic scatter. Golden-angle spread so successive motes
                // never line up, and a hashed vertical component so the fan is not flat.
                _scatter++;
                float angle = _scatter * 2.39996323f;
                float lift = 0.45f + 0.55f * Frac(_scatter * 0.61803399f);
                float reach = 0.55f + 0.75f * Frac(_scatter * 0.37718862f);

                mote.Age = 0f;
                mote.Life = life * (0.7f + 0.6f * Frac(_scatter * 0.24512f));
                mote.Velocity = new Vector3(Mathf.Cos(angle) * reach, lift, Mathf.Sin(angle) * reach) * speed;
                mote.Spin = new Vector3(220f * reach, 310f * lift, 170f * reach);
                mote.Tint = tint;
                mote.Transform.position = position + Vector3.up * 0.35f;
                mote.Transform.localRotation = Quaternion.Euler(angle * 57.3f, angle * 31.1f, 0f);
                mote.Transform.localScale = Vector3.one * (0.15f + 0.12f * reach);
                mote.Transform.gameObject.SetActive(true);
                Paint(mote.Renderer, tint, 1f, band: 0f);
            }
        }

        /// <summary>
        /// A bolt that flies from one point to another and detonates on arrival, throwing a
        /// wall and embers where it lands. The flight time is derived from the distance so
        /// a spell that reaches further visibly takes longer to get there.
        /// </summary>
        public void Bolt(Vector3 from, Vector3 to, Color tint, float speed)
        {
            if (!Enabled) return;

            BoltState bolt = null;
            for (int i = 0; i < _bolts.Count; i++)
            {
                if (!_bolts[i].Detonated) continue;
                bolt = _bolts[i];
                break;
            }
            if (bolt == null) bolt = _bolts[0];

            float distance = Vector3.Distance(from, to);
            bolt.Age = 0f;
            bolt.Life = Mathf.Clamp(distance / Mathf.Max(1f, speed), 0.12f, 0.75f);
            bolt.From = from + Vector3.up * 0.6f;
            bolt.To = to + Vector3.up * 0.6f;
            bolt.Tint = tint;
            bolt.Detonated = false;
            bolt.Transform.position = bolt.From;
            bolt.Transform.gameObject.SetActive(true);
            Paint(bolt.Renderer, tint, 1f, band: 0f);
        }

        /// <summary>Drops everything immediately — a phase change must not leave embers hanging.</summary>
        public void Clear()
        {
            for (int i = 0; i < _rings.Count; i++)
            {
                _rings[i].Age = _rings[i].Life;
                _rings[i].Transform.gameObject.SetActive(false);
            }
            for (int i = 0; i < _motes.Count; i++)
            {
                _motes[i].Age = _motes[i].Life;
                _motes[i].Transform.gameObject.SetActive(false);
            }
            for (int i = 0; i < _bolts.Count; i++)
            {
                _bolts[i].Age = _bolts[i].Life;
                _bolts[i].Detonated = true;
                _bolts[i].Transform.gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            if (!Enabled) return;
            float dt = Time.deltaTime;

            for (int i = 0; i < _rings.Count; i++)
            {
                Ring ring = _rings[i];
                if (ring.Age >= ring.Life) continue;

                ring.Age += dt;
                float t = Mathf.Clamp01(ring.Age / ring.Life);
                if (t >= 1f)
                {
                    ring.Transform.gameObject.SetActive(false);
                    continue;
                }

                // Radius eases OUT and brightness falls off faster than linear: a shockwave
                // sprints away from its origin and is gone before it stops moving. Easing
                // the radius linearly instead reads as an inflating balloon.
                float eased = 1f - (1f - t) * (1f - t);
                float radius = Mathf.Lerp(ring.FromRadius, ring.ToRadius, eased);
                float height = Mathf.Lerp(WallHeightNear, WallHeightFar, eased);
                ring.Transform.localScale = new Vector3(radius, height, radius);
                Paint(ring.Renderer, ring.Tint, (1f - t) * (1f - t), band: 1f);
            }

            for (int i = 0; i < _bolts.Count; i++)
            {
                BoltState bolt = _bolts[i];
                if (bolt.Detonated) continue;

                bolt.Age += dt;
                float bt = Mathf.Clamp01(bolt.Age / bolt.Life);

                if (bt >= 1f)
                {
                    // The detonation is the point of the bolt. Fired here rather than by the
                    // caller so the wall and the embers land where the bolt actually ARRIVED,
                    // on the frame it arrived, with no way for the two to drift apart.
                    bolt.Detonated = true;
                    Shock(bolt.To, bolt.Tint, 1.0f, 7.5f, 0.45f);
                    Burst(bolt.To, bolt.Tint, 14, 5.5f, 0.6f);
                    bolt.Transform.gameObject.SetActive(false);
                    continue;
                }

                // Eased so it leaves fast and arrives decisively, and stretched along its
                // own travel direction: a bolt shaped like a cube reads as a floating box,
                // one stretched into its velocity reads as something moving quickly.
                Vector3 position = Vector3.Lerp(bolt.From, bolt.To, bt * bt * (3f - 2f * bt));
                bolt.Transform.position = position;
                Vector3 heading = bolt.To - bolt.From;
                if (heading.sqrMagnitude > 1e-4f)
                    bolt.Transform.rotation = Quaternion.LookRotation(heading, Vector3.up);
                bolt.Transform.localScale = new Vector3(0.34f, 0.34f, 1.9f);
                Paint(bolt.Renderer, bolt.Tint, 1f, band: 0f);
            }

            for (int i = 0; i < _motes.Count; i++)
            {
                Mote mote = _motes[i];
                if (mote.Age >= mote.Life) continue;

                mote.Age += dt;
                float t = Mathf.Clamp01(mote.Age / mote.Life);
                if (t >= 1f)
                {
                    mote.Transform.gameObject.SetActive(false);
                    continue;
                }

                mote.Velocity += Vector3.down * (11f * dt);
                mote.Transform.position += mote.Velocity * dt;
                mote.Transform.Rotate(mote.Spin * dt, Space.Self);
                // Shrink as well as dim. A mote that only fades leaves a ghost of its
                // silhouette at the last frame it is drawn.
                mote.Transform.localScale = Vector3.one * (0.15f * (1f - t) + 0.04f);
                Paint(mote.Renderer, mote.Tint, 1f - t, band: 0f);
            }
        }

        private void Paint(MeshRenderer renderer, Color tint, float fade, float band)
        {
            _block.SetColor("_TintColor", tint);
            _block.SetFloat("_Fade", Mathf.Clamp01(fade));
            _block.SetFloat("_Band", band);
            renderer.SetPropertyBlock(_block);
        }

        private Ring Oldest()
        {
            Ring oldest = _rings[0];
            for (int i = 1; i < _rings.Count; i++)
                if (_rings[i].Age > oldest.Age) oldest = _rings[i];
            return oldest;
        }

        private static float Frac(float v) => v - Mathf.Floor(v);
    }
}
