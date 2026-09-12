using BattleRunner.Core.Run;
using UnityEngine;

namespace BattleRunner.Gameplay.Track
{
    /// <summary>
    /// A hostile SQUAD: as many soldiers as the number over their heads, drawn instanced,
    /// who fight the army for about a second when it reaches them.
    ///
    /// WHAT THIS REPLACED, AND WHY IT HAD TO GO. A pack was five frozen bodies with a `-26`
    /// over them, in five separate MeshRenderers, and the frame the crowd's leading plane
    /// touched it the whole thing was subtracted and released to the pool. Two complaints at
    /// once: "-26" drawn over five men is a number that contradicts what it is labelling, and
    /// a fight that is one frame long cannot be animated because there is nothing to animate.
    ///
    /// So a squad now has N soldiers in it, up to a display cap, and it is drawn through the
    /// SAME instanced path as the player's army — see SquadRenderer. That is also the single
    /// largest draw-call saving available in the project: a typical level had about twenty-five
    /// draws in five-body packs, and they are now two.
    ///
    /// THE FIGHT IS PACED, NOT REBALANCED. Core/Run/Melee spreads the same `force -= cost` the
    /// game always did across 1.15 seconds, so every tuned difficulty number survives untouched
    /// while the army visibly shrinks as it fights instead of teleporting to its new size.
    /// </summary>
    public sealed class EnemyPackBehaviour : MonoBehaviour, IPoolable
    {
        /// <summary>
        /// The most bodies drawn for one squad. Past this the count over their heads carries
        /// the size — nine hundred soldiers in a lane is a wall, not a squad, and it would
        /// cost more instances than the player's entire army.
        /// </summary>
        public const int DisplayCap = 40;

        /// <summary>
        /// The pack's WEIGHT on GateMath's ambush curve, not a headcount. A pack costs a
        /// share of the army it meets, like a red gate does, so the same pack is a real
        /// threat at forty men and at forty billion — where the old absolute cost of
        /// `3 + 2*difficulty + 2*step` was a rounding error the moment the army passed a
        /// few hundred, which is most of why packs stopped being felt at all.
        /// </summary>
        public int Weight { get; private set; }

        /// <summary>Which chunk of the round this pack stands in; packs bite harder deeper in.</summary>
        public int Depth { get; private set; }

        /// <summary>How many men this pack is showing itself as, resolved against the army.</summary>
        public long Headcount { get; private set; }
        public int Lane { get; private set; }
        public bool Defeated { get; private set; }

        /// <summary>Passed the crowd's plane, scored or not. See GateBehaviour.Resolved.</summary>
        public bool Resolved { get; private set; }

        /// <summary>Non-zero while this squad is being fought. Drives the whole clash.</summary>
        public float FightElapsed { get; private set; }
        public bool Fighting { get; private set; }
        public Melee Clash { get; private set; }

        /// <summary>How many bodies to draw this frame — drains as the squad loses.</summary>
        public int DisplayedCount { get; private set; }

        private TextMesh _label;
        private MeshRenderer _labelRenderer;
        private Transform _labelPivot;

        /// <summary>
        /// The scale the player's own soldiers are drawn at: CrowdRenderer's ScaleMin plus
        /// half its ScaleSpan. Enemies were drawn at 1.0 when the crowd was 0.94-1.06, and
        /// the crowd was cut to 0.44-0.50 when the formation was pinned inside one lane
        /// without bringing the packs with it — so enemies spent a long time at 2.1x the size
        /// of the soldiers running at them, which breaks the one comparison this whole game is
        /// about. Keep in step with CrowdRenderer.ScaleMin / ScaleSpan.
        /// </summary>
        public const float BodyScale = 0.47f;

        public static EnemyPackBehaviour Build(Mesh unitMesh, Material enemyMaterial, Font font)
        {
            var go = new GameObject("EnemySquad");
            var squad = go.AddComponent<EnemyPackBehaviour>();

            // NO BODY RENDERERS. The bodies are instanced by SquadRenderer; this object is a
            // position, a count, and a number floating over it.
            var pivot = new GameObject("LabelPivot");
            pivot.transform.SetParent(go.transform, false);
            squad._labelPivot = pivot.transform;

            var labelGo = new GameObject("Label", typeof(TextMesh));
            labelGo.transform.SetParent(pivot.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 1.15f, 0f);
            squad._label = labelGo.GetComponent<TextMesh>();
            squad._label.font = font;
            squad._label.fontSize = 56;
            squad._label.characterSize = 0.20f;
            squad._label.anchor = TextAnchor.MiddleCenter;
            squad._label.color = new Color(1f, 0.35f, 0.3f);
            squad._labelRenderer = squad._label.GetComponent<MeshRenderer>();
            squad._labelRenderer.sharedMaterial = font.material;
            squad._labelRenderer.sortingOrder = 1;
            return squad;
        }

