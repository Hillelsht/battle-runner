using System;
using BattleRunner.Core.Flow;
using BattleRunner.Core.Progression;

namespace BattleRunner.Gameplay.States
{
    /// <summary>
    /// Spend boss-kill points in the talent tree, then one tap out (doc 01, R6).
    ///
    /// Replaces three flat stat buttons whose shared flaw was that all three only mattered
    /// during the boss fight — nothing bought here changed the run itself. The tree's Zealot
    /// path does.
    ///
    /// Every choice is reversible: a talent can be handed back for its point, and FORGET ALL
    /// empties the tree. The screen owns the two-tap confirmation; this state owns the
    /// profile, so it re-checks the rule before touching it either way.
    /// </summary>
    public sealed class StatUpgradeState : IGameState
    {
        private readonly GameContext _ctx;

        public StatUpgradeState(GameContext ctx) => _ctx = ctx;

        public void Enter()
        {
            _ctx.SkillScreen.Show(OnTake, OnUnlearn, OnRespec, OnContinue);
            Refresh();
        }

        public void Tick(float deltaTime) => _ctx.SkillScreen.Tick(deltaTime);

        public void Exit() => _ctx.SkillScreen.Hide();

        private void Refresh() =>
            _ctx.SkillScreen.Refresh(_ctx.Profile.SkillNodes, _ctx.Profile.UnspentStatPoints);

        private void OnTake(string nodeId)
        {
            // A refused tap says why. Silently doing nothing reads as a broken button, and
            // the reasons here ("needs the first talent of this path") are the rules of the
            // tree — the player learns them by bumping into them.
            string blocked = SkillTree.BlockedReason(nodeId, _ctx.Profile.SkillNodes, _ctx.Profile.UnspentStatPoints);
            if (blocked != null)
            {
                _ctx.SkillScreen.ShowNote(blocked);
                return;
            }

            _ctx.Profile.SkillNodes.Add(nodeId);
            _ctx.Profile.UnspentStatPoints -= SkillTree.PointCost;
            Commit(null);
        }

        private void OnUnlearn(string nodeId)
        {
            string blocked = SkillTree.UnlearnBlockedReason(nodeId, _ctx.Profile.SkillNodes);
            if (blocked != null)
            {
                _ctx.SkillScreen.ShowNote(blocked);
                return;
            }

            // RemoveAll, not Remove: a save mangled into holding an id twice should come out
            // of this clean, and the refund follows what was actually taken out.
            int removed = _ctx.Profile.SkillNodes.RemoveAll(
                id => string.Equals(id, nodeId, StringComparison.Ordinal));
            _ctx.Profile.UnspentStatPoints += removed * SkillTree.PointCost;

            Commit($"{SkillTree.Find(nodeId)?.DisplayName} unlearned. Point refunded.");
        }

        private void OnRespec()
        {
            // PointsSpent counts only real talents, so junk left by an old save is cleared
            // without paying for it — it never cost a point in the first place.
            int refund = SkillTree.PointsSpent(_ctx.Profile.SkillNodes);
            _ctx.Profile.SkillNodes.Clear();
            _ctx.Profile.UnspentStatPoints += refund;

            Commit("All talents forgotten. Spend the points again.");
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
