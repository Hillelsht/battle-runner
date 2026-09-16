using BattleRunner.Core.Heroes;
using UnityEngine;

namespace BattleRunner.Gameplay.Menu
{
    /// <summary>
    /// A lit plinth with one hero standing on it, for the character-select screen.
    ///
    /// *"make all characters appear so I could see who do I choose with animation of them how
    /// they stay and greet and fight."*
    ///
    /// NO SECOND CAMERA AND NO RENDER TEXTURE, and that is worth writing down because the
    /// obvious reading of "3D inside a UI screen" is that you need both. The canvas is
    /// `ScreenSpaceOverlay`, which composites the UI over the finished frame — so wherever the
    /// UI is TRANSPARENT, whatever the camera rendered shows through. And during the menus
    /// `ArenaRoot` is disabled, so what the camera renders is nothing at all: an empty world and
    /// a sky. A stage standing in that emptiness, with the select screen leaving a window over
    /// it, is the whole feature.
    ///
    /// What that buys beyond the machinery it avoids: the hero is lit by the world's own key
    /// light and graded by the world's own post stack, so the bloom on an Ashcaller's orb is
    /// the bloom it will have in the run. A render texture would have had to reproduce all of
    /// that, and would have drifted from it.
    ///
    /// THE STAGE STANDS 300 METRES BEHIND THE START of the track, which is not superstition: the
    /// road is built from z = 0 upward and the despawn plane is computed from a constant, so
    /// anywhere in front of the origin is somewhere a chunk could one day be. Behind it, nothing
    /// is ever built.
    /// </summary>
    public sealed class HeroStage : MonoBehaviour
    {
        /// <summary>Where the stage stands. See the class note on why it is behind the start.</summary>
        public static readonly Vector3 Mark = new Vector3(0f, 0f, -300f);

        /// <summary>The top of the plinth, which is where the hero's feet go.</summary>
        public const float PlinthTop = 0.10f;

        /// <summary>Where the camera sits to look at the stage, and what it aims at.</summary>
        public static readonly Vector3 Eye = new Vector3(0.55f, 1.80f, -304.10f);
        public static readonly Vector3 Aim = new Vector3(0f, 1.34f, -300f);

        /// <summary>
        /// How much larger than the in-run hero the figure stands here.
        ///
        /// The run draws the hero at 1.35x and 10-17 m away, which measured out at 172-242 px
        /// tall on a 1920 render. Here it is about 4.1 m away at 2.2x, which is most of the
        /// middle of a portrait phone with the screen's own panels above and below it. That is
        /// the difference between "you can tell them apart" and *"so I could see who do I
        /// choose"*.
        /// </summary>
        public const float StageScale = 2.2f;

        /// <summary>
        /// Degrees the figure stands off square. Three-quarters on, turned so the main hand is
        /// the nearest thing to the lens: a figure flat to the camera loses the depth of a
        /// shield, a hood and a slung horn all at once.
        /// </summary>
        public const float RestYaw = -22f;

        private Transform _body;
        private Transform _main;
        private Transform _off;
        private MeshFilter _bodyFilter;
        private MeshFilter _mainFilter;
        private MeshFilter _offFilter;
        private Material _material;
        private Material _plinth;

        private HeroClass _hero = HeroRoster.Default;
        private HeroAct _act = HeroAct.Idle;
        private float _elapsed;
        private Color _glow = Color.white;

        /// <summary>
        /// Build the stage. <paramref name="heroMaterial"/> is a TEMPLATE, not the material that
        /// gets used — `HeroVisual.Wear` mutates the shared hero material in place, so a stage
        /// sharing it would repaint the in-run hero every time the player looked at a card.
        /// </summary>
        public void Initialize(Material heroMaterial, Material plinthMaterial)
        {
            transform.position = Mark;

            _material = heroMaterial != null ? ShaderSafety.CreateMaterial(heroMaterial) : null;
            if (_material != null)
            {
                // Posed rather than marched: the crowd shader's bob would fight the pose, and a
                // walk cycle decoded from a 2.2 scale is nonsense anyway.
                _material.SetFloatSafe("_BobAmount", 0f);
                _material.SetFloatSafe("_EmissionFlat", 0.10f);
            }

            if (plinthMaterial != null)
            {
                _plinth = ShaderSafety.CreateMaterial(plinthMaterial);
                _plinth.SetColorSafe("_BaseColor", new Color(0.11f, 0.105f, 0.13f));
                _plinth.SetColorSafe("_EmissionColor", new Color(0.05f, 0.045f, 0.07f));
                _plinth.SetFloatSafe("_BobAmount", 0f);

                var disc = new GameObject("Plinth", typeof(MeshFilter), typeof(MeshRenderer));
                disc.transform.SetParent(transform, false);
                disc.transform.localScale = new Vector3(1.55f, 1f, 1.55f);
                disc.GetComponent<MeshFilter>().sharedMesh = ProceduralMeshes.ArenaFloor;
                MeshRenderer discRenderer = disc.GetComponent<MeshRenderer>();
                discRenderer.sharedMaterial = _plinth;
                // RECEIVES but does not cast. The figure's shadow falling across the plinth is
                // what sits it on the stage rather than floating it over one; the plinth's own
                // shadow has nothing to fall on and would only cost a cascade.
                discRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                discRenderer.receiveShadows = true;
            }

            _body = Part("Body", out _bodyFilter);
            _main = Part("MainHand", out _mainFilter);
            _off = Part("OffHand", out _offFilter);
            gameObject.SetActive(false);
        }

