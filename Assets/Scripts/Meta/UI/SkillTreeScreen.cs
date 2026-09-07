using System;
using System.Collections.Generic;
using BattleRunner.Core.Progression;
using UnityEngine;
using UnityEngine.UI;

namespace BattleRunner.Meta.UI
{
    /// <summary>
    /// The talent tree: five tabs, each a scrolling column of tiers.
    ///
    /// THE OLD SCREEN WAS A FIXED 3x4 GRID and it could not survive this tree. Twelve nodes
    /// fit on a phone at once; sixty do not, and no amount of shrinking cells makes them.
    /// So the shape changed: one branch at a time, scrolled vertically, tier headings as
    /// signposts. A player looking for "what can I take next" scrolls to the first tier that
    /// is not greyed out, which is a far better reading of a big tree than a wall of cells.
    ///
    /// EVERY NODE IS STILL DRAWN, LOCKED ONES INCLUDED. A tree you cannot see the whole of
    /// is a menu, and the reason to have branches at all is so a player can plan two levels
    /// ahead. Locked cells say what they are waiting for ("Needs 12 points in Warlord")
    /// rather than simply refusing.
    ///
    /// TAKING AND GIVING BACK ARE NOW SEPARATE CONTROLS. With ranks, one node can be both
    /// rankable and refundable at the same moment, so a single tap can no longer mean both.
    /// The cell ranks up; the small minus arms an undo and the second tap on it spends it.
    /// The arm expires on its own, so backgrounding the app mid-decision never leaves a live
    /// one-tap undo on resume, and FORGET ALL takes the same two taps for the same reason.
    /// </summary>
    public sealed class SkillTreeScreen
    {
        private sealed class NodeWidget
        {
            public string NodeId;
            public Button Button;
            public Image Background;
            public Text Name;
            public Text Desc;
            public Text Pips;
            public GameObject Minus;
        }

        private sealed class TrackWidget
        {
            public string TrackId;
            public Button Button;
            public Image Background;
            public Text Name;
            public Text Detail;
        }

        private static readonly SkillBranch[] Tabs =
        {
            SkillBranch.Warlord, SkillBranch.Warden, SkillBranch.Zealot, SkillBranch.Crossroads
        };

        /// <summary>Paragon is the fifth tab and has no SkillBranch, so it is indexed past them.</summary>
        private const int ParagonTab = 4;
        private const int TabCount = 5;

        private const float ArmedSeconds = 4f;
        private const string RespecIdle = "FORGET ALL";
        private const string RespecArmed = "SURE?";

        // Layout, in reference pixels down the content column.
        private const float TierHeadHeight = 46f;
        private const float NodeHeight = 168f;
        private const float RowGap = 10f;
        private const float TierGap = 18f;
        private const float TrackHeight = 132f;

        private static readonly Color Ranked = new Color(0.52f, 0.36f, 0.14f);
        private static readonly Color Maxed = new Color(0.86f, 0.62f, 0.22f);
        private static readonly Color Available = new Color(0.24f, 0.30f, 0.44f);
        private static readonly Color Locked = new Color(0.13f, 0.13f, 0.17f);
        private static readonly Color Arming = new Color(0.55f, 0.18f, 0.16f);
        private static readonly Color KeystoneTint = new Color(0.34f, 0.18f, 0.42f);
        private static readonly Color Dim = new Color(0.45f, 0.45f, 0.52f);

        private readonly GameObject _root;
        private readonly Text _pointsLabel;
        private readonly Text _detailLabel;
        private readonly Button _respecButton;
        private readonly Text _respecLabel;
        private readonly RectTransform _continueRect;

        private readonly Image[] _tabFills = new Image[TabCount];
        private readonly GameObject[] _pages = new GameObject[TabCount];
        private readonly List<NodeWidget> _widgets = new List<NodeWidget>();
        private readonly List<TrackWidget> _tracks = new List<TrackWidget>();

        // Copies, not the caller's maps: the state mutates its own between refreshes and the
        // screen must repaint from what it was last told, not from work in progress.
        private readonly Dictionary<string, int> _ranks = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _paragon = new Dictionary<string, int>();
        private int _unspent;
        private int _tab;

