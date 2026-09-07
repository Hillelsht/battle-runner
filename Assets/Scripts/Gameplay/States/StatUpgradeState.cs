using System.Collections.Generic;
using BattleRunner.Core.Flow;
using BattleRunner.Core.Progression;
using BattleRunner.Core.Save;

namespace BattleRunner.Gameplay.States
{
    /// <summary>
    /// Spend boss-kill points in the talent tree, then one tap out (doc 01, R6).
    ///
    /// The tree is ranked now, so this state moves counts around a map rather than adding
    /// and removing ids from a set. Every choice is still reversible: a rank can be handed
    /// back for its point and FORGET ALL empties the tree, because a tree this size that a
    /// player meets three talents in is a trap if they cannot walk it back.
    ///
    /// Points also flow into the endless paragon pool once a keystone is held, so a player
    /// at round fifty still has somewhere to put them — the old tree ran dry after three
    /// boss kills and every point after that vanished.
    ///
    /// The screen owns the two-tap confirmation; this state owns the profile, so it
    /// re-checks every rule against Core before touching it either way.
    /// </summary>
    public sealed class StatUpgradeState : IGameState
    {
        private readonly GameContext _ctx;

        public StatUpgradeState(GameContext ctx) => _ctx = ctx;

        public void Enter()
        {
            _ctx.SkillScreen.Show(OnTake, OnUnlearn, OnParagon, OnRespec, OnContinue);
            Refresh();
        }

        public void Tick(float deltaTime) => _ctx.SkillScreen.Tick(deltaTime);

        public void Exit() => _ctx.SkillScreen.Hide();

        private void Refresh() =>
            _ctx.SkillScreen.Refresh(_ctx.Profile.SkillRankMap(), _ctx.Profile.ParagonRankMap(),
                _ctx.Profile.UnspentStatPoints);

        private void OnTake(string nodeId)
        {
            Dictionary<string, int> ranks = _ctx.Profile.SkillRankMap();

            // A refused tap says why. Silently doing nothing reads as a broken button, and
            // the reasons here ("Needs 8 points in Warlord") are the rules of the tree —
            // the player learns them by bumping into them.
            string blocked = SkillTree.BlockedReason(nodeId, ranks, _ctx.Profile.UnspentStatPoints);
            if (blocked != null)
            {
                _ctx.SkillScreen.ShowNote(blocked);
                return;
            }

            ranks[nodeId] = SkillTree.RankOf(ranks, nodeId) + 1;
            PlayerProfile.WriteMap(_ctx.Profile.SkillRanks, ranks);
            _ctx.Profile.UnspentStatPoints -= SkillTree.PointCost;
            Commit(null);
        }

        private void OnUnlearn(string nodeId)
        {
            Dictionary<string, int> ranks = _ctx.Profile.SkillRankMap();

            string blocked = SkillTree.UnlearnBlockedReason(nodeId, ranks);
            if (blocked != null)
            {
                _ctx.SkillScreen.ShowNote(blocked);
                return;
            }

            int rank = SkillTree.RankOf(ranks, nodeId);
            if (rank <= 1) ranks.Remove(nodeId); else ranks[nodeId] = rank - 1;
            PlayerProfile.WriteMap(_ctx.Profile.SkillRanks, ranks);
            _ctx.Profile.UnspentStatPoints += SkillTree.PointCost;

            Commit($"{SkillTree.Find(nodeId)?.DisplayName} refunded.");
        }

        private void OnParagon(string trackId)
        {
            Dictionary<string, int> paragon = _ctx.Profile.ParagonRankMap();
            Dictionary<string, int> ranks = _ctx.Profile.SkillRankMap();

            string blocked = Paragon.BlockedReason(trackId, paragon, ranks, _ctx.Profile.UnspentStatPoints);
            if (blocked != null)
            {
                _ctx.SkillScreen.ShowNote(blocked);
                return;
            }

            // Cost is read BEFORE the rank lands, or the player pays next rank's price.
            int cost = Paragon.NextRankCost(Paragon.TotalRanks(paragon));
            paragon[trackId] = (paragon.TryGetValue(trackId, out int held) ? held : 0) + 1;
            PlayerProfile.WriteMap(_ctx.Profile.ParagonRanks, paragon);
            _ctx.Profile.UnspentStatPoints -= cost;

            Commit($"{Paragon.Find(trackId)?.DisplayName} deepened.");
        }

        private void OnRespec()
        {
            // Only real nodes and tracks are refunded, so junk left by an old save is
            // cleared without paying for it — it never cost a point in the first place.
            // Paragon refunds at its ESCALATED prices, walking the cost curve back down,
            // or a player would lose points simply for having gone deep.
            int refund = SkillTree.PointsSpent(_ctx.Profile.SkillRankMap());

            Dictionary<string, int> paragon = _ctx.Profile.ParagonRankMap();
            int held = Paragon.TotalRanks(paragon);
            for (int r = held - 1; r >= 0; r--) refund += Paragon.NextRankCost(r);

            _ctx.Profile.SkillRanks.Clear();
            _ctx.Profile.ParagonRanks.Clear();
            _ctx.Profile.UnspentStatPoints += refund;

            Commit("Everything forgotten. Spend the points again.");
        }

        private void Commit(string note)
        {
            // Re-resolve immediately: the menu summary and the next run both read CurrentStats.
            _ctx.CurrentStats = Meta.Services.ProfileStatsResolver.Resolve(_ctx.Profile, _ctx.Config);
            _ctx.SaveProfile();
            Refresh();
            if (note != null) _ctx.SkillScreen.ShowNote(note);
        }

        private void OnContinue()
        {
            _ctx.Profile.CurrentLevelIndex++;
            _ctx.SaveProfile();
            _ctx.Machine.TransitionTo(_ctx.MenuState);
        }
    }
}
