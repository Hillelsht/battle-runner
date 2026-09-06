using BattleRunner.Gameplay.Crowd;
using UnityEngine;

namespace BattleRunner.Gameplay
{
    /// <summary>
    /// Portrait chase camera plus a juice layer: trauma shake, fov punch, lane lean.
    ///
    /// The rig keeps its own BASE pose and writes the JUICED pose to the transform.
    /// Reading the transform back into the follow — as the old
    /// Vector3.Lerp(transform.position, ...) did — folds the shake into the smoothing,
    /// so the camera chases its own noise and never settles.
    ///
    /// Nothing here touches Time.timeScale, which appears nowhere in this project and
    /// must stay that way: a rewarded-ad callback is the one thing that can truly wedge
    /// a run.
    /// </summary>
    public sealed class CameraRig : MonoBehaviour
    {
        /// <summary>
        /// How far behind the crowd the camera sits. TrackVisibility despawns against
        /// crowd.CenterZ minus this CONSTANT, so the juice layer must never move the rig
        /// along world z by anything the despawn margin has not been told about.
        /// </summary>
        public const float SetbackMeters = 10f;

        public const float BaseFieldOfView = 60f;

        // The floor is 58, not 55. Only the boss telegraph narrows the frame (-2.5 max),
        // and narrowing walks the bottom-of-frame ground point FORWARD: at 58 degrees it
        // sits at CenterZ-3.32 against a crowd rear of CenterZ-2.47 at the 200 tier cap,
        // and 1.5 degrees of shake pitch still leaves ~0.4 m of margin. The 72 ceiling
        // puts that band at CenterZ-4.93, comfortably ahead of the despawn plane.
        private const float MinFieldOfView = 58f;
        private const float MaxFieldOfView = 72f;

        // -ln(1 - 5/60) * 60. Reproduces the shipped 60 fps smoothing step exactly while
        // making the follow frame-rate independent — the old Lerp(a, b, dt * 5f) moved
        // further per second on a slow device than on a fast one.
        private const float FollowRate = 5.2207f;

        // Lane lean. Deliberately NOT driven off CrowdController's steering velocity:
        // that out-param peaks around 0.0155 m/s on a 2.2 m lane step, which would make
        // the lean invisible. The CenterX frame delta actually reflects the movement.
        private const float LateralSmoothRate = 8f;
        private const float LeadYawPerLateralSpeed = 0.147f;
        private const float RollPerLateralSpeed = 0.245f;
        private const float MaxLeadYawDegrees = 1.5f;
        private const float MaxLeadRollDegrees = 2.5f;
        private const float MaxTotalRollDegrees = 3.5f;

        // An exact critically damped spring in closed form: from rest with velocity v it
        // peaks at exactly v/(w*e) after 1/w seconds, so the impulse scale below makes
        // PunchFov(2f) peak at two degrees regardless of frame rate.
        private const float FovPunchStiffness = 14f;
        private const float FovPunchImpulse = FovPunchStiffness * 2.71828183f;

        // Framing drift is OFF. v0.4.0's framing was verified on a real device, and a
        // depth-proportional widen would pull the frame open by more than half a degree
        // even at run start, shrinking every body. Turn it on only with a fresh pass.
        private const float FramingFovPerMeterOfDepth = 0f;
        private const float TelegraphFovLeanIn = -2.5f;
        private const float FramingRate = 1.4f;

        // Same closed-form critically damped spring as the fov punch, tuned stiffer so
        // the frame rocks and recovers rather than wallowing.
        private const float KickStiffness = 18f;
        private const float KickImpulse = KickStiffness * 2.71828183f;
        private const float MaxKickMetres = 0.35f;

        private const float TraumaDecayPerSecond = 1.6f;
        private const float ShakeFrequency = 22f;
        private const float MaxShakePitchDegrees = 1.5f;
        private const float MaxShakeYawDegrees = 2.2f;
        private const float MaxShakeRollDegrees = 1.0f;
        private const float MaxShakeOffsetMeters = 0.12f;

        private CrowdController _crowd;
        private UnityEngine.Camera _camera;

        private Vector3 _basePosition;
        private Quaternion _baseRotation;
        private float _trauma;
        private float _shakeTime;
        private float _fovPunch;
        private float _fovPunchVelocity;
        private float _framingBias;
        private float _telegraph;
        private float _lastCenterX;
        private float _lateralSpeed;
        private float _kickY;
        private float _kickVelocity;

        public UnityEngine.Camera Camera => _camera;

        /// <summary>Current trauma, 0-1. Shake amplitude is its square.</summary>
        public float Trauma => _trauma;

        public void Initialize(CrowdController crowd)
        {
            _crowd = crowd;
            _camera = gameObject.AddComponent<UnityEngine.Camera>();
            _camera.fieldOfView = BaseFieldOfView;
            _camera.nearClipPlane = 0.3f;
            _camera.farClipPlane = 220f;
            _camera.clearFlags = RenderSettings.skybox != null
                ? CameraClearFlags.Skybox
                : CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.05f, 0.045f, 0.08f);
            SnapToCrowd();
        }

        /// <summary>
        /// Add trauma, 0-1. Amplitude is trauma SQUARED, which is what separates a shake
        /// from a jitter: small events barely register and big ones hit hard, and the
        /// tail decays away quietly instead of stopping dead.
        /// </summary>
        public void AddTrauma(float amount) => _trauma = Mathf.Clamp01(_trauma + amount);

        /// <summary>
        /// Apply a whole impulse. Keeping Trauma and KickY together means a caller cannot
        /// shake the frame without also rocking it in the right direction.
        /// </summary>
        public void Apply(BattleRunner.Core.Feel.CameraFeel feel)
        {
            AddTrauma(feel.Trauma);
            Kick(feel.KickY);
        }

