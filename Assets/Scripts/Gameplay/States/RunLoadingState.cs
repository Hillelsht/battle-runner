using BattleRunner.Core.Flow;
using BattleRunner.Core.Run;
using BattleRunner.Data.Definitions;
using BattleRunner.Meta.Services;

namespace BattleRunner.Gameplay.States
{
    /// <summary>
    /// Builds the level from pooled content and resolves the run's StatSheet — the
    /// greybox equivalent of the masked loading step (doc 03). One frame, no hitch:
    /// pools were prewarmed at bootstrap.
    /// </summary>
    public sealed class RunLoadingState : IGameState
    {
        private readonly GameContext _ctx;

        public RunLoadingState(GameContext ctx) => _ctx = ctx;

        public void Enter()
        {
            LevelDefinition level = _ctx.CurrentLevel;

            _ctx.ArenaRoot.SetActive(true);
            _ctx.TrackController.BuildLevel(level);
            _ctx.Crowd.ResetRun(level.StartingForce, 0f);
            _ctx.CameraRig.SnapToCrowd();

            _ctx.CurrentStats = ProfileStatsResolver.Resolve(_ctx.Profile, _ctx.Config);
            _ctx.Spell.ApplyStats(_ctx.CurrentStats);
            _ctx.Shield.ApplyStats(_ctx.CurrentStats);
            _ctx.Spell.ResetForPhase();
            _ctx.Shield.ResetForPhase();

            _ctx.Run = new RunState
            {
                ForceCount = level.StartingForce,
                SpellCharges = 0
            };
            _ctx.LastResult = null;
        }

        public void Tick(float deltaTime) => _ctx.Machine.TransitionTo(_ctx.RunnerState);

        public void Exit() { }
    }
}
