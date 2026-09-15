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

        /// <summary>
        /// A CHAMPION rather than a squad: it has health, it winds up, and it swings.
        ///
        /// A flag on the squad rather than a second pooled type. The pool is prewarmed from
        /// RoundPlan.MaxChunkCount * ChunkLayouts.MaxPacksPerChunk and ObjectPool.Get silently
        /// instantiates on an empty pool — which doc 04 bans mid-run — so a second pool means
        /// a second prewarm number that goes stale the next time a shape is added.
        /// </summary>
        public bool IsElite { get; private set; }

        /// <summary>The champion's own fight. Meaningless unless <see cref="IsElite"/>.</summary>
        public Elite Champion;

        /// <summary>Non-zero while this squad is being fought. Drives the whole clash.</summary>
        public float FightElapsed { get; private set; }
        public bool Fighting { get; private set; }
        public Melee Clash { get; private set; }

        /// <summary>How many bodies to draw this frame — drains as the squad loses.</summary>
        public int DisplayedCount { get; private set; }

        private TextMesh _label;
        private MeshRenderer _labelRenderer;
        private Transform _labelPivot;
        private Transform _healthBack;
        private Transform _healthFill;

        /// <summary>Metres. Wide enough to read at the distance a champion is first seen.</summary>
        private const float BarWidth = 1.5f;
        private const float BarHeight = 0.16f;

        private static Material _barBack;
        private static Material _barFill;

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

            // A WORLD-SPACE BAR, not HudScreen's boss bar. That one is a screen-space
            // singleton: two champions on the road at once would fight over it, and a thing
            // that does not stop the run should not claim the frame's one reserved slot.
            // Hung under the pivot that already billboards, so it turns to face the camera
            // for free.
            squad._healthBack = BuildBar(pivot.transform, new Color(0.10f, 0.05f, 0.06f), 0f);
            squad._healthFill = BuildBar(pivot.transform, new Color(1.55f, 0.28f, 0.22f), 0.004f);
            squad.SetHealthBarVisible(false);
            return squad;
        }

        /// <summary>
        /// One quad of the champion's health bar.
        ///
        /// On the project's own Vfx shader, which is unlit, HDR-tinted and — the reason it is
        /// this one rather than a `Shader.Find("Unlit/Color")` — lives in Resources and is
        /// therefore guaranteed into the build. A shader resolved by name can be stripped on
        /// device and come back magenta, which this project has already shipped once.
        /// `_Band = 0` draws the surface flat, which is what the motes already use it for.
        ///
        /// Returns null when Vfx.mat is unusable, exactly as VfxSystem does: the champion then
        /// has no bar and the count over its head carries its state. A missing bar is a worse
        /// champion; a magenta one is a broken game.
        /// </summary>
        private static Transform BuildBar(Transform parent, Color colour, float forward)
        {
            Material shared = BarMaterial(colour);
            if (shared == null) return null;

            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "EliteBar";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 1.62f, -forward);
            go.transform.localScale = new Vector3(BarWidth, BarHeight, 1f);

            var r = go.GetComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sharedMaterial = shared;
            return go.transform;
        }

        /// <summary>
        /// The two bar materials, made once and shared by every champion on the road.
        ///
        /// Shared rather than per-instance because the colours never vary: two materials for
        /// the whole game instead of two per pooled squad, and the pool is prewarmed to
        /// dozens.
        /// </summary>
        private static Material BarMaterial(Color colour)
        {
            bool back = colour.r < 0.5f;
            if (back && _barBack != null) return _barBack;
            if (!back && _barFill != null) return _barFill;

            var template = Resources.Load<Material>("Vfx");
            if (template == null || template.shader == null || !template.shader.isSupported)
                return null;

            var made = new Material(template);
            made.SetColorSafe("_TintColor", colour);
            made.SetFloatSafe("_Fade", 1f);
            made.SetFloatSafe("_Band", 0f);
            if (back) _barBack = made; else _barFill = made;
            return made;
        }

        public void Setup(int weight, int lane, int depth, Vector3 worldPosition,
            bool elite = false)
        {
            Weight = Mathf.Max(0, weight);
            Depth = Mathf.Max(0, depth);
            Lane = lane;
            IsElite = elite;
            Champion = default;
            _championStarted = false;
            Defeated = false;
            Resolved = false;
            Fighting = false;
            FightElapsed = 0f;
            Clash = default;
            transform.position = worldPosition;
            _countedFor = double.NaN;
            RefreshCount(BattleRunner.Core.Run.StandingArmy.Seed);
            SetLabelVisible(true);
            SetHealthBarVisible(false);
        }

        /// <summary>Show or hide the champion's bar. A squad never has one.</summary>
        public void SetHealthBarVisible(bool visible)
        {
            if (_healthBack != null) _healthBack.gameObject.SetActive(visible);
            if (_healthFill != null) _healthFill.gameObject.SetActive(visible);
        }

        /// <summary>
        /// Drive the bar from the champion's health, scaling from the left edge so it drains
        /// the way a health bar is read rather than shrinking toward its own centre.
        /// </summary>
        private void PaintHealthBar()
        {
            if (_healthFill == null) return;
            float h = Mathf.Clamp01(Champion.Health);
            _healthFill.localScale = new Vector3(BarWidth * h, BarHeight, 1f);
            _healthFill.localPosition = new Vector3(-BarWidth * 0.5f * (1f - h), 1.62f, -0.004f);
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
            if (IsElite)
            {
                EnsureChampion(armyForce);
                // A CHAMPION IS NOT A CLASH WITH A LONGER TIMER. Melee fixes its outcome at
                // construction — SurvivingAllies is pure arithmetic over two readonly fields
                // — which is right for a squad the army rolls over and cannot represent a
                // fight whose result depends on whether the player raises a shield three
                // quarters of a second from now. Elite is a separate mutable struct beside
                // it rather than an edit to it, because Melee's termination and conservation
                // properties are pinned by tests that have nothing to do with champions.
                Fighting = true;
                FightElapsed = 0f;
                SetHealthBarVisible(true);
                PaintHealthBar();
                return;
            }

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

            if (IsElite)
            {
                bool died = Champion.Grind(dt);
                PaintHealthBar();
                if (!died) return false;
                Fighting = false;
                Defeated = true;
                SetLabelVisible(false);
                SetHealthBarVisible(false);
                return true;
            }

            Clash.At(FightElapsed, out _, out long enemies);
            DisplayedCount = (int)Mathf.Min(enemies, DisplayCap);
            _label.text = BattleRunner.Core.Stats.StatFormat.Army(enemies);
            if (FightElapsed < Melee.Duration) return false;
            Fighting = false;
            Defeated = true;
            SetLabelVisible(false);
            return true;
        }

        /// <summary>
        /// A champion the spell hit. It is HURT rather than deleted.
        ///
        /// ClearAmbushesAhead releases any unresolved pack it finds with no filter, so a
        /// champion reusing this behaviour would be one-shot by a flick for free. That had to
        /// be a deliberate choice rather than an accident of reuse: deleting it makes the
        /// spell strictly better than fighting and removes the decision the champion exists
        /// to pose. Taking half its health keeps the spell a real answer — it turns a fight
        /// you might lose into one you will win — without making the champion a formality.
        ///
        /// Returns true when the spell finished it off, so the caller still pays the bounty.
        /// </summary>
        public bool TakeSpell()
        {
            if (!IsElite || Defeated) return false;

            // DOES NOT START THE FIGHT. A spell can land on a champion the army has not
            // reached yet, and setting Fighting there would pin it to the army's front and
            // let it start swinging from forty metres away — a champion attacking from
            // outside the road the player is on.
            EnsureChampion(_countedFor > 0.0 ? _countedFor : StandingArmy.Seed);
            SetHealthBarVisible(true);

            bool died = Champion.Grind(Champion.FightSeconds * SpellShare);
            PaintHealthBar();
            if (!died) return false;
            Fighting = false;
            Defeated = true;
            SetLabelVisible(false);
            SetHealthBarVisible(false);
            return true;
        }

        /// <summary>
        /// Build the champion's fight once, and only once.
        ///
        /// A spell landing before the army arrives must not be undone by the army then
        /// arriving and resetting its health — which is exactly what a second Elite.Begin
        /// would do, and it would read as the spell having done nothing.
        /// </summary>
        private void EnsureChampion(double army)
        {
            if (_championStarted) return;
            Champion = Elite.Begin(army, Weight);
            _championStarted = true;
        }

        private bool _championStarted;

        /// <summary>How much of a champion's fight one spell is worth.</summary>
        private const float SpellShare = 0.5f;

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