        private string _armedNode;
        private float _armedFor;
        private bool _respecIsArmed;
        private float _respecArmedFor;

        private Action<string> _onTake;
        private Action<string> _onUnlearn;
        private Action<string> _onParagon;
        private Action _onRespec;
        private Action _onContinue;

        public SkillTreeScreen(Transform canvas)
        {
            RectTransform root = UiFactory.FullscreenPanel(canvas, "SkillTree", UiFactory.Ink);
            _root = root.gameObject;

            Text header = UiFactory.Label(root, "Header", "GROW STRONGER", 52, UiFactory.Gold);
            UiFactory.Place((RectTransform)header.transform, 0.5f, 0.957f, 900f, 76f);

            _pointsLabel = UiFactory.Label(root, "Points", string.Empty, 32, UiFactory.Parchment);
            UiFactory.Place((RectTransform)_pointsLabel.transform, 0.5f, 0.915f, 900f, 52f);

            BuildTabs(root);

            for (int i = 0; i < Tabs.Length; i++) _pages[i] = BuildBranchPage(root, Tabs[i]);
            _pages[ParagonTab] = BuildParagonPage(root);

            _detailLabel = UiFactory.Label(root, "Detail", string.Empty, 26, UiFactory.Parchment);
            UiFactory.Place((RectTransform)_detailLabel.transform, 0.5f, 0.142f, 980f, 90f);

            _respecButton = UiFactory.ActionButton(root, "Respec", RespecIdle,
                new Color(0.30f, 0.12f, 0.12f), OnRespecPressed);
            UiFactory.Place((RectTransform)_respecButton.transform, 0.24f, 0.055f, 340f, 108f);
            _respecLabel = _respecButton.GetComponentInChildren<Text>();
            _respecLabel.fontSize = 28;

            Button continueBtn = UiFactory.ActionButton(root, "Continue", "CONTINUE", UiFactory.Blood,
                () => _onContinue?.Invoke());
            _continueRect = (RectTransform)continueBtn.transform;
            PlaceContinue(false);

            Hide();
        }

        // --- Construction ------------------------------------------------------

        private void BuildTabs(RectTransform root)
        {
            for (int i = 0; i < TabCount; i++)
            {
                int captured = i;
                Button tab = UiFactory.ActionButton(root, $"Tab{i}", TabName(i),
                    Available, () => SelectTab(captured));
                UiFactory.PlaceRegion((RectTransform)tab.transform,
                    0.030f + i * 0.188f, 0.838f, 0.206f + i * 0.188f, 0.888f);
                Text label = tab.GetComponentInChildren<Text>();
                label.fontSize = 20;
                _tabFills[i] = tab.GetComponent<Image>();
            }
        }

        private GameObject BuildBranchPage(RectTransform root, SkillBranch branch)
        {
            ScrollRect scroll = UiFactory.ScrollColumn(root, $"Page_{branch}", out RectTransform content);
            UiFactory.PlaceRegion((RectTransform)scroll.transform, 0.025f, 0.185f, 0.975f, 0.830f);

            List<SkillNode> nodes = SkillTree.Branch(branch);
            float y = 0f;
            int tier = -1;
            int column = 0;

            foreach (SkillNode node in nodes)
            {
                if (node.Tier != tier)
                {
                    // A new tier starts a new row even when the last one ended half full,
                    // or the heading would sit beside a node that belongs above it.
                    if (tier >= 0) y += NodeHeight + TierGap;
                    tier = node.Tier;
                    column = 0;

                    Text head = UiFactory.Label(content, $"Tier{branch}{tier}", TierHeading(branch, tier),
                        24, UiFactory.Arcane, TextAnchor.MiddleLeft);
                    UiFactory.PlaceCell((RectTransform)head.transform, 0.02f, 0.98f, y, TierHeadHeight);
                    y += TierHeadHeight;
                }
                else if (column == 0)
                {
                    y += NodeHeight + RowGap;
                }

                _widgets.Add(BuildNodeCell(content, node, column, y));
                column = 1 - column;
            }

            if (tier >= 0) y += NodeHeight;
            // A tail of blank space so the last row clears the CONTINUE button when the
            // column is scrolled all the way down.
            UiFactory.SetContentHeight(content, y + 40f);

            scroll.gameObject.SetActive(false);
            return scroll.gameObject;
        }

