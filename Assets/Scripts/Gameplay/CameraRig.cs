using BattleRunner.Gameplay.Crowd;
using UnityEngine;

namespace BattleRunner.Gameplay
{
    /// <summary>Portrait chase camera: behind and above the crowd, soft x-follow.</summary>
    public sealed class CameraRig : MonoBehaviour
    {
        private CrowdController _crowd;
        private UnityEngine.Camera _camera;

        public UnityEngine.Camera Camera => _camera;

        public void Initialize(CrowdController crowd)
        {
            _crowd = crowd;
            _camera = gameObject.AddComponent<UnityEngine.Camera>();
            _camera.fieldOfView = 65f;
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

        private Vector3 TargetPosition() =>
            new Vector3(_crowd.CenterX * 0.35f, 8.5f, _crowd.CenterZ - 10.5f);

        private Vector3 LookTarget() =>
            new Vector3(_crowd.CenterX * 0.6f, 0.8f, _crowd.CenterZ + 6f);
    }
}
