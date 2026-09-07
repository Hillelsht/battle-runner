using System;
using System.Collections.Generic;
using BattleRunner.Core.Progression;
using UnityEngine;
using UnityEngine.UI;

namespace BattleRunner.Meta.UI
{
    /// <summary>
    /// The talent tree: three branches as columns, three tiers as rows, so the shape of a
    /// build is legible at a glance on a portrait phone.
    ///
    /// Every node is always drawn — a tree you cannot see the whole of is not a tree, it is
    /// a menu. Unaffordable and locked nodes are dimmed rather than hidden, so the player
    /// can plan two levels ahead, which is the entire reason to have branches at all.
    ///
    /// Learned nodes stay tappable, because a talent you cannot give back is a trap: the
    /// first tap arms the undo and says so, the second spends it. The arm expires on its own
    /// so backgrounding the app mid-decision never leaves a live one-tap undo on resume, and
    /// FORGET ALL takes the same two taps for the same reason.
    /// </summary>
    public sealed class SkillTreeScreen
    {
        private sealed class NodeWidget
        {
            public Button Button;
            public Image Background;
            public Text Label;
            public string NodeId;
        }

        private static readonly SkillBranch[] Columns =
        {
            SkillBranch.Warlord, SkillBranch.Warden, SkillBranch.Zealot
        };

        private const float ArmedSeconds = 4f;
        private const string RespecIdle = "FORGET ALL";
        private const string RespecArmed = "SURE?";

        private static readonly Color Taken = new Color(0.86f, 0.62f, 0.22f);
        private static readonly Color Available = new Color(0.24f, 0.30f, 0.44f);
        private static readonly Color Locked = new Color(0.13f, 0.13f, 0.17f);
        private static readonly Color Arming = new Color(0.55f, 0.18f, 0.16f);
        private static readonly Color Dim = new Color(0.45f, 0.45f, 0.52f);

        private readonly GameObject _root;
        private readonly Text _pointsLabel;
        private readonly Text _detailLabel;
        private readonly Button _respecButton;
        private readonly RectTransform _continueRect;
        private readonly Text _respecLabel;
        private readonly List<NodeWidget> _widgets = new List<NodeWidget>();

        // A copy, not the caller's list: the state mutates its own between refreshes and the
        // screen must repaint from what it was last told, not from work in progress.
        private readonly List<string> _taken = new List<string>();
        private int _unspent;

        private string _armedNode;
        private float _armedFor;
        private bool _respecIsArmed;
        private float _respecArmedFor;

        private Action<string> _onTake;
        private Action<string> _onUnlearn;
        private Action _onRespec;
        private Action _onContinue;

