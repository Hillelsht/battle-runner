using BattleRunner.Core.Flow;
using BattleRunner.Core.Feel;
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
            _ctx.Effects.Clear();
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
            float speed = _ctx.Tutorial.HoldsRun
                ? 0f
                : _ctx.Config.Balance.RunSpeedMetersPerSec * (1f + _ctx.CurrentStats.Get(StatIds.RunSpeed));

            _ctx.Crowd.AdvanceZ(speed * dt);
            run.Distance += speed * dt;
            _ctx.Crowd.Tick(dt);
            _ctx.TrackController.Tick(_ctx.Crowd);

            _ctx.Spell.Tick(dt);
            _ctx.Shield.Tick(dt);
            _ctx.Hud.SetCooldowns(_ctx.Spell.CooldownRemaining, _ctx.Shield.CooldownRemaining, _ctx.Shield.IsActive);
        }

        // Peak channels sit near 1.6, not 2.5+. These reach the GPU through a
        // MaterialPropertyBlock, and whether Unity gamma-expands a Color set that way in a
        // linear project is the one thing here I could not settle from the container. At
        // 1.6 the effect clears the 0.85 bloom threshold comfortably if the value is taken
        // raw, and is hot-but-not-absurd if it is expanded (1.6^2.2 = 2.9). At 2.5 the
        // expanded case would be 8.5 and the screen would white out. A device screenshot
        // decides which, and then these can be tuned in one direction with confidence.
        private static readonly Color SpellTint = new Color(0.40f, 0.70f, 1.60f);
        private static readonly Color LossTint = new Color(1.55f, 0.32f, 0.23f);

        private static Color GateTint(GateOp op) => op switch
        {
            GateOp.Multiply => new Color(1.60f, 1.10f, 0.34f),
            GateOp.Subtract => new Color(1.55f, 0.29f, 0.26f),
            _ => new Color(0.33f, 0.74f, 1.70f)
        };

        private void OnFlickUp() => _ctx.Spell.TryCast();
        private void OnFlickDown() => _ctx.Shield.TryRaise();

        private void OnSpellCast()
        {
            float range = _ctx.Config.Spells.ClearRangeMeters;
            int cleared = _ctx.TrackController.ClearEnemiesAhead(_ctx.Crowd.CenterZ, range);

            // A ring that sprints out to the spell's ACTUAL clear range, so the player
            // learns how far the flick reaches by watching it rather than by dying to a
            // pack that was one metre outside it.
            _ctx.Effects.Shock(new Vector3(_ctx.Crowd.CenterX, 0f, _ctx.Crowd.CenterZ),
                SpellTint, 1.2f, range, 0.42f);
            _ctx.Effects.Burst(new Vector3(_ctx.Crowd.CenterX, 0f, _ctx.Crowd.CenterZ + 1.5f),
                SpellTint, 10, 4.2f, 0.5f);

            if (cleared > 0)
                Debug.Log($"[Run] Spell cleared {cleared} enemy pack(s).");
        }

        private void OnGateApplied(GateOp op, int value, Vector3 where)
        {
            RunState run = _ctx.Run;
            long before = run.ForceCount;
            run.ForceCount = GateMath.ApplyGateWithYield(run.ForceCount, op, value,
                _ctx.Config.Balance.SoftCap, _ctx.CurrentStats.Get(StatIds.GateYield), out long overflow);
            run.OverflowAccumulated += overflow;
            run.GatesHit++;
            _ctx.Crowd.SetForce(run.ForceCount);
            _ctx.Hud.SetForce(run.ForceCount);

            // Scaled by the RATIO, so a x2 lands the same at 10 units and at 1000.
            _ctx.CameraRig.Apply(CameraFeel.ForGate(op, before, run.ForceCount));
            if (run.ForceCount > before)
                _ctx.CameraRig.PunchFov(1.4f + 2.6f * CameraFeel.ForGate(op, before, run.ForceCount).Trauma);

            // Sized by the same octave ratio the camera uses, so a x2 at 10 units and a x2
            // at 1000 throw the same ring — and a +1 barely ripples. The gate is the whole
            // game and until now passing one produced no event at all: the number changed,
            // the camera nudged, and that was it.
            float weight = CameraFeel.ForGate(op, before, run.ForceCount).Trauma;
            Color tint = GateTint(op);
            _ctx.Effects.Shock(where, tint, 0.8f, 2.6f + 4.4f * weight, 0.34f + 0.16f * weight);
            if (run.ForceCount > before)
                _ctx.Effects.Burst(where, tint, 4 + Mathf.RoundToInt(10f * weight), 3.4f, 0.45f);

            if (run.ForceCount <= 0) OnForceDepleted();
        }

        private void OnEnemyContact(int forceCost, Vector3 where)
        {
            if (_ctx.Shield.IsActive) return;

            RunState run = _ctx.Run;
            // Resist shrugs off part of the bite; capped so a pack always costs something.
            float resist = System.Math.Min(0.85f, _ctx.CurrentStats.Get(StatIds.EnemyResist));
            long bite = (long)System.Math.Ceiling(forceCost * (1.0 - resist));
            long beforeBite = run.ForceCount;
            run.ForceCount = System.Math.Max(0L, run.ForceCount - bite);
            _ctx.Crowd.SetForce(run.ForceCount);
            _ctx.Hud.SetForce(run.ForceCount);

            _ctx.CameraRig.Apply(CameraFeel.ForLoss(beforeBite, run.ForceCount));

            // Debris scaled to what the pack actually took, not to its printed cost —
            // Bramble and Undying cut the bite, and the effect should show the bite.
            float loss = CameraFeel.ForLoss(beforeBite, run.ForceCount).Trauma;
            _ctx.Effects.Shock(where, LossTint, 0.6f, 2.2f + 2.6f * loss, 0.30f);
            _ctx.Effects.Burst(where, LossTint, 6 + Mathf.RoundToInt(14f * loss), 3.8f, 0.55f);

            if (run.ForceCount <= 0) OnForceDepleted();
        }

        private void OnForceDepleted()
        {
            _awaitingPrompt = true;
            // Tick() stops here, so a live coaching prompt would sit frozen behind the
            // resurrect modal. Drop it; an un-taught step re-arms if the player revives.
            _ctx.Tutorial.EndPhase();
            // Same reason for the ward: CancelActive drops the block window without
            // refunding the cooldown, which ResetForPhase would.
            _ctx.Shield.CancelActive();
            _ctx.Ward.Clear();
            // Embers still arcing behind a modal read as the game continuing underneath it.
            _ctx.Effects.Clear();
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
