using BattleRunner.Core.Crowd;
using BattleRunner.Core.Heroes;
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
        private MeshFilter _filter;
        private Material _material;
        private int _tierCap = 200;

        public void Initialize(CrowdController crowd, Mesh unitMesh, Material heroMaterial, int tierCap)
        {
            _crowd = crowd;
            _tierCap = tierCap;

            var visual = new GameObject("HeroMesh", typeof(MeshFilter), typeof(MeshRenderer));
            visual.transform.SetParent(transform, false);
            _filter = visual.GetComponent<MeshFilter>();
            _filter.sharedMesh = unitMesh;
            var renderer = visual.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = heroMaterial;
            _material = heroMaterial;
            // Casts: the hero leads the column and needs to sit in it.
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            _visual = visual.transform;
        }

        /// <summary>
        /// Become a different hero.
        ///
        /// A RE-SKIN RATHER THAN A CONSTRUCTOR ARGUMENT because of when the two things
        /// happen: the hero object is built during bootstrap, before any save slot is
        /// active, so at the moment Initialize runs nobody has chosen anything yet. The
        /// alternative — deferring the whole GameObject until after character select —
        /// would leave every other system holding a null Hero for the first few states.
        ///
        /// It MUTATES the shared hero material rather than instancing a new one, so the
        /// shield ward keeps pointing at the same object it was handed. That is also why
        /// the ward has to be told to re-read its rest colour afterwards: it caches one.
        /// </summary>
        public void Wear(HeroProfile profile, Mesh mesh)
        {
            if (_filter != null && mesh != null) _filter.sharedMesh = mesh;
            if (_material == null) return;
            _material.SetColorSafe("_BaseColor", ThemePalette.ToColor(profile.Body));
            _material.SetColorSafe("_EmissionColor", ThemePalette.ToColor(profile.Glow));
        }

        private void LateUpdate()
        {
            if (_crowd == null) return;
            // 1.35x reads as "the leader"; 1.6x read as a separate boss figure.
            float scale = 1.35f * CrowdMath.HeroScaleFor(_crowd.ForceCount, _tierCap);
            // Stand on the crowd's leading plane rather than a fixed 0.6 m. With the old
            // disc reaching 3.48 m ahead of the centroid, a hero pinned at +0.6 was 2.9 m
            // INSIDE the crowd with ~120 of 200 bodies drawn in front of it.
            transform.position = new Vector3(_crowd.CenterX, 0f, _crowd.FrontZ);
            _visual.localScale = new Vector3(scale, scale, scale);
        }
    }
}