        private NodeWidget BuildNodeCell(RectTransform content, SkillNode node, int column, float y)
        {
            string captured = node.Id;
            Button button = UiFactory.ActionButton(content, $"Node_{node.Id}", node.DisplayName,
                Locked, () => OnNodeTapped(captured));
            float xMin = column == 0 ? 0.015f : 0.515f;
            UiFactory.PlaceCell((RectTransform)button.transform, xMin, xMin + 0.470f, y, NodeHeight);

            var widget = new NodeWidget { NodeId = node.Id, Button = button };
            widget.Background = button.GetComponent<Image>();

            // ActionButton's own stretched label becomes the NAME line rather than being
            // left to overlap the description.
            widget.Name = button.GetComponentInChildren<Text>();
            widget.Name.fontSize = 23;
            widget.Name.horizontalOverflow = HorizontalWrapMode.Wrap;
            widget.Name.verticalOverflow = VerticalWrapMode.Truncate;
            UiFactory.PlaceRegion((RectTransform)widget.Name.transform, 0.06f, 0.60f, 0.94f, 0.96f);

            widget.Desc = UiFactory.Label(button.transform, "Desc", node.Description, 18, Color.white);
            widget.Desc.horizontalOverflow = HorizontalWrapMode.Wrap;
            widget.Desc.verticalOverflow = VerticalWrapMode.Truncate;
            widget.Desc.raycastTarget = false;
            UiFactory.PlaceRegion((RectTransform)widget.Desc.transform, 0.06f, 0.20f, 0.94f, 0.58f);

            widget.Pips = UiFactory.Label(button.transform, "Pips", string.Empty, 18, UiFactory.Gold,
                TextAnchor.MiddleRight);
            widget.Pips.raycastTarget = false;
            UiFactory.PlaceRegion((RectTransform)widget.Pips.transform, 0.30f, 0.02f, 0.94f, 0.19f);

            // Parented to the cell, so it draws above it and eats the tap that would
            // otherwise rank the node up.
            Button minus = UiFactory.ActionButton(button.transform, "Minus", "-",
                new Color(0.30f, 0.12f, 0.12f), () => OnMinusTapped(captured));
            UiFactory.PlaceRegion((RectTransform)minus.transform, 0.05f, 0.03f, 0.26f, 0.20f);
            minus.GetComponentInChildren<Text>().fontSize = 26;
            widget.Minus = minus.gameObject;

            return widget;
        }

        private GameObject BuildParagonPage(RectTransform root)
        {
            ScrollRect scroll = UiFactory.ScrollColumn(root, "Page_Paragon", out RectTransform content);
            UiFactory.PlaceRegion((RectTransform)scroll.transform, 0.025f, 0.185f, 0.975f, 0.830f);

            Text head = UiFactory.Label(content, "ParagonHead",
                "ENDLESS — every point still counts", 24, UiFactory.Arcane, TextAnchor.MiddleLeft);
            UiFactory.PlaceCell((RectTransform)head.transform, 0.02f, 0.98f, 0f, TierHeadHeight);

            float y = TierHeadHeight;
            foreach (Paragon.Track track in Paragon.Tracks)
            {
                string captured = track.Id;
                Button button = UiFactory.ActionButton(content, $"Track_{track.Id}", track.DisplayName,
                    Locked, () => _onParagon?.Invoke(captured));
                UiFactory.PlaceCell((RectTransform)button.transform, 0.015f, 0.985f, y, TrackHeight);
                y += TrackHeight + RowGap;

                var widget = new TrackWidget { TrackId = track.Id, Button = button };
                widget.Background = button.GetComponent<Image>();
                widget.Name = button.GetComponentInChildren<Text>();
                widget.Name.fontSize = 28;
                UiFactory.PlaceRegion((RectTransform)widget.Name.transform, 0.04f, 0.50f, 0.96f, 0.94f);

                widget.Detail = UiFactory.Label(button.transform, "Detail", string.Empty, 20,
                    UiFactory.Parchment);
                widget.Detail.raycastTarget = false;
                UiFactory.PlaceRegion((RectTransform)widget.Detail.transform, 0.04f, 0.08f, 0.96f, 0.48f);
                _tracks.Add(widget);
            }

            UiFactory.SetContentHeight(content, y + 40f);
            scroll.gameObject.SetActive(false);
            return scroll.gameObject;
        }

