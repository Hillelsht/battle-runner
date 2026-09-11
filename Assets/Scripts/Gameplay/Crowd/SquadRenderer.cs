using System.Collections.Generic;
using BattleRunner.Core.Run;
using BattleRunner.Gameplay.Track;
using UnityEngine;

namespace BattleRunner.Gameplay.Crowd
{
    /// <summary>
    /// Draws every enemy squad on the level, and the detachment of the player's own soldiers
    /// that runs out to fight them, as instanced soldiers rather than GameObjects.
    ///
    /// THE GAMEPLAY FIX IS ALSO THE PERFORMANCE FIX. A render audit found a typical frame
    /// submitting roughly 200-230 draw calls against doc 04's ceiling of 120, and the single
    /// worst contributor after the gates was enemy packs: five separate MeshRenderers each,
    /// about twenty-five draws a level. Every squad in the level is now TWO draws — one for
    /// the enemies, one for the fighters — and they are bigger, countable and animated as
    /// well as cheaper.
    ///
    /// WHY THE FIGHTERS ARE DRAWN HERE AND NOT DETACHED FROM THE CROWD ITSELF. The obvious
    /// design is a detached mode in CrowdController: let real units leave their formation
    /// slots and come back. But CrowdController's formation is the most load-bearing maths in
    /// the game — its width and tail invariants are pinned by tests, and the crowd's scale is
    /// CI-enforced against the shader that decodes the bob phase out of it. Spending that risk
    /// buys nothing the player can see, because a soldier who leaves the front rank of a
    /// five-hundred-strong blob leaves no visible hole. Drawing the detachment as extra
    /// instances in the gap reads identically and touches none of it.
    /// </summary>
    public sealed class SquadRenderer : MonoBehaviour
    {
        /// <summary>Enemy squads plus their fighters, at the display cap, with headroom.</summary>
        private const int MaxInstances = 512;

        /// <summary>Matches CrowdRenderer so an enemy is the same size as a soldier.</summary>
        private const float BodyScale = EnemyPackBehaviour.BodyScale;

        /// <summary>
        /// How far in front of the army the fighting happens. Far enough that the clash is
        /// not inside the crowd's own silhouette, near enough that it is clearly the player's
        /// army doing it rather than a third party.
        /// </summary>
        private const float EngageOffset = 1.9f;

        private readonly Matrix4x4[] _enemies = new Matrix4x4[MaxInstances];
        private readonly Matrix4x4[] _fighters = new Matrix4x4[MaxInstances];

        private CrowdController _crowd;
        private Material _enemyMaterial;
        private Material _allyMaterial;
        private Mesh _enemyMesh;
        private Mesh _fighterMesh;
        private List<EnemyPackBehaviour> _squads;
        private bool _instancing;

        public void Initialize(CrowdController crowd, Material enemyMaterial, Material allyMaterial,
            List<EnemyPackBehaviour> squads)
        {
            _crowd = crowd;
            _enemyMaterial = enemyMaterial;
            _allyMaterial = allyMaterial;
            _squads = squads;
            // Axe for the enemy, shield for the detachment: two silhouettes that read apart at
            // a glance even before the colours do, which matters most in the half second the
            // two crowds are interpenetrating.
            _enemyMesh = ProceduralMeshes.Soldier(ProceduralMeshes.SoldierKind.Axe);
            _fighterMesh = ProceduralMeshes.Soldier(ProceduralMeshes.SoldierKind.Shield);

            if (_enemyMaterial != null && !_enemyMaterial.enableInstancing)
                _enemyMaterial.enableInstancing = true;
            if (_allyMaterial != null && !_allyMaterial.enableInstancing)
                _allyMaterial.enableInstancing = true;

            _instancing = SystemInfo.supportsInstancing
                          && _enemyMaterial != null && _enemyMaterial.shader != null
                          && _enemyMaterial.shader.isSupported;
        }

        /// <summary>
        /// Where the n-th soldier of a squad of `count` stands, in squad-local space.
        ///
        /// Deliberately NOT CrowdMath.FormationSlot: that spiral is tuned to keep a
        /// five-hundred-strong army inside a 2.2 m lane, and a squad of nine wants to look
        /// like nine men standing in a road, not like a scale model of an army. Rows of five,
        /// staggered, with the odd rank offset so it does not read as a grid.
        /// </summary>
        private static Vector3 SquadSlot(int index, int count)
        {
            const int PerRow = 5;
            const float Across = 0.34f;
            const float Deep = 0.42f;
            int row = index / PerRow;
            int column = index % PerRow;
            int inThisRow = Mathf.Min(PerRow, count - row * PerRow);
            float x = (column - (inThisRow - 1) * 0.5f) * Across;
            float z = row * Deep;
            if ((row & 1) == 1) x += Across * 0.5f;
            return new Vector3(x, 0f, z);
        }

