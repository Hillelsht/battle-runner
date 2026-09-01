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
    /// </summary>
    public sealed class StatUpgradeState : IGameState
    {
        private readonly GameContext _ctx;

        public StatUpgradeState(GameContext ctx) => _ctx = ctx;

        public void Enter()
        {
            _ctx.SkillScreen.Show(OnTake, OnContinue);
            Refresh();
        }

        public void Tick(float deltaTime) { }

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
                _ctx.SkillScreen.ShowRefusal(blocked);
                return;
            }

            _ctx.Profile.SkillNodes.Add(nodeId);
            _ctx.Profile.UnspentStatPoints -= SkillTree.PointCost;

            // Re-resolve immediately: the menu summary and the next run both read CurrentStats.
            _ctx.CurrentStats = Meta.Services.ProfileStatsResolver.Resolve(_ctx.Profile, _ctx.Config);
            _ctx.SaveProfile();
            Refresh();
        }

        private void OnContinue()
        {
            _ctx.Profile.CurrentLevelIndex++;
            _ctx.SaveProfile();
            _ctx.Machine.TransitionTo(_ctx.MenuState);
        }
    }
}