        private static string TabName(int tab) => tab switch
        {
            0 => "WARLORD",
            1 => "WARDEN",
            2 => "ZEALOT",
            3 => "HYBRID",
            _ => "PARAGON"
        };

        /// <summary>
        /// Crossroads tiers do not mean depth, they mean "this much in BOTH branches", so
        /// they get their own wording — labelling them "TIER 4" would be a lie about a rule
        /// the player has to understand to take one.
        /// </summary>
        private static string TierHeading(SkillBranch branch, int tier) =>
            branch == SkillBranch.Crossroads
                ? $"NEEDS {tier * SkillTree.TierUnlockCost} IN BOTH BRANCHES"
                : tier >= SkillTree.MaxTier
                    ? "KEYSTONE — CHOOSE ONE"
                    : $"TIER {tier} · {(tier - 1) * SkillTree.TierUnlockCost} POINTS IN BRANCH";

        // --- Lifecycle ---------------------------------------------------------

        public void Show(Action<string> onTake, Action<string> onUnlearn, Action<string> onParagon,
            Action onRespec, Action onContinue)
        {
            _onTake = onTake;
            _onUnlearn = onUnlearn;
            _onParagon = onParagon;
            _onRespec = onRespec;
            _onContinue = onContinue;
            DisarmAll();
            SelectTab(0);
            _root.SetActive(true);
        }

        public void Hide()
        {
            DisarmAll();
            _root.SetActive(false);
        }