        /// <summary>
        /// A deterministic 0..1 per body, so yaw and lunge phase differ between neighbours
        /// without a random draw that would reshuffle every frame.
        /// </summary>
        private static float Jitter(int a, int b)
        {
            uint h = (uint)(a * 73856093) ^ (uint)(b * 19349663);
            h ^= h >> 13;
            h *= 2654435761u;
            h ^= h >> 16;
            return (h & 0xFFFFu) / 65535f;
        }

        private void LateUpdate()
        {
            if (_squads == null || _enemyMaterial == null || _crowd == null) return;

            int enemyCount = 0;
            int fighterCount = 0;
            float now = Time.time;

            for (int s = 0; s < _squads.Count; s++)
            {
                EnemyPackBehaviour squad = _squads[s];
                if (squad == null || squad.DisplayedCount <= 0) continue;

                Vector3 origin = squad.transform.position;
                int bodies = Mathf.Min(squad.DisplayedCount, MaxInstances - enemyCount);

                // While fighting, the two lines press into each other. A squad that stands
                // still while the army arrives reads as scenery being deleted, not as a fight.
                float press = 0f;
                float clash = 0f;
                if (squad.Fighting)
                {
                    float t = Mathf.Clamp01(squad.FightElapsed / Melee.Duration);
                    press = Mathf.Sin(t * Mathf.PI) * 0.55f;
                    clash = 1f;
                }

                for (int i = 0; i < bodies; i++)
                {
                    Vector3 local = SquadSlot(i, squad.DisplayedCount);
                    float seed = Jitter(s, i);
                    // Enemies face the oncoming army (-Z), swaying a little.
                    float yaw = 180f + (seed - 0.5f) * 26f;
                    float lunge = 0f;
                    if (clash > 0f)
                    {
                        // Each body swings on its own clock, so the line boils rather than
                        // pulsing in unison — unison is what makes a crowd read as one object.
                        float beat = (now * 6.5f + seed * 6.283f);
                        lunge = Mathf.Sin(beat) * 0.16f;
                        yaw += Mathf.Sin(beat * 0.5f) * 18f;
                    }
                    Vector3 pos = origin + local + new Vector3(0f, 0f, -press + lunge);
                    _enemies[enemyCount++] = Matrix4x4.TRS(pos,
                        Quaternion.Euler(0f, yaw, 0f), Vector3.one * BodyScale);
                    if (enemyCount >= MaxInstances) break;
                }

                if (!squad.Fighting || fighterCount >= MaxInstances) continue;

                // The detachment. Drawn between the army's front and the squad, running in at
                // the start of the clash and pushing forward through it.
                squad.Clash.At(squad.FightElapsed, out long alliesLeft, out _);
                int fighters = squad.Clash.Fighters((int)Mathf.Min(alliesLeft, int.MaxValue));
                fighters = Mathf.Min(fighters, MaxInstances - fighterCount);
                float into = Mathf.Clamp01(squad.FightElapsed / (Melee.Duration * 0.35f));

                for (int i = 0; i < fighters; i++)
                {
                    Vector3 local = SquadSlot(i, fighters);
                    float seed = Jitter(s + 977, i);
                    float beat = now * 7.0f + seed * 6.283f;
                    // Start at the army's front rank, arrive at the enemy's face.
                    float startZ = _crowd.FrontZ;
                    float endZ = origin.z - EngageOffset + press;
                    float z = Mathf.Lerp(startZ, endZ, into) - local.z + Mathf.Sin(beat) * 0.16f;
                    var pos = new Vector3(origin.x + local.x * 0.9f, 0f, z);
                    float yaw = Mathf.Sin(beat * 0.5f) * 20f;
                    _fighters[fighterCount++] = Matrix4x4.TRS(pos,
                        Quaternion.Euler(0f, yaw, 0f), Vector3.one * BodyScale);
                }
            }

            if (enemyCount > 0) Submit(_enemies, enemyCount, _enemyMesh, _enemyMaterial);
            if (fighterCount > 0) Submit(_fighters, fighterCount, _fighterMesh,
                _allyMaterial != null ? _allyMaterial : _enemyMaterial);
        }

        private void Submit(Matrix4x4[] matrices, int count, Mesh mesh, Material material)
        {
            if (mesh == null || material == null) return;
            // Bounds around the whole visible stretch of road. An instanced draw whose bounds
            // do not contain its instances is culled entire, and a squad vanishing as the
            // camera turns is far worse than a slightly generous box.
            var bounds = new Bounds(new Vector3(0f, 1f, _crowd.CenterZ + 60f),
                new Vector3(40f, 6f, 260f));
            var rp = new RenderParams(material)
            {
                worldBounds = bounds,
                shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On,
                receiveShadows = true
            };

            if (_instancing)
            {
                Graphics.RenderMeshInstanced(rp, mesh, 0, matrices, count);
                return;
            }
            for (int i = 0; i < count; i++) Graphics.RenderMesh(rp, mesh, 0, matrices[i]);
        }
    }
}