        /// <summary>Signed vertical rock, in metres. Springs back the same way the fov does.</summary>
        public void Kick(float metres)
        {
            if (metres == 0f) return;
            _kickVelocity += metres * KickImpulse;
        }

        /// <summary>Punch the field of view; positive widens. Punches compose.</summary>
        public void PunchFov(float degrees)
        {
            if (degrees == 0f) return;
            _fovPunchVelocity += degrees * FovPunchImpulse;
        }

        /// <summary>0..1 boss wind-up. Leans the frame in; release it on the strike.</summary>
        public void SetTelegraph(float intensity) => _telegraph = Mathf.Clamp01(intensity);

        public void ResetJuice()
        {
            _trauma = 0f;
            _shakeTime = 0f;
            _fovPunch = 0f;
            _fovPunchVelocity = 0f;
            _kickY = 0f;
            _kickVelocity = 0f;
            _framingBias = 0f;
            _telegraph = 0f;
            _lateralSpeed = 0f;
            if (_camera != null) _camera.fieldOfView = BaseFieldOfView;
        }

        public void SnapToCrowd()
        {
            if (_crowd == null) return;
            ResetJuice();
            _lastCenterX = _crowd.CenterX;
            _basePosition = TargetPosition();
            _baseRotation = Quaternion.LookRotation(LookTarget() - _basePosition);
            transform.SetPositionAndRotation(_basePosition, _baseRotation);
        }

        private void LateUpdate()
        {
            if (_crowd == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            float k = 1f - Mathf.Exp(-FollowRate * dt);
            _basePosition = Vector3.Lerp(_basePosition, TargetPosition(), k);
            _baseRotation = Quaternion.Slerp(_baseRotation,
                Quaternion.LookRotation(LookTarget() - _basePosition), k);

            float rawLateral = (_crowd.CenterX - _lastCenterX) / dt;
            _lastCenterX = _crowd.CenterX;
            _lateralSpeed = Mathf.Lerp(_lateralSpeed, rawLateral,
                1f - Mathf.Exp(-LateralSmoothRate * dt));

            float leadYaw = Mathf.Clamp(_lateralSpeed * LeadYawPerLateralSpeed,
                -MaxLeadYawDegrees, MaxLeadYawDegrees);
            float leadRoll = Mathf.Clamp(-_lateralSpeed * RollPerLateralSpeed,
                -MaxLeadRollDegrees, MaxLeadRollDegrees);

            _trauma = Mathf.Max(0f, _trauma - TraumaDecayPerSecond * dt);
            _shakeTime += dt * ShakeFrequency;
            float s = _trauma * _trauma;
            float n0 = Noise(0f), n1 = Noise(17f), n2 = Noise(31f), n3 = Noise(43f), n4 = Noise(59f);

            // WORLD x and y only, never z. The despawn plane is derived from
            // SetbackMeters as a constant, so inventing world-z motion here would let a
            // gate pop out of existence in front of the player. Rotational shake moves
            // the position by nothing at all, which is why most of the shake lives there.
            float ke = Mathf.Exp(-KickStiffness * dt);
            float ka = _kickVelocity + KickStiffness * _kickY;
            _kickY = Mathf.Clamp((_kickY + ka * dt) * ke, -MaxKickMetres, MaxKickMetres);
            _kickVelocity = (_kickVelocity - KickStiffness * ka * dt) * ke;

            transform.SetPositionAndRotation(
                _basePosition + new Vector3(n0 * s * MaxShakeOffsetMeters,
                                            n1 * s * MaxShakeOffsetMeters + _kickY, 0f),
                _baseRotation * Quaternion.Euler(
                    n2 * s * MaxShakePitchDegrees,
                    n3 * s * MaxShakeYawDegrees + leadYaw,
                    Mathf.Clamp(n4 * s * MaxShakeRollDegrees + leadRoll,
                                -MaxTotalRollDegrees, MaxTotalRollDegrees)));

            TickFieldOfView(dt);
        }

        // Perlin rather than Random: successive samples are correlated, so the camera
        // sweeps instead of vibrating. Independent seeds keep the axes from moving as one.
        private float Noise(float seed) => Mathf.PerlinNoise(seed, _shakeTime) * 2f - 1f;

        private void TickFieldOfView(float dt)
        {
            float e = Mathf.Exp(-FovPunchStiffness * dt);
            float a = _fovPunchVelocity + FovPunchStiffness * _fovPunch;
            _fovPunch = (_fovPunch + a * dt) * e;
            _fovPunchVelocity = (_fovPunchVelocity - FovPunchStiffness * a * dt) * e;

            float crowdDepth = _crowd.FrontZ - _crowd.CenterZ;
            float wanted = crowdDepth * FramingFovPerMeterOfDepth + _telegraph * TelegraphFovLeanIn;
            _framingBias = Mathf.Lerp(_framingBias, wanted, 1f - Mathf.Exp(-FramingRate * dt));

            _camera.fieldOfView = Mathf.Clamp(BaseFieldOfView + _fovPunch + _framingBias,
                MinFieldOfView, MaxFieldOfView);
        }

        // Portrait chase framing: ~11 degrees of pitch. Unchanged from v0.4.0, which was
        // verified on a device — the juice layer is added around it, not instead of it.
        private Vector3 TargetPosition() =>
            new Vector3(_crowd.CenterX * 0.85f, 5.5f, _crowd.CenterZ - SetbackMeters);

        private Vector3 LookTarget() =>
            new Vector3(_crowd.CenterX * 0.85f, 1.5f, _crowd.CenterZ + 10f);
    }
}
