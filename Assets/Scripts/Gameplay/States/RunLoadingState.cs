using BattleRunner.Core.Flow;
using BattleRunner.Core.Audio;
using BattleRunner.Core.Progression;
using BattleRunner.Core.Run;
using BattleRunner.Core.World;
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

            // The world is dressed BEFORE the road is built, so the very first frame of a
            // round is already the right place rather than the last round's place repainted.
            // This is the only per-round moment that holds the round index while nothing is
            // on screen: the index is stable from here until StatUpgradeState advances it.
            RoundPlan plan = RoundPlan.For(_ctx.Profile.CurrentLevelIndex);
            WorldTheme theme = WorldThemes.For(plan);
            ThemeVariant variant = ThemeVariant.For(plan);
            EnvironmentLook.ApplyTheme(theme, variant);
            _ctx.TrackController.ApplyTheme(theme, variant);

            // The road is generated for THIS round rather than read off one of six baked
            // levels cycled forever — which is why round eight used to replay round two's
            // gates. Eight chunk shapes, sequenced from the round index.
            ChunkLayout[] layouts = ChunkLayouts.BuildRound(plan);

            // THE ARMY IS CARRIED IN. It used to be set to level.StartingForce — five men —
            // on every single round, which is the whole of "now every round the crowd shrinks
            // to minimum". StartOfRound is what replaces it: whatever survived the last round,
            // lifted to the permanent floor, so this number can never be smaller than it was
            // at the start of any previous round.
            _ctx.ArmyFloor = StandingArmy.Floor(_ctx.Profile.ArmyBestEver);
            double army = StandingArmy.StartOfRound(_ctx.Profile.ArmyBanked, _ctx.Profile.ArmyBestEver);
            _ctx.CurrentPar = ChunkLayouts.EstimateParForce(layouts, army);

            _ctx.ArenaRoot.SetActive(true);
            _ctx.TrackController.BuildLevel(layouts);

            // After BuildLevel, because the verge is dressed to the road's actual length.
            // It runs a little past the ground strip's own end so nothing pops in at the
            // horizon while the player is still looking at it through fog.
            _ctx.Props.Build(theme, variant, _ctx.Profile.CurrentLevelIndex,
                -20f, _ctx.TrackController.FinishZ + 210f);
            // The music is bent to the world by the same two things the player can already
            // see: how far they can see (fog decides the filter) and how cold the sky is
            // (the zenith decides the pitch). One bed serves all eight worlds — eight beds
            // would outweigh the entire rest of the project.
            _ctx.Audio.SetMood(MusicMood.For(theme.FogEnd,
                theme.SkyZenith.R, theme.SkyZenith.B, theme.Accent.Chroma));
            _ctx.Audio.SetCombat(false);
            _ctx.Audio.Play(AudioCue.RoundStart);

            _ctx.Crowd.ResetRun(army, 0f);
            _ctx.CameraRig.SnapToCrowd();

            _ctx.CurrentStats = ProfileStatsResolver.Resolve(_ctx.Profile, _ctx.Config);
            _ctx.Spell.ApplyStats(_ctx.CurrentStats);
            _ctx.Shield.ApplyStats(_ctx.CurrentStats);
            _ctx.Spell.ResetForPhase();
            _ctx.Shield.ResetForPhase();

            _ctx.Run = new RunState { StartingForce = army };
            _ctx.Run.SetForce(army);
            _ctx.Run.SpellCharges = _ctx.Spell.Charges;

            _ctx.LastResult = null;
        }

        public void Tick(float deltaTime) => _ctx.Machine.TransitionTo(_ctx.RunnerState);

        public void Exit() { }
    }
}
