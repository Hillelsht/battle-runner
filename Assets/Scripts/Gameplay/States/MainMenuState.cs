using BattleRunner.Core.Flow;
using BattleRunner.Core.Save;
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
            // The WORLD's name, not the level asset's. An act wears one world for three to
            // five rounds while the level list cycles on its own period, so showing the level
            // name here would tell the player they are entering somewhere they are not.
            BattleRunner.Core.World.WorldTheme theme = BattleRunner.Core.World.WorldThemes.For(
                BattleRunner.Core.Progression.RoundPlan.For(_ctx.Profile.CurrentLevelIndex));
            _ctx.MenuScreen.Show(_ctx.Profile.CurrentLevelIndex,
                theme != null ? theme.DisplayName : "???", summary);
        }

        public void Tick(float deltaTime) { }

        public void Exit() => _ctx.MenuScreen.Hide();

        public void OnPlayPressed() => _ctx.Machine.TransitionTo(_ctx.RunLoadingState);

        /// <summary>Back to the slot picker to load or erase a different save.</summary>
        public void OnNewRunPressed() => _ctx.Machine.TransitionTo(_ctx.SlotState);
    }
}
