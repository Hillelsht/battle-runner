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
                UiFactory.Place((RectTransform)title.transform, ColumnX(c), 0.825f, 330f, 50f);
            }

            for (int c = 0; c < Columns.Length; c++)
            {
                List<SkillNode> nodes = SkillTree.Branch(Columns[c]);
                int row = 0;
                int lastTier = 0;

                foreach (SkillNode node in nodes)
                {
                    // Two exclusive nodes share a tier and sit on consecutive rows, so the
                    // fork is visible as a fork.
                    if (node.Tier != lastTier && lastTier != 0) row++;
                    else if (lastTier != 0) row++;
                    lastTier = node.Tier;

                    var widget = new NodeWidget { NodeId = node.Id };
                    string captured = node.Id;
                    Button button = UiFactory.ActionButton(root, $"Node_{node.Id}", string.Empty,
                        Locked, () => OnNodeTapped(captured));
                    UiFactory.Place((RectTransform)button.transform, ColumnX(c), RowY(row), 330f, 118f);

                    widget.Button = button;
                    widget.Background = button.GetComponent<Image>();
                    widget.Label = button.GetComponentInChildren<Text>();
                    widget.Label.fontSize = 24;
                    _widgets.Add(widget);
                    row++;
                }
            }

            _detailLabel = UiFactory.Label(root, "Detail", string.Empty, 28, UiFactory.Parchment);
            UiFactory.Place((RectTransform)_detailLabel.transform, 0.5f, 0.135f, 980f, 90f);

            _respecButton = UiFactory.ActionButton(root, "Respec", RespecIdle,
                new Color(0.30f, 0.12f, 0.12f), OnRespecPressed);
            UiFactory.Place((RectTransform)_respecButton.transform, 0.26f, 0.055f, 380f, 110f);
            _respecLabel = _respecButton.GetComponentInChildren<Text>();
            _respecLabel.fontSize = 28;

            Button continueBtn = UiFactory.ActionButton(root, "Continue", "CONTINUE", UiFactory.Blood,
                () => _onContinue?.Invoke());
            UiFactory.Place((RectTransform)continueBtn.transform, 0.66f, 0.055f, 480f, 110f);

            Hide();
        }

        private static string BranchName(SkillBranch branch) => branch switch
        {
            SkillBranch.Warlord => "WARLORD",
            SkillBranch.Warden => "WARDEN",
            _ => "ZEALOT"
        };

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

            _respecButton.gameObject.SetActive(learned > 0);
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
