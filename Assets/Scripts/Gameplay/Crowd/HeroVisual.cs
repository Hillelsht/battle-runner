using BattleRunner.Core.Crowd;
using UnityEngine;

namespace BattleRunner.Gameplay.Crowd
{
    /// <summary>
    /// The one unit that IS a GameObject: the gear-carrying Hero leading the crowd.
    /// Above the render cap the hero's scale expresses growth the bodies can't (R2).
    /// </summary>
    public sealed class HeroVisual : MonoBehaviour
    {
        private CrowdController _crowd;
        private Transform _visual;
        private int _tierCap = 200;

        public void Initialize(CrowdController crowd, Mesh unitMesh, Material heroMaterial, int tierCap)
        {
            _crowd = crowd;
            _tierCap = tierCap;

            var visual = new GameObject("HeroMesh", typeof(MeshFilter), typeof(MeshRenderer));
            visual.transform.SetParent(transform, false);
            visual.GetComponent<MeshFilter>().sharedMesh = unitMesh;
            var renderer = visual.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = heroMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _visual = visual.transform;
        }

        private void LateUpdate()
        {
            if (_crowd == null) return;
            // 1.2x reads as "the leader"; 1.6x read as a separate boss figure.
            float scale = 1.2f * CrowdMath.HeroScaleFor(_crowd.ForceCount, _tierCap);
            transform.position = new Vector3(_crowd.CenterX, 0f, _crowd.CenterZ + 0.6f);
            _visual.localScale = new Vector3(scale, scale, scale);
        }
    }
}