        /// <summary>Repaint every node against the current ranks and point balance.</summary>
        public void Refresh(IReadOnlyDictionary<string, int> skillRanks,
            IReadOnlyDictionary<string, int> paragonRanks, int unspentPoints)
        {
            Copy(skillRanks, _ranks);
            Copy(paragonRanks, _paragon);
            _unspent = unspentPoints;

            // An armed undo whose rank is already gone has nothing left to confirm.
            if (_armedNode != null && SkillTree.RankOf(_ranks, _armedNode) <= 0) Disarm();
            if (SkillTree.PointsSpent(_ranks) == 0 && Paragon.TotalRanks(_paragon) == 0) DisarmRespec();

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

        // --- Painting ----------------------------------------------------------

        private void SelectTab(int tab)
        {
            _tab = Mathf.Clamp(tab, 0, TabCount - 1);
            for (int i = 0; i < TabCount; i++)
            {
                if (_pages[i] != null) _pages[i].SetActive(i == _tab);
                _tabFills[i].color = i == _tab ? Maxed : Available;
            }
            Paint();
        }

        private void Paint()
        {
            _pointsLabel.text = _unspent == 1 ? "1 point to spend" : $"{_unspent} points to spend";

            bool keystone = SkillTree.AnyKeystoneTaken(_ranks);

            foreach (NodeWidget widget in _widgets)
            {
                SkillNode node = SkillTree.Find(widget.NodeId);
                if (node == null) continue;

                int rank = SkillTree.RankOf(_ranks, node.Id);
                bool armed = rank > 0 && string.Equals(_armedNode, node.Id, StringComparison.Ordinal);
                string blocked = SkillTree.BlockedReason(node.Id, _ranks, _unspent);
                bool canTake = blocked == null;

                widget.Background.color = armed ? Arming
                    : rank >= node.MaxRanks && rank > 0 ? Maxed
                    : rank > 0 ? Ranked
                    : node.Kind == SkillKind.Keystone && canTake ? KeystoneTint
                    : canTake ? Available
                    : Locked;

                bool lit = rank > 0 || canTake;
                widget.Name.color = lit ? Color.white : Dim;
                widget.Desc.color = lit ? Color.white : Dim;

                // The bottom line is the node's status, and what counts as status changes
                // with state: ranks when it has some, what it is waiting for when it does
                // not, and the confirmation when an undo is armed.
                widget.Pips.text = armed ? "TAP - AGAIN"
                    : rank > 0 ? $"{rank}/{node.MaxRanks}"
                    : canTake ? (node.MaxRanks > 1 ? $"0/{node.MaxRanks}" : "READY")
                    : blocked;
                widget.Pips.color = armed ? Color.white
                    : rank > 0 || canTake ? UiFactory.Gold
                    : Dim;

                widget.Minus.SetActive(rank > 0);
                // Locked nodes stay live: the tap is how the player finds out WHY.
                widget.Button.interactable = true;
            }

            foreach (TrackWidget track in _tracks)
            {
                Paragon.Track def = Paragon.Find(track.TrackId);
                if (def == null) continue;

                int rank = _paragon.TryGetValue(track.TrackId, out int held) ? held : 0;
                int cost = Paragon.NextRankCost(Paragon.TotalRanks(_paragon));
                bool affordable = keystone && _unspent >= cost;

                track.Background.color = !keystone ? Locked : affordable ? Available : Ranked;
                track.Name.color = keystone ? Color.white : Dim;
                track.Detail.color = keystone ? UiFactory.Parchment : Dim;
                track.Detail.text = !keystone
                    ? "Locked until you take a keystone"
                    : $"Rank {rank} · {Describe(def, rank)} · next costs {cost}";
                track.Button.interactable = true;
            }

            int spent = SkillTree.PointsSpent(_ranks);
            int paragonRanks = Paragon.TotalRanks(_paragon);
            bool anything = spent > 0 || paragonRanks > 0;

            // FORGET ALL only exists once something has been learned, so CONTINUE has to
            // move when it goes — leaving it pinned right puts the screen's only call to
            // action off centre on the fresh tree every new player meets it in.
            _respecButton.gameObject.SetActive(anything);
            PlaceContinue(anything);
            _respecLabel.text = _respecIsArmed ? RespecArmed : RespecIdle;

            if (_tab == ParagonTab)
                _detailLabel.text = keystone
                    ? $"{paragonRanks} paragon rank{(paragonRanks == 1 ? string.Empty : "s")} · each rank costs more and gives a little less"
                    : "Paragon opens the moment you take a keystone.";
            else if (spent == 0)
                _detailLabel.text = "Spend four points in a branch to open its next tier.";
            else
                _detailLabel.text = $"{spent} point{(spent == 1 ? string.Empty : "s")} spent · tap - on a talent to take one back";
        }

        private static string Describe(Paragon.Track track, int rank)
        {
            float value = Paragon.ValueAt(track, rank);
            return value >= 10f ? $"+{value:0}" : $"+{value:0.##}";
        }

        private void PlaceContinue(bool sharingTheRow) =>
            UiFactory.Place(_continueRect, sharingTheRow ? 0.68f : 0.5f, 0.055f, 400f, 108f);

        // --- Input -------------------------------------------------------------

        private void OnNodeTapped(string nodeId)
        {
            DisarmAll();
            Paint();
            _onTake?.Invoke(nodeId);
        }

        private void OnMinusTapped(string nodeId)
        {
            if (string.Equals(_armedNode, nodeId, StringComparison.Ordinal))
            {
                DisarmAll();
                _onUnlearn?.Invoke(nodeId);
                return;
            }

            string blocked = SkillTree.UnlearnBlockedReason(nodeId, _ranks);
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
            ShowNote($"Tap - again to give back a rank of {SkillTree.Find(nodeId)?.DisplayName}.");
        }

        private void OnRespecPressed()
        {
            if (!_respecIsArmed)
            {
                Disarm();
                _respecIsArmed = true;
                _respecArmedFor = 0f;
                Paint();
                ShowNote("Forget every talent and paragon rank, and take all the points back?");
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

        private static void Copy(IReadOnlyDictionary<string, int> from, Dictionary<string, int> into)
        {
            into.Clear();
            if (from == null) return;
            foreach (KeyValuePair<string, int> pair in from) into[pair.Key] = pair.Value;
        }
    }
}
