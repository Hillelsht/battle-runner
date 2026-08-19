using BattleRunner.Core.Flow;
using BattleRunner.Meta.Services;

namespace BattleRunner.Gameplay.States
{
    public sealed class MainMenuState : IGameState
    {
        private readonly GameContext _ctx;

        public MainMenuState(GameContext ctx) => _ctx = ctx;

        public void Enter()
        {
            _ctx.ArenaRoot.SetActive(false);
            _ctx.Hud.Hide();

            _ctx.CurrentStats = ProfileStatsResolver.Resolve(_ctx.Profile, _ctx.Config);
            string summary = ProfileStatsResolver.Summary(_ctx.Profile, _ctx.Config, _ctx.CurrentStats);
            var level = _ctx.CurrentLevel;
            _ctx.MenuScreen.Show(_ctx.Profile.CurrentLevelIndex,
                level != null ? level.DisplayName : "???", summary);
        }

        public void Tick(float deltaTime) { }

        public void Exit() => _ctx.MenuScreen.Hide();

        public void OnPlayPressed() => _ctx.Machine.TransitionTo(_ctx.RunLoadingState);
    }
}
