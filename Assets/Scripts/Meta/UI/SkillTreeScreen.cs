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

        private static readonly Color Taken = new Color(0.86f, 0.62f, 0.22f);
        private static readonly Color Available = new Color(0.24f, 0.30f, 0.44f);
        private static readonly Color Locked = new Color(0.13f, 0.13f, 0.17f);

        private readonly GameObject _root;
        private readonly Text _pointsLabel;
        private readonly Text _detailLabel;
        private readonly List<NodeWidget> _widgets = new List<NodeWidget>();

        private Action<string> _onTake;
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
                        Locked, () => _onTake?.Invoke(captured));
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

            Button continueBtn = UiFactory.ActionButton(root, "Continue", "CONTINUE", UiFactory.Blood,
                () => _onContinue?.Invoke());
            UiFactory.Place((RectTransform)continueBtn.transform, 0.5f, 0.055f, 560f, 110f);

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

        public void Show(Action<string> onTake, Action onContinue)
        {
            _onTake = onTake;
            _onContinue = onContinue;
            _root.SetActive(true);
        }

        /// <summary>Repaint every node against the current choices and point balance.</summary>
        public void Refresh(IReadOnlyCollection<string> taken, int unspentPoints)
        {
            _pointsLabel.text = unspentPoints == 1 ? "1 point to spend" : $"{unspentPoints} points to spend";

            int learned = 0;
            foreach (NodeWidget widget in _widgets)
            {
                SkillNode node = SkillTree.Find(widget.NodeId);
                if (node == null) continue;

                bool isTaken = Contains(taken, node.Id);
                if (isTaken) learned++;
                string blocked = SkillTree.BlockedReason(node.Id, taken, unspentPoints);

                widget.Background.color = isTaken ? Taken : blocked == null ? Available : Locked;
                widget.Label.color = isTaken || blocked == null ? Color.white : new Color(0.45f, 0.45f, 0.52f);
                widget.Label.text = $"{node.DisplayName}\n{node.Description}";
                widget.Button.interactable = blocked == null;
            }

            _detailLabel.text = learned == 0
                ? "Pick a path. The choice at the second rank locks out its rival."
                : $"{learned} talent{(learned == 1 ? string.Empty : "s")} learned";
        }

        /// <summary>Explain a refused tap rather than doing nothing.</summary>
        public void ShowRefusal(string reason) => _detailLabel.text = reason;

        public void Hide() => _root.SetActive(false);

        private static bool Contains(IReadOnlyCollection<string> taken, string id)
        {
            if (taken == null) return false;
            foreach (string t in taken)
                if (string.Equals(t, id, StringComparison.Ordinal)) return true;
            return false;
        }
    }
}