        public void Setup(int weight, int lane, int depth, Vector3 worldPosition)
        {
            Weight = Mathf.Max(0, weight);
            Depth = Mathf.Max(0, depth);
            Lane = lane;
            Defeated = false;
            Resolved = false;
            Fighting = false;
            FightElapsed = 0f;
            Clash = default;
            transform.position = worldPosition;
            _countedFor = double.NaN;
            RefreshCount(BattleRunner.Core.Run.StandingArmy.Seed);
            SetLabelVisible(true);
        }

        private double _countedFor = double.NaN;

        /// <summary>
        /// Resolve this pack's share against the army approaching it, and show that count.
        ///
        /// The squad size and the label are written from the same number so they can never
        /// disagree — the bug this replaced said "-26" over five drawn men. Guarded on the
        /// army it was last counted for, for the same reason a gate's sign is.
        /// </summary>
        public void RefreshCount(double army)
        {
            if (!double.IsNaN(_countedFor) && _countedFor > 0.0
                && Mathf.Abs((float)(army - _countedFor)) < (float)(_countedFor * 0.02)) return;
            _countedFor = army;

            double bite = -BattleRunner.Core.Run.GateMath.Headcount(
                army, BattleRunner.Core.Run.GateOp.Subtract, Weight, Depth);
            Headcount = bite <= 0.0 ? 1L : (bite >= long.MaxValue ? long.MaxValue : (long)bite);
            DisplayedCount = (int)Mathf.Min(Headcount, DisplayCap);
            if (_label != null)
                _label.text = BattleRunner.Core.Stats.StatFormat.Army(Headcount);
        }

        /// <summary>
        /// Begin the clash. Called on the frame the crowd's front plane reaches this squad in
        /// its own lane; the outcome is already fixed, only the telling of it takes time.
        /// </summary>
        public void BeginFight(double armyForce)
        {
            long allies = armyForce >= long.MaxValue ? long.MaxValue
                : armyForce <= 0.0 ? 0L : (long)armyForce;
            Clash = new Melee(allies, Headcount);
            Fighting = true;
            FightElapsed = 0f;
        }

        /// <summary>Advance the clash. Returns true on the frame it finishes.</summary>
        public bool TickFight(float dt)
        {
            if (!Fighting) return false;
            FightElapsed += dt;
            Clash.At(FightElapsed, out _, out long enemies);
            DisplayedCount = (int)Mathf.Min(enemies, DisplayCap);
            _label.text = BattleRunner.Core.Stats.StatFormat.Army(enemies);
            if (FightElapsed < Melee.Duration) return false;
            Fighting = false;
            Defeated = true;
            SetLabelVisible(false);
            return true;
        }

        public void Resolve() => Resolved = true;

        public void Defeat()
        {
            Defeated = true;
            SetLabelVisible(false);
        }

        /// <summary>
        /// Face the camera. The old labels were pinned at a fixed 12 degree pitch on the
        /// assumption the camera never leaves -Z, which stopped being true the moment the rig
        /// gained its dynamic pitch and shake — so a count could be read edge-on exactly when
        /// it mattered most.
        /// </summary>
        public void FaceCamera(Vector3 cameraPosition)
        {
            if (_labelPivot == null) return;
            Vector3 toCamera = cameraPosition - _labelPivot.position;
            toCamera.y = 0f;
            if (toCamera.sqrMagnitude < 1e-4f) return;
            _labelPivot.rotation = Quaternion.LookRotation(-toCamera, Vector3.up);
        }

        public void SetLabelVisible(bool visible)
        {
            if (_labelRenderer != null && _labelRenderer.enabled != visible)
                _labelRenderer.enabled = visible;
        }

        public void OnSpawned() => SetLabelVisible(true);

        public void OnDespawned()
        {
            SetLabelVisible(false);
            Fighting = false;
            DisplayedCount = 0;
        }
    }
}
