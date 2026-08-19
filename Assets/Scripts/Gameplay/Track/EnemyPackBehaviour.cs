using UnityEngine;

namespace BattleRunner.Gameplay.Track
{
    /// <summary>A pack of hostile units that subtracts force on contact. Pure visual — no physics.</summary>
    public sealed class EnemyPackBehaviour : MonoBehaviour, IPoolable
    {
        public int ForceCost { get; private set; }
        public bool Defeated { get; private set; }

        private TextMesh _label;

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
                unit.transform.localPosition = offset;
                unit.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                unit.GetComponent<MeshFilter>().sharedMesh = unitMesh;
                var renderer = unit.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = enemyMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            var labelGo = new GameObject("Label", typeof(TextMesh));
            labelGo.transform.SetParent(go.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 1.8f, 0f);
            labelGo.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            pack._label = labelGo.GetComponent<TextMesh>();
            pack._label.font = font;
            pack._label.fontSize = 56;
            pack._label.characterSize = 0.04f;
            pack._label.anchor = TextAnchor.MiddleCenter;
            pack._label.color = new Color(1f, 0.35f, 0.3f);
            pack._label.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            return pack;
        }

        public void Setup(int forceCost, Vector3 worldPosition)
        {
            ForceCost = forceCost;
            Defeated = false;
            transform.position = worldPosition;
            _label.text = $"-{forceCost}";
        }

        /// <summary>Consumed by contact or destroyed by a spell.</summary>
        public void Defeat() => Defeated = true;

        public void OnSpawned() { }
        public void OnDespawned() { }
    }
}
