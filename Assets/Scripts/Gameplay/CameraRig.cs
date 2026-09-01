using BattleRunner.Gameplay.Crowd;
using UnityEngine;

namespace BattleRunner.Gameplay
{
    /// <summary>Portrait chase camera: behind and above the crowd, soft x-follow.</summary>
    public sealed class CameraRig : MonoBehaviour
    {
        /// <summary>How far behind the crowd the camera sits. Anything further back than this
        /// is off-screen, which is what makes it safe to despawn there.</summary>
        public const float SetbackMeters = 10f;

        private CrowdController _crowd;
        private UnityEngine.Camera _camera;

        public UnityEngine.Camera Camera => _camera;

        public void Initialize(CrowdController crowd)
        {
            _crowd = crowd;
            _camera = gameObject.AddComponent<UnityEngine.Camera>();
            _camera.fieldOfView = 60f;
            _camera.nearClipPlane = 0.3f;
            _camera.farClipPlane = 220f;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.05f, 0.045f, 0.08f);
            SnapToCrowd();
        }

        public void SnapToCrowd()
        {
            if (_crowd == null) return;
            transform.position = TargetPosition();
            transform.rotation = Quaternion.LookRotation(LookTarget() - transform.position);
        }

        private void LateUpdate()
        {
            if (_crowd == null) return;
            transform.position = Vector3.Lerp(transform.position, TargetPosition(), Time.deltaTime * 5f);
            Quaternion look = Quaternion.LookRotation(LookTarget() - transform.position);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, Time.deltaTime * 5f);
        }

        // Portrait chase framing: ~11 degrees of pitch, not 25. At 8.5m height the
        // ground filled 86% of a 1080x1920 frame and units were seen almost from
        // overhead; this puts sky behind the crowd's heads and gates mid-screen.
        private Vector3 TargetPosition() =>
            new Vector3(_crowd.CenterX * 0.85f, 5.5f, _crowd.CenterZ - SetbackMeters);

        private Vector3 LookTarget() =>
            new Vector3(_crowd.CenterX * 0.85f, 1.5f, _crowd.CenterZ + 10f);
    }
}
