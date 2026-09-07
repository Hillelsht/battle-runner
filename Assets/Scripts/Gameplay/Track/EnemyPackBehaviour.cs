using UnityEngine;

namespace BattleRunner.Gameplay.Track
{
    /// <summary>A pack of hostile units that subtracts force on contact. Pure visual — no physics.</summary>
    public sealed class EnemyPackBehaviour : MonoBehaviour, IPoolable
    {
        public int ForceCost { get; private set; }
        public int Lane { get; private set; }
        public bool Defeated { get; private set; }

        /// <summary>Passed the crowd's plane, scored or not. See GateBehaviour.Resolved.</summary>
        public bool Resolved { get; private set; }

        private TextMesh _label;
        private MeshRenderer _labelRenderer;

        /// <summary>
        /// The scale the player's own soldiers are drawn at: CrowdRenderer's ScaleMin plus
        /// half its ScaleSpan. Enemies used to be drawn at 1.0, which was right when the
        /// crowd was 0.94-1.06 — but the crowd was cut to 0.44-0.50 when the formation was
        /// pinned inside one lane, and the packs were never brought with it. Enemies have
        /// been 2.1x the size of the soldiers running at them ever since, which breaks the
        /// one comparison this whole game is about: is my army bigger than that.
        /// Keep in step with CrowdRenderer.ScaleMin / ScaleSpan.
        /// </summary>
        private const float BodyScale = 0.47f;

        private static readonly Vector3[] ClusterOffsets =
        {
            new Vector3(0f, 0f, 0f),
            new Vector3(-0.6f, 0f, 0.4f),
            new Vector3(0.6f, 0f, 0.4f),
            new Vector3(-0.3f, 0f, -0.5f),
            new Vector3(0.35f, 0f, -0.45f)
        };

        public static EnemyPackBehaviour Build(Mesh unitMesh, Material enemyMaterial, Font font)
        {
            var go = new GameObject("EnemyPack");
            var pack = go.AddComponent<EnemyPackBehaviour>();

            foreach (Vector3 offset in ClusterOffsets)
            {
                var unit = new GameObject("Enemy", typeof(MeshFilter), typeof(MeshRenderer));
                unit.transform.SetParent(go.transform, false);
                unit.transform.localPosition = offset * BodyScale;
                unit.transform.localScale = Vector3.one * BodyScale;
                unit.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                unit.GetComponent<MeshFilter>().sharedMesh = unitMesh;
                var renderer = unit.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = enemyMaterial;
                // Casts: a pack you can see the shadow of reads as an obstacle.
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }

            var labelGo = new GameObject("Label", typeof(TextMesh));
            labelGo.transform.SetParent(go.transform, false);
            // Down with the bodies — 1.8 m was clear of a 1.0-scale enemy's head and is
            // now three body-heights above a 0.47 one.
            labelGo.transform.localPosition = new Vector3(0f, 0.95f, 0f);
            // Readable face is -Z, which is where the camera already is (see GateBehaviour).
            labelGo.transform.localRotation = Quaternion.Euler(12f, 0f, 0f);
            pack._label = labelGo.GetComponent<TextMesh>();
            pack._label.font = font;
            pack._label.fontSize = 56;
            pack._label.characterSize = 0.18f;
            pack._label.anchor = TextAnchor.MiddleCenter;
            pack._label.color = new Color(1f, 0.35f, 0.3f);
            pack._labelRenderer = pack._label.GetComponent<MeshRenderer>();
            pack._labelRenderer.sharedMaterial = font.material;
            pack._labelRenderer.sortingOrder = 1;
            return pack;
        }

        public void Setup(int forceCost, int lane, Vector3 worldPosition)
        {
            ForceCost = forceCost;
            Lane = lane;
            Defeated = false;
            Resolved = false;
            transform.position = worldPosition;
            _label.text = $"-{forceCost}";
        }

        /// <summary>See GateBehaviour.SetLabelVisible — labels draw through geometry and must be culled.</summary>
        public void SetLabelVisible(bool visible)
        {
            if (_labelRenderer != null && _labelRenderer.enabled != visible)
                _labelRenderer.enabled = visible;
        }

        /// <summary>Consumed by contact or destroyed by a spell.</summary>
        public void Defeat() => Defeated = true;

        /// <summary>The crowd has drawn level with this pack; it bites now or never.</summary>
        public void Resolve() => Resolved = true;

        public void OnSpawned() => SetLabelVisible(true);
        public void OnDespawned() { }
    }
}
