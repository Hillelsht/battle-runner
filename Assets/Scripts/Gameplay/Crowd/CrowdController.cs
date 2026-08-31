using BattleRunner.Core.Crowd;
using BattleRunner.Data.Channels;
using UnityEngine;

namespace BattleRunner.Gameplay.Crowd
{
    /// <summary>
    /// The crowd is data, not GameObjects (doc 04): one controller owns every unit's
    /// position in plain arrays; rendering happens in CrowdRenderer via instancing.
    /// ForceCount (long) is the source of truth for damage and UI; rendered bodies
    /// saturate at the device-tier cap.
    ///
    /// The formation is bounded to the road. Everything that needs to know where the
    /// crowd actually is -- the renderer's bounds, the hero, gate and enemy crossings --
    /// reads <see cref="HalfWidth"/> / <see cref="FrontZ"/> / <see cref="RearZ"/> from
    /// here rather than re-deriving it, because a second copy of the formation model is
    /// exactly how the old disc drifted 3.5 m away from what the player saw.
    /// </summary>
    public sealed class CrowdController : MonoBehaviour
    {
        public const int MaxSimulated = 512;

        private readonly Vector3[] _positions = new Vector3[MaxSimulated];
        private readonly Vector2[] _slotOffsets = new Vector2[MaxSimulated];
        private readonly float[] _phases = new float[MaxSimulated];

        private LongEventChannel _forceChanged;
        private int _tierCap = 200;
        private float _laneWidth = 2.2f;

        // Width belongs to the road: the crowd sits inside one 2.2 m lane and passes through
        // a 1.60 m gate aperture, so it never exceeds ~0.78 m either side of the anchor no
        // matter how large the army gets. Growth goes up the road instead.
        private float _halfWidthMax = CrowdMath.HalfWidthMaxFor(2.2f);

        private const float LeaderMargin = 0.30f;
        private const float MinLeaderOffset = 0.60f;

        private long _forceCount;
        private int _visibleUnits;
        private float _centerX;
        private float _targetX;
        private float _centerZ;
        private float _centerVelX;

        public long ForceCount => _forceCount;
        public int VisibleUnits => _visibleUnits;
        public float CenterX => _centerX;
        public float CenterZ => _centerZ;
        public Vector3 Centroid => new Vector3(_centerX, 0f, _centerZ);

        /// <summary>Half the crowd's lateral extent — the same number the renderer draws.</summary>
        public float HalfWidth => Envelope().X;

        /// <summary>
        /// World Z of the crowd's leading plane: where the hero stands and where gates and
        /// enemies resolve, so what the player sees touching a gate is what scores it.
        /// </summary>
        public float FrontZ => _centerZ + Mathf.Max(MinLeaderOffset, Envelope().Y + LeaderMargin);

        /// <summary>World Z of the crowd's trailing edge.</summary>
        public float RearZ => _centerZ - Envelope().Z;

        private System.Numerics.Vector3 Envelope() =>
            CrowdMath.FormationEnvelope(_visibleUnits, _halfWidthMax);

        public void Initialize(LongEventChannel forceChanged, int tierCap, float laneWidthMeters)
        {
            _forceChanged = forceChanged;
            _tierCap = Mathf.Min(tierCap, MaxSimulated);
            _laneWidth = laneWidthMeters;
            // Leave a visible gutter inside the lane so the road reads either side of the crowd.
            _halfWidthMax = CrowdMath.HalfWidthMaxFor(laneWidthMeters);
        }

        public void ResetRun(long startingForce, float startZ)
        {
            _centerX = 0f;
            _targetX = 0f;
            _centerVelX = 0f;
            _centerZ = startZ;
            for (int i = 0; i < MaxSimulated; i++) _phases[i] = (i * 0.618f) % 1f;

            // Establish the count first: seeding needs the slot offsets, and those depend
            // on it. The old order collapsed all 512 slots to one point before the count
            // was known, so the crowd had to unfold from a dot on the first frame.
            _visibleUnits = 0;
            SetForce(startingForce);
        }

        /// <summary>Snap the steering target to a discrete lane centre — the road has three lanes, not a continuum.</summary>
        public void OnLaneTarget(float normalizedX)
        {
            int lane = Mathf.Clamp(Mathf.FloorToInt(Mathf.Clamp01(normalizedX) * 3f) - 1, -1, 1);
            _targetX = lane * _laneWidth;
        }

        public void SetForce(long force)
        {
            int previous = _visibleUnits;
            _forceCount = force < 0 ? 0 : force;
            _visibleUnits = CrowdMath.VisibleUnits(_forceCount, _tierCap);

            // Seed slots that just became visible. Without this they draw from wherever
            // they were last parked — at the run's start Z — and streak the length of the
            // level to catch up the first time a gate grows the crowd.
            for (int i = previous; i < _visibleUnits; i++)
            {
                System.Numerics.Vector2 slot =
                    CrowdMath.FormationSlot(i, _visibleUnits, _halfWidthMax);
                _slotOffsets[i] = new Vector2(slot.X, slot.Y);
                _positions[i] = new Vector3(_centerX + slot.X, 0f, _centerZ + slot.Y);
            }

            _forceChanged?.Raise(_forceCount);
        }

        public void AdvanceZ(float meters) => _centerZ += meters;

        public void Tick(float dt)
        {
            // Centroid steers toward the lane target; ~0.1 s smoothing absorbs the
            // classifier's commit window so control feels instant (doc 02).
            _centerX = CrowdMath.SpringDamperStep(_centerX, ref _centerVelX, _targetX, 12f, dt);

            // Smooth the LOCAL offset, never the world position. Smoothing world Z against a
            // target that advances 10 m/s leaves a permanent v*dt*b/(1-b) = 0.92 m error, so
            // every body rendered ~1 m behind where the game thought it was. Driving the ramp
            // through the exact _centerZ term and settling only the offset removes it entirely.
            float blend = 1f - Mathf.Exp(-10f * dt);

            for (int i = 0; i < _visibleUnits; i++)
            {
                System.Numerics.Vector2 slot =
                    CrowdMath.FormationSlot(i, _visibleUnits, _halfWidthMax);
                Vector2 offset = _slotOffsets[i];
                offset.x += (slot.X - offset.x) * blend;
                offset.y += (slot.Y - offset.y) * blend;
                _slotOffsets[i] = offset;

                _positions[i] = new Vector3(_centerX + offset.x, 0f, _centerZ + offset.y);
            }
        }

        /// <summary>
        /// World AABB of the visible crowd — instanced draws are culled against zero bounds
        /// (doc 04). Built from the formation's real, asymmetric extent: the crowd trails the
        /// anchor, so a box centred on the centroid would clip the tail away.
        /// </summary>
        public Bounds ComputeBounds()
        {
            float halfWidth = Envelope().X;
            var bounds = new Bounds();
            bounds.SetMinMax(
                new Vector3(_centerX - halfWidth - 0.5f, -0.2f, RearZ - 0.5f),
                new Vector3(_centerX + halfWidth + 0.5f, 1.8f, FrontZ + 0.5f));
            return bounds;
        }

        public Vector3 UnitPosition(int index) => _positions[index];
        public float UnitPhase(int index) => _phases[index];
    }
}