        private Transform Part(string name, out MeshFilter filter)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            filter = go.GetComponent<MeshFilter>();
            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
            return go.transform;
        }

        /// <summary>
        /// Put a hero on the stage, greeting.
        ///
        /// Always the greeting rather than the idle, because that is the act's whole job: the
        /// player has just asked to look at this one, and a figure that acknowledges being
        /// looked at is the difference between a character and a model viewer.
        /// </summary>
        public void Show(HeroClass hero, Color body, Color glow)
        {
            _hero = hero;
            _glow = glow;
            gameObject.SetActive(true);

            ProceduralMeshes.HeroBuild parts = ProceduralMeshes.HeroInParts(hero);
            if (_bodyFilter != null) _bodyFilter.sharedMesh = parts.Body;
            if (_mainFilter != null) _mainFilter.sharedMesh = parts.MainHand;
            if (_offFilter != null) _offFilter.sharedMesh = parts.OffHand;
            if (_main != null) _main.localPosition = parts.MainGrip * StageScale;
            if (_off != null) _off.localPosition = parts.OffGrip * StageScale;

            if (_material != null)
            {
                _material.SetColorSafe("_BaseColor", body);
                _material.SetColorSafe("_EmissionColor", glow);
            }

            Play(HeroAct.Greet);
        }

        /// <summary>Start an act from its first frame.</summary>
        public void Play(HeroAct act)
        {
            _act = act;
            _elapsed = 0f;
            Pose();
        }

        public void Hide() => gameObject.SetActive(false);

        private void LateUpdate()
        {
            // UNSCALED, because a menu has no reason to care what Time.timeScale is and the
            // pause path sets it to zero — a hero frozen mid-greeting on the select screen
            // would read as the app having hung.
            _elapsed += Time.unscaledDeltaTime;

            // A one-shot act hands back to the idle rather than freezing on its last frame.
            // Both end at rest, so the handover is a cut with nothing to blend — which is the
            // property HeroPoseTests pins.
            if (_act != HeroAct.Idle && _elapsed >= HeroChoreography.ActSeconds(_hero, _act))
            {
                _act = HeroAct.Idle;
                _elapsed = 0f;
            }
            Pose();
        }

        private void Pose()
        {
            HeroPose p = HeroChoreography.At(_hero, _act, _elapsed);

            transform.SetPositionAndRotation(
                Mark + new Vector3(0f, PlinthTop + p.Rise, -p.Step),
                Quaternion.Euler(p.Pitch, RestYaw + p.Yaw, p.Roll));

            const float S = StageScale;
            if (_body != null) _body.localScale = new Vector3(S, S, S);

            // The hands pivot about their GRIPS, which is the whole reason the meshes were
            // authored around them: localPosition is the grip and localRotation turns the piece
            // about it. A weapon whose vertices sat at absolute positions could only be moved by
            // moving the hero, and a mace that cannot swing without the body swinging with it is
            // not an animation, it is a statue on a turntable.
            PoseHand(_main, _mainFilter, p.MainSwing, p.MainLift, S);
            PoseHand(_off, _offFilter, 0f, p.OffLift, S);

            if (_material != null)
            {
                _material.SetColorSafe("_EmissionColor",
                    new Color(_glow.r * p.Heat, _glow.g * p.Heat, _glow.b * p.Heat));
                _material.SetFloatSafe("_EmissionFlat",
                    0.10f + 0.22f * Mathf.Max(0f, p.Heat - 1f));
            }
        }

        private static void PoseHand(Transform hand, MeshFilter filter, float swing, float lift,
            float scale)
        {
            if (hand == null || filter == null || filter.sharedMesh == null) return;
            hand.localScale = new Vector3(scale, scale, scale);
            // Swing is about X — the arc a weapon travels toward the camera — and lift is about
            // Z, the arm coming away from the body. In that order, so a raised weapon still
            // swings along its own arc rather than around the hero's waist.
            hand.localRotation = Quaternion.Euler(swing, 0f, lift);
        }
    }
}
