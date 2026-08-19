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
        }

        public void Tick(float dt)
        {
            if (_awaitingPrompt || _finished) return;

            RunState run = _ctx.Run;
            float speed = _ctx.Config.Balance.RunSpeedMetersPerSec;

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
            _ctx.Resurrect.Show(
                _ctx.Ads.IsRewardedReady(AdPlacement.Resurrect),
                onResurrect: () => _ctx.Ads.ShowRewarded(AdPlacement.Resurrect, granted =>
                {
                    _ctx.Resurrect.Hide();
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
                }),
                onGiveUp: () =>
                {
                    _ctx.Resurrect.Hide();
                    GiveUp();
                });
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
