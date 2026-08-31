using BattleRunner.Core.Flow;
using BattleRunner.Core.Run;
using BattleRunner.Core.Stats;
using BattleRunner.Meta.Services;
using UnityEngine;

namespace BattleRunner.Gameplay.States
{
    /// <summary>
    /// The runner phase: drag steers, gates do math, enemies bite, flicks cast.
    /// All gate/enemy math flows through Core (GateMath) so behavior matches the
    /// tested arithmetic exactly.
    /// </summary>
    public sealed class RunnerLoopState : IGameState
    {
        private readonly GameContext _ctx;
        private bool _awaitingPrompt;
        private bool _finished;

        public RunnerLoopState(GameContext ctx) => _ctx = ctx;

        public void Enter()
        {
            _awaitingPrompt = false;
            _finished = false;

            _ctx.Hud.Show();
            _ctx.Hud.SetForce(_ctx.Run.ForceCount);
            _ctx.Hud.HideBossBar();

            _ctx.LaneTargetChannel.Subscribe(_ctx.Crowd.OnLaneTarget);
            _ctx.FlickUpChannel.Subscribe(OnFlickUp);
            _ctx.FlickDownChannel.Subscribe(OnFlickDown);
            _ctx.Spell.Cast += OnSpellCast;

            _ctx.TrackController.GateApplied += OnGateApplied;
            _ctx.TrackController.EnemyContact += OnEnemyContact;
            _ctx.TrackController.FinishReached += OnFinishReached;

            _ctx.Tutorial.Subscribe();
        }

        public void Exit()
        {
            _ctx.LaneTargetChannel.Unsubscribe(_ctx.Crowd.OnLaneTarget);
            _ctx.FlickUpChannel.Unsubscribe(OnFlickUp);
            _ctx.FlickDownChannel.Unsubscribe(OnFlickDown);
            _ctx.Spell.Cast -= OnSpellCast;

            _ctx.TrackController.GateApplied -= OnGateApplied;
            _ctx.TrackController.EnemyContact -= OnEnemyContact;
            _ctx.TrackController.FinishReached -= OnFinishReached;

            _ctx.Tutorial.Unsubscribe();
            _ctx.Tutorial.EndPhase();
        }

        public void Tick(float dt)
        {
            if (_awaitingPrompt || _finished) return;

            // The coach runs on unscaled time so a held prompt still times out even while
            // the world is standing still for it.
            _ctx.Tutorial.TickRunner(dt);

            RunState run = _ctx.Run;

            // A held prompt stops the road, not the game: cooldowns, the HUD, input and the
            // ad service all keep ticking below. Time.timeScale would freeze those too, and
            // a stalled rewarded-ad callback is the one failure that can wedge a run.
            float speed = _ctx.Tutorial.HoldsRun ? 0f : _ctx.Config.Balance.RunSpeedMetersPerSec;

            _ctx.Crowd.AdvanceZ(speed * dt);
            run.Distance += speed * dt;
            _ctx.Crowd.Tick(dt);
            _ctx.TrackController.Tick(_ctx.Crowd);

            _ctx.Spell.Tick(dt);
            _ctx.Shield.Tick(dt);
            _ctx.Hud.SetCooldowns(_ctx.Spell.CooldownRemaining, _ctx.Shield.CooldownRemaining, _ctx.Shield.IsActive);
        }

        private void OnFlickUp() => _ctx.Spell.TryCast();
        private void OnFlickDown() => _ctx.Shield.TryRaise();

        private void OnSpellCast()
        {
            int cleared = _ctx.TrackController.ClearEnemiesAhead(
                _ctx.Crowd.CenterZ, _ctx.Config.Spells.ClearRangeMeters);
            if (cleared > 0)
                Debug.Log($"[Run] Spell cleared {cleared} enemy pack(s).");
        }

        private void OnGateApplied(GateOp op, int value)
        {
            RunState run = _ctx.Run;
            run.ForceCount = GateMath.ApplyGate(run.ForceCount, op, value,
                _ctx.Config.Balance.SoftCap, out long overflow);
            run.OverflowAccumulated += overflow;
            run.GatesHit++;
            _ctx.Crowd.SetForce(run.ForceCount);
            _ctx.Hud.SetForce(run.ForceCount);

            if (run.ForceCount <= 0) OnForceDepleted();
        }

        private void OnEnemyContact(int forceCost)
        {
            if (_ctx.Shield.IsActive) return;

            RunState run = _ctx.Run;
            run.ForceCount = System.Math.Max(0L, run.ForceCount - forceCost);
            _ctx.Crowd.SetForce(run.ForceCount);
            _ctx.Hud.SetForce(run.ForceCount);

            if (run.ForceCount <= 0) OnForceDepleted();
        }

        private void OnForceDepleted()
        {
            _awaitingPrompt = true;
            // Tick() stops here, so a live coaching prompt would sit frozen behind the
            // resurrect modal. Drop it; an un-taught step re-arms if the player revives.
            _ctx.Tutorial.EndPhase();
            _ctx.Resurrect.Show(
                _ctx.Ads.IsRewardedReady(AdPlacement.Resurrect),
                onResurrect: () =>
                {
                    _ctx.Resurrect.Hide(); // no re-taps while the ad plays (review C2)
                    _ctx.Ads.ShowRewarded(AdPlacement.Resurrect, granted => OnResurrectResult(granted));
                },
                onGiveUp: () =>
                {
                    _ctx.Resurrect.Hide();
                    GiveUp();
                });
        }

        private void OnResurrectResult(bool granted)
        {
            if (!ReferenceEquals(_ctx.Machine.Current, this)) return;
            if (granted)
            {
                long revived = System.Math.Max(10L, _ctx.CurrentLevel.ParForceAtFinish / 4);
                _ctx.Run.ForceCount = revived;
                _ctx.Crowd.SetForce(revived);
                _ctx.Hud.SetForce(revived);
                _awaitingPrompt = false;
            }
            else
            {
                GiveUp();
            }
        }

        private void GiveUp()
        {
            _ctx.SaveProfile();
            _ctx.Machine.TransitionTo(_ctx.MenuState);
        }

        private void OnFinishReached()
        {
            _finished = true;
            _ctx.LastResult = new RunResult
            {
                FinalForceCount = _ctx.Run.ForceCount,
                OverflowAccumulated = _ctx.Run.OverflowAccumulated,
                HeroStats = _ctx.CurrentStats,
                SpellChargesRemaining = _ctx.Spell.Ready ? 1 : 0,
                Distance = _ctx.Run.Distance,
                GatesHit = _ctx.Run.GatesHit,
                ReachedBoss = true
            };
            _ctx.Machine.TransitionTo(_ctx.BossState);
        }
    }
}
