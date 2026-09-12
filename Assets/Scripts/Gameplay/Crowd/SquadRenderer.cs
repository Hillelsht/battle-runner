using System.Collections.Generic;
using BattleRunner.Core.Boss;
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
    /// about twenty-five draws a level. Every squad in the level is now THREE draws at the
    /// absolute worst — standing enemies, brawling enemies, and the detachment — and the
    /// middle one exists only while a clash is on screen. They are bigger, countable and
    /// animated as well as cheaper.
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

        /// <summary>
        /// The walk phase, encoded where the shader reads it.
        ///
        /// CrowdInstanced.shader recovers each body's stride from its uniform SCALE — there is
        /// no second per-instance channel, only the matrix, so the 0.44-0.50 window is the
        /// whole animation bus. Every squad body was drawn at a flat 0.47, which is the exact
        /// middle of that window, so the shader decoded phase 0.5 for all of them and a squad
        /// marched as one synchronised band: every left leg forward on the same frame. The
        /// player's own army has looked right the whole time because CrowdRenderer varies it.
        ///
        /// The 6% size spread this introduces is the same spread the army already has, and it
        /// is the reason the army does not read as a photocopy either.
        /// </summary>
        private static float PhasedScale(float jitter) =>
            CrowdRenderer.ScaleMin + jitter * CrowdRenderer.ScaleSpan;

        /// <summary>
        /// How far in front of the army the fighting happens. Far enough that the clash is
        /// not inside the crowd's own silhouette, near enough that it is clearly the player's
        /// army doing it rather than a third party.
        /// </summary>
        private const float EngageOffset = 1.9f;

        /// <summary>How hard a brawling enemy works its legs. See _brawlMaterial.</summary>
        private const float BrawlBob = 0.10f;

        private readonly Matrix4x4[] _enemies = new Matrix4x4[MaxInstances];
        /// <summary>The enemies of squads currently in a clash. See _brawlMaterial.</summary>
        private readonly Matrix4x4[] _brawlers = new Matrix4x4[MaxInstances];
        private readonly Matrix4x4[] _fighters = new Matrix4x4[MaxInstances];

        private CrowdController _crowd;
        private Material _enemyMaterial;
        /// <summary>
        /// The same red, with legs.
        ///
        /// `enemyMaterial._BobAmount` is 0 (GameBootstrap), which is correct for a squad
        /// STANDING in the road waiting — a man at a halt should not be walking on the spot.
        /// It was also applied while the two lines were inside each other, so in every clash
        /// the player's soldiers animated and the red side was a rigid statue sliding along
        /// the road. Two materials, and the split costs ONE extra instanced draw that exists
        /// only while a clash is actually on screen.
        ///
        /// 0.10 rather than the crowd's own amount: this is men trading blows on the spot,
        /// not men running. The stride wants to read as bracing, not as a march.
        /// </summary>
        private Material _brawlMaterial;
        private Material _allyMaterial;
        private Mesh _enemyMesh;
        private Mesh _fighterMesh;
        private List<EnemyPackBehaviour> _squads;
        private List<GateBehaviour> _gates;
        private readonly Matrix4x4[] _allies = new Matrix4x4[MaxInstances];
        private bool _instancing;

        // --- the boss skirmish ------------------------------------------------
        //
        // During a boss round there are no enemy squads on the level, so _fighters is an
        // array of 512 matrices sitting idle for the entire encounter. The skirmish fills
        // exactly that array, which is why a line of thirty men fighting a boss costs ZERO
        // additional draw calls — doc 04's ceiling is 120 and this is the increment that
        // could most easily have blown through it.
        private bool _bossSkirmish;
        private Vector3 _bossFoot;
        private float _bossFightSeconds;
        private int _bossFighters;
        /// <summary>When the boss killed fighter i, or -1. Indexed by skirmish slot.</summary>
        private readonly float[] _bossDownAt = new float[BossMelee.MaxFighters];

        /// <summary>
        /// Draw a line of the army's soldiers fighting the boss.
        ///
        /// Called every frame of an encounter by BossEncounterState, which owns the clock —
        /// this component owns no fight state of its own beyond who is down, so a fight that
        /// ends mid-frame cannot leave a body standing in an empty arena.
        /// </summary>
        public void SetBossSkirmish(Vector3 bossFoot, float fightSeconds, double force)
        {
            if (!_bossSkirmish)
            {
                for (int i = 0; i < _bossDownAt.Length; i++) _bossDownAt[i] = -1f;
                _bossSkirmish = true;
            }
            _bossFoot = bossFoot;
            _bossFightSeconds = fightSeconds;
            _bossFighters = Mathf.Min(BossMelee.Fighters(force), BossMelee.MaxFighters);
        }

        /// <summary>
        /// The boss's blow just landed. Take some of the line off its feet.
        ///
        /// A blow that removes force and nothing else is a number. This is what makes it a
        /// thing that happened to somebody: the men nearest the boss go down, and stay down.
        /// </summary>
        public void BossKilled(int howMany)
        {
            if (!_bossSkirmish || howMany <= 0) return;
            int killed = 0;
            // Walk from a rotating start so it is not always the same slots that die, which
            // would leave a permanent gap at one end of the line.
            int start = (int)(_bossFightSeconds * 3.7f) % Mathf.Max(1, _bossFighters);
            for (int n = 0; n < _bossFighters && killed < howMany; n++)
            {
                int i = (start + n) % _bossFighters;
                if (_bossDownAt[i] >= 0f) continue;
                _bossDownAt[i] = _bossFightSeconds;
                killed++;
            }
            // Everyone is down and the fight is still going: the line re-forms. The army has
            // hundreds of men and the detachment is a detachment, not the last of them.
            if (killed < howMany || AllDown())
                for (int i = 0; i < _bossDownAt.Length; i++) _bossDownAt[i] = -1f;
        }

        private bool AllDown()
        {
            for (int i = 0; i < _bossFighters; i++)
                if (_bossDownAt[i] < 0f) return false;
            return true;
        }

        public void ClearBossSkirmish()
        {
            _bossSkirmish = false;
            _bossFighters = 0;
        }

        /// <summary>
        /// The most bodies drawn for one gate crowd, on either sign. A +40 is already a wall
        /// of men across a 1.9 m lane; past that the number over their heads carries the size,
        /// exactly as it does for an enemy squad.
        /// </summary>
        private const int AllyDisplayCap = 28;

        public void Initialize(CrowdController crowd, Material enemyMaterial, Material allyMaterial,
            List<EnemyPackBehaviour> squads, List<GateBehaviour> gates)
        {
            _gates = gates;
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
            if (_enemyMaterial != null)
            {
                _brawlMaterial = ShaderSafety.CreateMaterial(_enemyMaterial);
                _brawlMaterial.SetFloatSafe("_BobAmount", BrawlBob);
                _brawlMaterial.enableInstancing = true;
            }
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
            int brawlCount = 0;
            int fighterCount = 0;
            float now = Time.time;

            for (int s = 0; s < _squads.Count; s++)
            {
                EnemyPackBehaviour squad = _squads[s];
                if (squad == null || squad.DisplayedCount <= 0) continue;

                Vector3 origin = squad.transform.position;
                // A squad in a clash goes into the brawl bucket, which is the one with legs.
                Matrix4x4[] bucket = squad.Fighting ? _brawlers : _enemies;
                int already = squad.Fighting ? brawlCount : enemyCount;
                int bodies = Mathf.Min(squad.DisplayedCount, MaxInstances - already);

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
                    bucket[already++] = Matrix4x4.TRS(pos,
                        Quaternion.Euler(0f, yaw, 0f), Vector3.one * PhasedScale(seed));
                    if (already >= MaxInstances) break;
                }

                if (squad.Fighting) brawlCount = already; else enemyCount = already;

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
                        Quaternion.Euler(0f, yaw, 0f), Vector3.one * PhasedScale(seed));
                }
            }

            // Gates that change the army by a COUNT are people, on both signs. An add gate
            // is a reinforcement standing in the lane that breaks and runs into you; a
            // subtract gate is men who stand in the road and take that many of yours down
            // with them, and get ridden down doing it. Only the multiply gate is still an
            // arch, because a x3 is not a number of men.
            int allyCount = 0;
            if (_gates != null)
            {
                for (int g = 0; g < _gates.Count; g++)
                {
                    GateBehaviour gate = _gates[g];
                    if (gate == null || gate.Destroyed || !gate.DrawAsCrowd) continue;
                    if (gate.SinceConsumed >= gate.CrowdSeconds) continue;

                    bool hostile = gate.Op == GateOp.Subtract;

                    // A hostile gate's men go in the ENEMY bucket, and once the clash starts
                    // in the brawling one, so the same split that gave a fighting squad legs
                    // gives these legs too.
                    Matrix4x4[] bucket;
                    int already;
                    if (!hostile) { bucket = _allies; already = allyCount; }
                    else if (gate.SinceConsumed >= 0f) { bucket = _brawlers; already = brawlCount; }
                    else { bucket = _enemies; already = enemyCount; }

                    // FROM THE HEADCOUNT, NOT THE WEIGHT. A gate's authored number is how many
                    // SHARES it is worth now, so drawing `Value` men would put one or two
                    // soldiers in the road in front of an army of a billion. The resolved
                    // headcount is what the sign says, so it is what the crowd must be.
                    long men = BattleRunner.Core.Run.GateMath.Headcount(
                        _crowd.ForceCount, gate.Op, gate.Weight, gate.Depth);
                    if (men == 0L) continue;
                    int bodies = (int)Mathf.Min(Mathf.Abs(men), AllyDisplayCap);
                    if (bodies <= 0) continue;
                    bodies = Mathf.Min(bodies, MaxInstances - already);
                    if (bodies <= 0) continue;

                    Vector3 origin = gate.transform.position;
                    float t = gate.SinceConsumed < 0f
                        ? 0f
                        : Mathf.Clamp01(gate.SinceConsumed / gate.CrowdSeconds);
                    // Cubed, so they hold their ground for a moment and then go all at once —
                    // a linear fade reads as the gate being deleted rather than as men moving.
                    float gone = t * t * t;
                    var muster = new Vector3(_crowd.CenterX, 0f, _crowd.FrontZ);

                    for (int i = 0; i < bodies; i++)
                    {
                        Vector3 local = SquadSlot(i, bodies);
                        float seed = Jitter(g + (hostile ? 8191 : 4093), i);
                        float beat = now * (hostile ? 7.5f : 5f) + seed * 6.283f;
                        Vector3 stand = origin + local;
                        Vector3 pos;
                        float yaw;
                        if (hostile)
                        {
                            // Driven BACKWARD and scattered sideways, not absorbed. Each man
                            // goes his own distance and his own way, so the line comes apart
                            // rather than sliding off as one piece — being overrun is the
                            // only moment in a run where the army visibly pays for something.
                            float shove = (0.9f + seed * 1.6f) * gone;
                            float slew = (seed - 0.5f) * 1.8f * gone;
                            pos = stand + new Vector3(slew, 0f, shove + Mathf.Sin(beat) * 0.12f);
                            // Facing the army, then spun as they go down.
                            yaw = 180f + (seed - 0.5f) * 30f + Mathf.Sin(beat * 0.6f) * 22f
                                  + gone * (seed < 0.5f ? -120f : 120f);
                        }
                        else
                        {
                            pos = Vector3.Lerp(stand, muster, gone);
                            // Facing the player while they wait, turning to march once taken.
                            yaw = Mathf.Lerp(180f + (seed - 0.5f) * 30f, 0f, gone)
                                  + Mathf.Sin(beat) * 5f;
                        }
                        // The shrink-out multiplies the PHASED scale, so a body keeps its own
                        // stride right up to the moment it is gone. (Once the factor is past
                        // ~0.15 the scale leaves the window the shader decodes from and the
                        // phase pins, which is correct: a body that small is two pixels and
                        // its legs are not the thing being read.)
                        float scale = PhasedScale(seed) * (1f - gone * 0.85f);
                        bucket[already++] = Matrix4x4.TRS(pos,
                            Quaternion.Euler(0f, yaw, 0f), Vector3.one * scale);
                        if (already >= MaxInstances) break;
                    }

                    if (!hostile) allyCount = already;
                    else if (gate.SinceConsumed >= 0f) brawlCount = already;
                    else enemyCount = already;
                }
            }

            // THE BOSS SKIRMISH. Into the same bucket the squad fighters use — during a boss
            // round there are no squads, so it is empty and this is free.
            if (_bossSkirmish && _bossFighters > 0)
            {
                float armyZ = _crowd.FrontZ;
                float span = _bossFoot.z - armyZ;
                for (int i = 0; i < _bossFighters && fighterCount < MaxInstances; i++)
                {
                    SkirmishPose sp = BossMelee.Pose(i, _bossFighters, _bossFightSeconds,
                        _bossDownAt[i]);
                    if (sp.Standing <= 0f) continue;     // he is dead; stop drawing him
                    var pos = new Vector3(_bossFoot.x + sp.Across, 0f, armyZ + span * sp.Toward);
                    // Lean is a body pitch, which is the weapon arc: there is no per-instance
                    // channel but the matrix, and a rotation is free inside one.
                    var rot = Quaternion.Euler(sp.Lean, sp.Yaw, 0f);
                    float scale = PhasedScale(Jitter(31337, i)) * (0.35f + 0.65f * sp.Standing);
                    _fighters[fighterCount++] = Matrix4x4.TRS(pos, rot, Vector3.one * scale);
                }
            }

            if (enemyCount > 0) Submit(_enemies, enemyCount, _enemyMesh, _enemyMaterial);
            if (brawlCount > 0) Submit(_brawlers, brawlCount, _enemyMesh,
                _brawlMaterial != null ? _brawlMaterial : _enemyMaterial);
            if (allyCount > 0) Submit(_allies, allyCount, _fighterMesh,
                _allyMaterial != null ? _allyMaterial : _enemyMaterial);
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