        public SkillTreeScreen(Transform canvas)
        {
            RectTransform root = UiFactory.FullscreenPanel(canvas, "SkillTree", UiFactory.Ink);
            _root = root.gameObject;

            Text header = UiFactory.Label(root, "Header", "GROW STRONGER", 58, UiFactory.Gold);
            UiFactory.Place((RectTransform)header.transform, 0.5f, 0.94f, 900f, 90f);

            _pointsLabel = UiFactory.Label(root, "Points", string.Empty, 36, UiFactory.Parchment);
            UiFactory.Place((RectTransform)_pointsLabel.transform, 0.5f, 0.885f, 900f, 60f);

            // Column headings name the fantasy, not the stat — "Warlord" carries more than
            // "+damage" and it is what the player will call their build.
            for (int c = 0; c < Columns.Length; c++)
            {
                Text title = UiFactory.Label(root, $"Col{c}", BranchName(Columns[c]), 30, UiFactory.Arcane);
                UiFactory.Place((RectTransform)title.transform, ColumnX(c), 0.825f, CellWidth, 50f);
            }

            for (int c = 0; c < Columns.Length; c++)
            {
                List<SkillNode> nodes = SkillTree.Branch(Columns[c]);

                // One row per node, in tier order. SkillTree.Branch sorts by tier and every
                // branch is exactly 1 + 2 exclusive + 1 capstone, so the list index IS the
                // row — the two tier-2 nodes land on consecutive rows and the fork reads as
                // a fork for free.
                //
                // This replaces a lastTier state machine whose two if/else arms BOTH did
                // row++ and which then incremented again at the end of the body, stepping
                // 0, 2, 4, 6. RowY(6) = 0.755 - 6*0.145 = -0.115, i.e. below the bottom of
                // the screen: every capstone was rendered off-screen and unreachable, and
                // the three visible rows sat at double the intended spacing.
                for (int row = 0; row < nodes.Count; row++)
                {
                    SkillNode node = nodes[row];
                    var widget = new NodeWidget { NodeId = node.Id };
                    string captured = node.Id;
                    Button button = UiFactory.ActionButton(root, $"Node_{node.Id}", string.Empty,
                        Locked, () => OnNodeTapped(captured));
                    UiFactory.Place((RectTransform)button.transform, ColumnX(c), RowY(row), CellWidth, 118f);

                    widget.Button = button;
                    widget.Background = button.GetComponent<Image>();
                    widget.Label = button.GetComponentInChildren<Text>();

                    // WRAP, not Overflow. UiFactory.Label sets HorizontalWrapMode.Overflow
                    // for every label in the game, which is right for a heading and wrong
                    // for a cell in a grid: the capstone descriptions ("+25% Might, +50%
                    // spell damage") are half again as wide as their cell, so at 24 pt they
                    // ran out through both edges and were drawn OVER by the neighbouring
                    // column's opaque panel, which is why they read as clipped mid-word.
                    // Truncate vertically so a long string can never push out of the button
                    // either. 20 pt over two lines fits the longest string with room spare.
                    widget.Label.horizontalOverflow = HorizontalWrapMode.Wrap;
                    widget.Label.verticalOverflow = VerticalWrapMode.Truncate;
                    widget.Label.fontSize = 20;
                    var labelRect = (RectTransform)widget.Label.transform;
                    labelRect.offsetMin = new Vector2(12f, 6f);
                    labelRect.offsetMax = new Vector2(-12f, -6f);
                    _widgets.Add(widget);
                }
            }

            _detailLabel = UiFactory.Label(root, "Detail", string.Empty, 28, UiFactory.Parchment);
            UiFactory.Place((RectTransform)_detailLabel.transform, 0.5f, 0.135f, 980f, 90f);

            // Narrower and further apart than 380@0.26 + 480@0.66. Place() mixes a
            // NORMALISED centre with a PIXEL width, so the gap between two buttons shrinks
            // as the canvas narrows: at the 1080-unit reference the pair cleared each other
            // by 2 units, and on any phone taller than 16:9 the CanvasScaler's match-0.5
            // shrinks the canvas to ~978 units and they overlapped by 39.
            _respecButton = UiFactory.ActionButton(root, "Respec", RespecIdle,
                new Color(0.30f, 0.12f, 0.12f), OnRespecPressed);
            UiFactory.Place((RectTransform)_respecButton.transform, 0.24f, 0.055f, 340f, 110f);
            _respecLabel = _respecButton.GetComponentInChildren<Text>();
            _respecLabel.fontSize = 28;

            Button continueBtn = UiFactory.ActionButton(root, "Continue", "CONTINUE", UiFactory.Blood,
                () => _onContinue?.Invoke());
            _continueRect = (RectTransform)continueBtn.transform;
            PlaceContinue(false);

            Hide();
        }

        private static string BranchName(SkillBranch branch) => branch switch
        {
            SkillBranch.Warlord => "WARLORD",
            SkillBranch.Warden => "WARDEN",
            _ => "ZEALOT"
        };

        /// <summary>
        /// 280, not 330. Columns are pitched 0.32 apart, which is 313 units on the ~978-unit
        /// canvas a phone taller than 16:9 produces — narrower than the 330-unit cells, so
        /// the three columns overlapped by 18.7 units and each one's panel drew over its
        /// neighbour's text. 280 leaves a real gutter on every supported aspect.
        /// </summary>
        private const float CellWidth = 280f;

        private void PlaceContinue(bool sharingTheRow) =>
            UiFactory.Place(_continueRect, sharingTheRow ? 0.68f : 0.5f, 0.055f, 400f, 110f);

        private static float ColumnX(int column) => 0.18f + column * 0.32f;

        // Four rows: tier 1, the two exclusive tier-2 nodes, then the capstone.
        private static float RowY(int row) => 0.755f - row * 0.145f;

        public void Show(Action<string> onTake, Action<string> onUnlearn, Action onRespec, Action onContinue)
        {
            _onTake = onTake;
            _onUnlearn = onUnlearn;
            _onRespec = onRespec;
            _onContinue = onContinue;
            DisarmAll();
            _root.SetActive(true);
        }

