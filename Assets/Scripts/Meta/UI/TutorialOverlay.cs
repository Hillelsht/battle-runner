using UnityEngine;
using UnityEngine.UI;

namespace BattleRunner.Meta.UI
{
    /// <summary>
    /// The coaching prompt: a headline, one line of instruction, and a patience bar that
    /// drains so the player can see the game is about to move on without them.
    ///
    /// Built from the same vocabulary as every other screen — UiFactory offers solid
    /// rectangles and legacy Text and nothing else, so the "arrow" is the HUD's own ASCII
    /// ("^" / "v" / "&lt; &gt;") rather than a glyph the built-in font may not carry.
    ///
    /// Deliberately parented ABOVE the HUD but created BEFORE ResurrectPrompt: the canvas
    /// sets no sortingOrder anywhere, so draw order is sibling order, and the resurrect
    /// modal must stay on top of a coaching hint.
    /// </summary>
    public sealed class TutorialOverlay
    {
        private readonly GameObject _root;
        private readonly Text _headline;
        private readonly Text _detail;
        private readonly RectTransform _patience;
        private readonly float _patienceFullWidth;

        public TutorialOverlay(Transform canvas)
        {
            _root = new GameObject("TutorialOverlay", typeof(RectTransform));
            _root.transform.SetParent(canvas, false);
            UiFactory.Stretch((RectTransform)_root.transform);

            // A band rather than a full-screen scrim: the lesson is about what is happening
            // on the road, so the road must stay visible while it is taught.
            RectTransform band = UiFactory.Panel(_root.transform, "Band", UiFactory.Ink);
            UiFactory.Place(band, 0.5f, 0.70f, 900f, 190f);

            _headline = UiFactory.Label(_root.transform, "Headline", string.Empty, 60, UiFactory.Gold,
                TextAnchor.MiddleCenter);
            UiFactory.Place((RectTransform)_headline.transform, 0.5f, 0.735f, 880f, 80f);

            _detail = UiFactory.Label(_root.transform, "Detail", string.Empty, 36, UiFactory.Parchment,
                TextAnchor.MiddleCenter);
            UiFactory.Place((RectTransform)_detail.transform, 0.5f, 0.678f, 880f, 60f);

            _patienceFullWidth = 860f;
            RectTransform track = UiFactory.Panel(_root.transform, "PatienceTrack", UiFactory.Shadow);
            UiFactory.Place(track, 0.5f, 0.638f, _patienceFullWidth, 8f);

            _patience = UiFactory.Panel(_root.transform, "Patience", UiFactory.Arcane);
            UiFactory.Place(_patience, 0.5f, 0.638f, _patienceFullWidth, 8f);
            // Place() centres the pivot, which would drain the bar from both ends. Pin the
            // left edge to the track's left edge so it empties left-to-right.
            _patience.pivot = new Vector2(0f, 0.5f);
            _patience.anchoredPosition = new Vector2(-_patienceFullWidth * 0.5f, 0f);

            // Nothing in the coach may absorb a tap. UiFactory's Image and Text both default
            // raycastTarget to true and it is overridden nowhere in the project, so a
            // full-width prompt would silently eat button presses through the GraphicRaycaster.
            foreach (Graphic graphic in _root.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = false;

            Hide();
        }

        public void Show(string headline, string detail)
        {
            _headline.text = headline;
            _detail.text = detail;
            SetPatience(0f);
            _root.SetActive(true);
        }

        /// <summary>0 = just appeared, 1 = about to give up and let the run continue.</summary>
        public void SetPatience(float progress)
        {
            float remaining = Mathf.Clamp01(1f - progress);
            _patience.sizeDelta = new Vector2(_patienceFullWidth * remaining, _patience.sizeDelta.y);
        }

        public void Hide() => _root.SetActive(false);
    }
}
