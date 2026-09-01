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
            var level = _ctx.CurrentLevel;
            _ctx.MenuScreen.Show(_ctx.Profile.CurrentLevelIndex,
                level != null ? level.DisplayName : "???", summary);
        }

        public void Tick(float deltaTime) => _ctx.MenuScreen.Tick(deltaTime);

        public void Exit() => _ctx.MenuScreen.Hide();

        public void OnPlayPressed() => _ctx.Machine.TransitionTo(_ctx.RunLoadingState);

        /// <summary>
        /// Wipe the save and start over, tutorial and all. Stamped at the current schema so
        /// no migration runs — the v2 to v3 step marks the tutorial already taught, which is
        /// right for a returning player's save but would defeat the whole point here.
        /// </summary>
        public void OnNewRunPressed()
        {
            _ctx.Profile = new PlayerProfile { SchemaVersion = SaveMigrator.CurrentVersion };
            // Reset BEFORE saving: SaveProfile persists the coach onto the profile, so a
            // coach still holding the previous player's progress would stamp it right back
            // onto the fresh profile and the tutorial would never replay.
            _ctx.Tutorial.ResetProgress();
            _ctx.SaveProfile();
            Enter(); // refresh the menu against the fresh profile
        }
    }
}
