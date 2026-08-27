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
    /// </summary>
    public sealed class CrowdController : MonoBehaviour
    {
        private const int MaxSimulated = 512;

        private readonly Vector3[] _positions = new Vector3[MaxSimulated];
        private readonly Vector2[] _velocities = new Vector2[MaxSimulated];
        private readonly float[] _phases = new float[MaxSimulated];

        private LongEventChannel _forceChanged;
        private int _tierCap = 200;
        private float _spacing = 0.55f;
        private float _laneSpan = 2.6f;

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

        public void Initialize(LongEventChannel forceChanged, int tierCap, float laneSpanMeters)
        {
            _forceChanged = forceChanged;
            _tierCap = Mathf.Min(tierCap, MaxSimulated);
            _laneSpan = laneSpanMeters;
        }

        public void ResetRun(long startingForce, float startZ)
        {
            _centerX = 0f;
            _targetX = 0f;
            _centerVelX = 0f;
            _centerZ = startZ;
            for (int i = 0; i < MaxSimulated; i++)
            {
                _positions[i] = new Vector3(0f, 0f, startZ);
                _velocities[i] = Vector2.zero;
                _phases[i] = (i * 0.618f) % 1f;
            }
            SetForce(startingForce);
        }

        /// <summary>Normalized screen X from the lane-drag intent maps across the 3-lane span.</summary>
        public void OnLaneTarget(float normalizedX) =>
            _targetX = (Mathf.Clamp01(normalizedX) * 2f - 1f) * _laneSpan;

        public void SetForce(long force)
        {
            _forceCount = force < 0 ? 0 : force;
            _visibleUnits = CrowdMath.VisibleUnits(_forceCount, _tierCap);
            _forceChanged?.Raise(_forceCount);
        }

        public void AdvanceZ(float meters) => _centerZ += meters;

        public void Tick(float dt)
        {
            // Centroid steers toward the lane target; ~0.1 s smoothing absorbs the
            // classifier's commit window so control feels instant (doc 02).
            _centerX = CrowdMath.SpringDamperStep(_centerX, ref _centerVelX, _targetX, 12f, dt);

            float spacing = CurrentSpacing();
            float zBlend = Mathf.Exp(-10f * dt);

            for (int i = 0; i < _visibleUnits; i++)
            {
                System.Numerics.Vector2 slot = CrowdMath.PhyllotaxisSlot(i, spacing);
                float targetX = _centerX + slot.X;
                float targetZ = _centerZ + slot.Y;

                Vector2 velocity = _velocities[i];
                float x = CrowdMath.SpringDamperStep(_positions[i].x, ref velocity.x, targetX, 10f, dt);
                _velocities[i] = velocity;

                // Z is driven kinematically, not sprung: a critically damped spring
                // tracking a 10 m/s ramp sits a constant 2 m behind, so bodies rendered
                // two metres short of the gate that had already consumed them.
                float z = targetZ + (_positions[i].z - targetZ) * zBlend;
                _positions[i] = new Vector3(x, 0f, z);
            }
        }

        /// <summary>
        /// Formation spacing compresses past ~40 bodies so the disc radius saturates.
        /// At 0.55 spacing, 200 units spanned 15.5 m on a 7.9 m road.
        /// </summary>
        private float CurrentSpacing() =>
            _visibleUnits <= 40 ? _spacing : _spacing * Mathf.Sqrt(40f / _visibleUnits);

        /// <summary>World AABB of the visible crowd — instanced draws are culled against zero bounds otherwise (doc 04).</summary>
        public Bounds ComputeBounds()
        {
            float radius = CurrentSpacing() * Mathf.Sqrt(Mathf.Max(1, _visibleUnits)) + 2f;
            return new Bounds(new Vector3(_centerX, 0.8f, _centerZ), new Vector3(radius * 2f, 3f, radius * 2f));
        }

        public Vector3 UnitPosition(int index) => _positions[index];
        public float UnitPhase(int index) => _phases[index];
    }
}
