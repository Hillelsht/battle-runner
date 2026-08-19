using BattleRunner.Core.Flow;
using BattleRunner.Core.Stats;

namespace BattleRunner.Gameplay.States
{
    /// <summary>Three buttons, a recommended star, one tap out (doc 01, R6).</summary>
    public sealed class StatUpgradeState : IGameState
    {
        private readonly GameContext _ctx;

        public StatUpgradeState(GameContext ctx) => _ctx = ctx;

        public void Enter()
        {
            _ctx.StatScreen.Show(OnSpend, OnContinue);
            Refresh();
        }

        public void Tick(float deltaTime) { }

        public void Exit() => _ctx.StatScreen.Hide();

        private void Refresh()
        {
            var balance = _ctx.Config.Balance;
            _ctx.StatScreen.Refresh(
                _ctx.Profile.UnspentStatPoints,
                _ctx.Profile.GetStatPoints(StatIds.Damage),
                _ctx.Profile.GetStatPoints(StatIds.Health),
                _ctx.Profile.GetStatPoints(StatIds.Cooldown),
                balance.DamagePerPoint,
                balance.HealthPerPoint,
                balance.CooldownPerPoint);
        }

        private void OnSpend(string statId)
        {
            if (_ctx.Profile.UnspentStatPoints <= 0) return;
            _ctx.Profile.UnspentStatPoints--;
            _ctx.Profile.AddStatPoint(statId);
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