        /// <summary>Repaint every node against the current choices and point balance.</summary>
        public void Refresh(IReadOnlyCollection<string> taken, int unspentPoints)
        {
            _taken.Clear();
            if (taken != null) _taken.AddRange(taken);
            _unspent = unspentPoints;

            // An armed undo whose talent is already gone has nothing left to confirm.
            if (_armedNode != null && !Contains(_taken, _armedNode)) Disarm();
            if (_taken.Count == 0) DisarmRespec();

            Paint();
        }

        /// <summary>Driven by the state so an armed undo can expire on its own.</summary>
        public void Tick(float deltaTime)
        {
            bool expired = false;

            if (_armedNode != null)
            {
                _armedFor += deltaTime;
                if (_armedFor >= ArmedSeconds) { Disarm(); expired = true; }
            }

            if (_respecIsArmed)
            {
                _respecArmedFor += deltaTime;
                if (_respecArmedFor >= ArmedSeconds) { DisarmRespec(); expired = true; }
            }

            if (expired) Paint();
        }

        /// <summary>Say something back to the player — a refused tap, or a confirmed one.</summary>
        public void ShowNote(string note) => _detailLabel.text = note;

        public void Hide()
        {
            DisarmAll();
            _root.SetActive(false);
        }

        private void Paint()
        {
            _pointsLabel.text = _unspent == 1 ? "1 point to spend" : $"{_unspent} points to spend";

            int learned = 0;
            foreach (NodeWidget widget in _widgets)
            {
                SkillNode node = SkillTree.Find(widget.NodeId);
                if (node == null) continue;

                bool isTaken = Contains(_taken, node.Id);
                if (isTaken) learned++;
                bool armed = isTaken && string.Equals(_armedNode, node.Id, StringComparison.Ordinal);
                string blocked = SkillTree.BlockedReason(node.Id, _taken, _unspent);

                widget.Background.color = armed ? Arming
                    : isTaken ? Taken
                    : blocked == null ? Available : Locked;
                widget.Label.color = isTaken || blocked == null ? Color.white : Dim;
                widget.Label.text = armed
                    ? $"{node.DisplayName}\nTap again to unlearn"
                    : $"{node.DisplayName}\n{node.Description}";

                // Learned nodes stay live even when something leans on them: the tap is how
                // the player finds out WHICH talent has to come off first.
                widget.Button.interactable = isTaken || blocked == null;
            }

            // FORGET ALL only exists once something has been learned, so CONTINUE has to
            // move when it goes. Leaving it pinned to the right-hand slot put the screen's
            // only call to action 172 px off centre on a fresh tree — the exact state every
            // new player meets it in.
            _respecButton.gameObject.SetActive(learned > 0);
            PlaceContinue(learned > 0);
            _respecLabel.text = _respecIsArmed ? RespecArmed : RespecIdle;

            _detailLabel.text = learned == 0
                ? "Pick a path. The choice at the second rank locks out its rival."
                : $"{learned} talent{(learned == 1 ? string.Empty : "s")} learned · tap one to give it back";
        }

        private void OnNodeTapped(string nodeId)
        {
            if (!Contains(_taken, nodeId))
            {
                DisarmAll();
                Paint();
                _onTake?.Invoke(nodeId);
                return;
            }

            if (string.Equals(_armedNode, nodeId, StringComparison.Ordinal))
            {
                DisarmAll();
                _onUnlearn?.Invoke(nodeId);
                return;
            }

            string blocked = SkillTree.UnlearnBlockedReason(nodeId, _taken);
            if (blocked != null)
            {
                DisarmAll();
                Paint();
                ShowNote(blocked);
                return;
            }

            _armedNode = nodeId;
            _armedFor = 0f;
            DisarmRespec();
            Paint();
            ShowNote($"Tap {SkillTree.Find(nodeId)?.DisplayName} again to unlearn it.");
        }

        private void OnRespecPressed()
        {
            if (!_respecIsArmed)
            {
                Disarm();
                _respecIsArmed = true;
                _respecArmedFor = 0f;
                Paint();
                ShowNote("Forget every talent and take all the points back?");
                return;
            }

            DisarmRespec();
            _onRespec?.Invoke();
        }

        private void DisarmAll()
        {
            Disarm();
            DisarmRespec();
        }

        private void Disarm()
        {
            _armedNode = null;
            _armedFor = 0f;
        }

        private void DisarmRespec()
        {
            _respecIsArmed = false;
            _respecArmedFor = 0f;
        }

        private static bool Contains(IReadOnlyCollection<string> taken, string id)
        {
            if (taken == null) return false;
            foreach (string t in taken)
                if (string.Equals(t, id, StringComparison.Ordinal)) return true;
            return false;
        }
    }
}
