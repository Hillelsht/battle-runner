using UnityEngine;

namespace BattleRunner.Gameplay.Crowd
{
    /// <summary>
    /// The army's own headcount, floating over the crowd in world space.
    ///
    /// WHY THIS IS NOT THE HUD NUMBER. The force count has always been drawn large at the top
    /// of the screen, and that is a different thing: it is a readout, a long way from the
    /// crowd, that the player has to look away from the action to read. Asked for "numbers
    /// above it on how many people in this crowd", over the army AND over every squad, the
    /// point is comparison — my number against theirs, both in the same glance, both attached
    /// to the thing they describe. A readout at the top of the screen cannot do that.
    ///
    /// It is deliberately quieter than the HUD number: this is a label on an object, not the
    /// score.
    /// </summary>
    public sealed class ArmyCountLabel : MonoBehaviour
    {
        /// <summary>
        /// Height above the road. Above a 0.5-scale soldier's head with room to spare, and
        /// low enough to stay inside the frame at the rig's pitch.
        /// </summary>
        private const float Height = 1.45f;

        /// <summary>
        /// How far in FRONT of the centroid. The crowd trails its anchor, so a label on the
        /// centroid sits in the middle of the mass and is read through several ranks of
        /// bodies; pushing it forward puts it over the leading edge against open road.
        /// </summary>
        private const float Lead = 1.2f;

        private CrowdController _crowd;
        private TextMesh _text;
        private Transform _pivot;
        private Transform _camera;
        private string _shown;
        private double _eased = -1;

        /// <summary>
        /// How fast the displayed number chases the real one, per second, as a fraction of
        /// the remaining gap.
        ///
        /// THIS EXISTS BECAUSE OF THE MELEE. A clash subtracts the force on the frame contact
        /// is made — every tuned difficulty number in the game depends on that — but the SQUAD
        /// drains its count over the following second while the two lines fight. Left
        /// snapping, the player would watch their own number fall instantly and the enemy's
        /// fall slowly, which reads as the fight being decorative. Easing makes both sides
        /// fall together, which is what a fight looks like.
        ///
        /// It flatters every other change too: a x3 gate now visibly counts up rather than
        /// teleporting. The number is never wrong for long — 8 per second closes 99% of any
        /// gap in about 0.6 s, comfortably inside a 1.15 s clash.
        /// </summary>
        private const float ChaseRate = 8f;

        /// <summary>
        /// Past this, snap. A player dying is not a moment to animate through, and a run that
        /// starts fresh must not count down from whatever the last run ended on.
        /// </summary>
        private const double SnapGap = 4;

        public void Initialize(CrowdController crowd, Font font)
        {
            _crowd = crowd;
            var pivot = new GameObject("ArmyCountPivot");
            pivot.transform.SetParent(transform, false);
            _pivot = pivot.transform;

            var go = new GameObject("ArmyCount", typeof(TextMesh));
            go.transform.SetParent(_pivot, false);
            _text = go.GetComponent<TextMesh>();
            _text.font = font;
            _text.fontSize = 56;
            _text.characterSize = 0.22f;
            _text.anchor = TextAnchor.MiddleCenter;
            // The army's own blue, lifted well clear of the crowd it sits over. Not the HUD's
            // gold: gold is the score, blue is us, and the squads answer in red.
            _text.color = new Color(0.62f, 0.80f, 1f);
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = font.material;
            renderer.sortingOrder = 2;
        }

        private void LateUpdate()
        {
            if (_crowd == null || _pivot == null) return;

            if (_camera == null)
            {
                Camera main = Camera.main;
                if (main != null) _camera = main.transform;
            }

            _pivot.position = new Vector3(_crowd.CenterX, Height, _crowd.FrontZ + Lead);

            // Billboarded, unlike every other label in the game before this. The old ones were
            // pinned at a fixed 12 degree pitch on the assumption the camera never leaves -Z,
            // which stopped being true the moment the rig gained dynamic pitch and shake.
            if (_camera != null)
            {
                Vector3 toCamera = _camera.position - _pivot.position;
                toCamera.y = 0f;
                if (toCamera.sqrMagnitude > 1e-4f)
                    _pivot.rotation = Quaternion.LookRotation(-toCamera, Vector3.up);
            }

            double force = _crowd.ForceCount;
            // Snap when the gap is small ABSOLUTELY or small RELATIVELY. The absolute test
            // alone was right for an army of forty and useless for one of forty billion,
            // where four men is not a gap worth easing across and the chase would run for a
            // frame and then stop anyway.
            if (_eased < 0 || force <= 0
                || System.Math.Abs(_eased - force) < System.Math.Max(SnapGap, force * 0.001))
                _eased = force;
            else
                _eased += (force - _eased) * (1.0 - System.Math.Exp(-ChaseRate * Time.deltaTime));

            // Only touch the TextMesh when the DISPLAYED number changes. Assigning .text
            // rebuilds the glyph mesh, and doing that every frame for a value that changes a
            // few times a second is pure waste on the exact device this has to hold 60 on.
            string shown = BattleRunner.Core.Stats.StatFormat.Army(_eased);
            if (shown == _shown) return;
            _shown = shown;
            _text.text = shown;
        }
    }
}
