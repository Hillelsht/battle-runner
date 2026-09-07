using BattleRunner.Core.Feel;
using BattleRunner.Core.Boss;
using BattleRunner.Core.Flow;
using BattleRunner.Core.Run;
using BattleRunner.Core.Stats;
using BattleRunner.Data.Definitions;
using BattleRunner.Meta.Services;
using UnityEngine;

namespace BattleRunner.Gameplay.States
{
    /// <summary>
    /// The boss consumes the RunResult contract — force, stats, overflow — via BossSim
    /// (doc 01, R9). Attacks telegraph for the shield window; flick-up burns spell bursts.
    /// </summary>
    public sealed class BossEncounterState : IGameState
    {
        private readonly GameContext _ctx;
        private BossDefinition _boss;
        private float _bossHpMax;
        private float _bossHp;
        private Vector3 _bossPosition;
        private float _attackTimer;
        private bool _resolved;
        private bool _awaitingPrompt;

        public BossEncounterState(GameContext ctx) => _ctx = ctx;

        /// <summary>True while the boss is winding up — the window a shield must land in.</summary>
        public bool TelegraphActive => _boss != null && _attackTimer <= _boss.TelegraphSeconds;

        public void Enter()
        {
            _boss = _ctx.CurrentLevel.Boss;
            _resolved = false;
            _awaitingPrompt = false;

            _bossHpMax = BossSim.BossHp(_boss.BaseHp, _boss.PerLevelGrowth, _ctx.Profile.CurrentLevelIndex);
            _bossHp = _bossHpMax;
            _attackTimer = _boss.AttackIntervalSeconds;

            // Captured, not recomputed. BossView.Show pins the boss HERE for the whole
            // encounter while the crowd keeps ticking, so deriving the position from
            // Crowd.CenterZ later would walk the effects away from the body they belong to.
            _bossPosition = new Vector3(0f, 0f, _ctx.Crowd.CenterZ + 16f);
            _ctx.BossView.Show(_boss, _bossPosition);
            _ctx.Hud.ShowBossBar(_boss.DisplayName);
            _ctx.Hud.SetBossHp(1f);

            _ctx.Spell.ResetForPhase();
            _ctx.Shield.ResetForPhase();

            _ctx.LaneTargetChannel.Subscribe(_ctx.Crowd.OnLaneTarget);
            _ctx.FlickUpChannel.Subscribe(OnFlickUp);
            _ctx.FlickDownChannel.Subscribe(OnFlickDown);
            _ctx.Spell.Cast += OnSpellCast;

            // RunnerLoopState.Exit unsubscribed the coach before we got here, so the shield
            // lesson would never hear Shield.Raised without this.
            _ctx.Tutorial.Subscribe();
        }

        public void Exit()
        {
            _ctx.LaneTargetChannel.Unsubscribe(_ctx.Crowd.OnLaneTarget);
            _ctx.FlickUpChannel.Unsubscribe(OnFlickUp);
            _ctx.FlickDownChannel.Unsubscribe(OnFlickDown);
            _ctx.Spell.Cast -= OnSpellCast;

            _ctx.Tutorial.Unsubscribe();
            _ctx.Tutorial.EndPhase();

            _ctx.BossView.Hide();
            _ctx.Hud.HideBossBar();
            _ctx.Dome.Clear();
            _ctx.Effects.Clear();
        }

        public void Tick(float dt)
        {
            if (_resolved || _awaitingPrompt) return;

            _ctx.Crowd.Tick(dt);
            _ctx.Spell.Tick(dt);
            _ctx.Shield.Tick(dt);
            _ctx.Hud.SetCooldowns(_ctx.Spell.CooldownRemaining, _ctx.Shield.CooldownRemaining, _ctx.Shield.IsActive);

            // Sustained crowd damage.
            float dps = BossSim.PlayerDps(_ctx.LastResult, _ctx.Config.Balance.SoftCap);
            ApplyBossDamage(dps * dt);
            if (_resolved) return;

            _ctx.Tutorial.TickBoss(dt, TelegraphActive);

            // Attack cycle with telegraph — the shield-timing game.
            _attackTimer -= dt;
            float telegraph = 1f - Mathf.Clamp01(_attackTimer / _boss.TelegraphSeconds);
            float wind = _attackTimer <= _boss.TelegraphSeconds ? telegraph : 0f;
            _ctx.BossView.SetTelegraph(wind);
            _ctx.CameraRig.SetTelegraph(wind);

            if (_attackTimer <= 0f)
            {
                _attackTimer = _boss.AttackIntervalSeconds;
                _ctx.BossView.SetTelegraph(0f);
                _ctx.CameraRig.SetTelegraph(0f);
                LandBossAttack();
            }
        }

        // Peak channels sit near 1.6, not 2.5+. These reach the GPU through a
        // MaterialPropertyBlock, and whether Unity gamma-expands a Color set that way in a
        // linear project is the one thing here I could not settle from the container. At
        // 1.6 the effect clears the 0.85 bloom threshold comfortably if the value is taken
        // raw, and is hot-but-not-absurd if it is expanded (1.6^2.2 = 2.9). At 2.5 the
        // expanded case would be 8.5 and the screen would white out. A device screenshot
        // decides which, and then these can be tuned in one direction with confidence.
        private static readonly Color SpellTint = new Color(0.40f, 0.70f, 1.60f);
        private static readonly Color BlockTint = new Color(0.95f, 1.35f, 1.85f);
        private static readonly Color StrikeTint = new Color(1.65f, 0.34f, 0.21f);
        private static readonly Color DeathTint = new Color(1.70f, 0.70f, 0.26f);
        private static readonly Color EchoTint = new Color(1.10f, 0.52f, 1.70f);
        private static readonly Color ExecuteTint = new Color(1.80f, 0.24f, 0.30f);

        private void OnFlickUp() => _ctx.Spell.TryCast();
        private void OnFlickDown() => _ctx.Shield.TryRaise();

        private void OnSpellCast()
        {
            float hit = SpellDamage();
            bool echo = Talents.Rolls(_ctx.CurrentStats.Get(StatIds.SpellEcho), Random.value);
            ApplyBossDamage(hit);
            // On the boss an echo IS a literal second cast — there is only one target and
            // hitting it twice is exactly what the talent promises. Guarded, because the
            // first hit may already have finished the fight.
            if (echo && !_resolved) ApplyBossDamage(hit);
            _ctx.BossView.FlashHit();
            _ctx.CameraRig.Apply(CameraFeel.Spell);
            _ctx.CameraRig.PunchFov(2.2f);

            // The spell was a number leaving the health bar. Now a bolt leaves the army and
            // detonates ON the boss, which is the difference between a stat change and a
            // hit — and it lands exactly where the boss is because the position was pinned
            // when the encounter began.
            var origin = new Vector3(_ctx.Crowd.CenterX, 0f, _ctx.Crowd.FrontZ);
            _ctx.Effects.Burst(origin, SpellTint, 8, 3.2f, 0.35f);
            _ctx.Effects.Bolt(origin, _bossPosition, SpellTint, 40f);
            if (echo) _ctx.Effects.Bolt(origin, _bossPosition, EchoTint, 26f);
        }

        /// <summary>One spell's worth of damage. Shared with the shield reflect, so the two
        /// can never drift onto different magnitudes.</summary>
        private float SpellDamage()
        {
            float dps = BossSim.PlayerDps(_ctx.LastResult, _ctx.Config.Balance.SoftCap);
            return dps * _ctx.Config.Spells.BossDamageMultiplier
                   * (1f + _ctx.CurrentStats.Get(StatIds.SpellPower));
        }

        private void ApplyBossDamage(float amount)
        {
            if (_resolved) return;
            _bossHp -= amount;

            // Execute is checked on every tick of damage, not only on the spell, because the
            // crowd's sustained dps is what actually walks a boss down into the threshold —
            // gating it to the spell would make the Headsman keystone fire almost never.
            if (Talents.Executes(_bossHp, _bossHpMax, _ctx.CurrentStats.Get(StatIds.Execute)))
            {
                _bossHp = 0f;
                _ctx.Effects.Shock(_bossPosition + Vector3.up * 1.2f, ExecuteTint, 0.6f, 11f, 0.4f);
            }

            _ctx.Hud.SetBossHp(Mathf.Max(0f, _bossHp) / _bossHpMax);
            if (_bossHp <= 0f) OnBossDefeated();
        }

        private void LandBossAttack()
        {
            long before = _ctx.Run.ForceCount;
            long after = BossSim.ApplyBossHit(before, _boss.HitFraction,
                _ctx.LastResult.HeroStats.Get(BattleRunner.Core.Stats.StatIds.Health),
                _ctx.Shield.IsActive);

            bool blocked = _ctx.Shield.IsActive;

            if (after != before)
            {
                _ctx.Run.ForceCount = after;
                _ctx.LastResult.FinalForceCount = after;
                _ctx.Crowd.SetForce(after);
                _ctx.Hud.SetForce(after);
            }

            _ctx.CameraRig.Apply(CameraFeel.ForBossStrike(before, after, blocked));

            // A landed blow throws debris off the ARMY; a blocked one rings off the shield
            // instead. Two different events that used to look the same except for a number.
            if (blocked)
                _ctx.Effects.Shock(new Vector3(_ctx.Crowd.CenterX, 0f, _ctx.Crowd.CenterZ),
                    BlockTint, 1.6f, 5.5f, 0.45f);
            else
                _ctx.Effects.Burst(new Vector3(_ctx.Crowd.CenterX, 0f, _ctx.Crowd.CenterZ),
                    StrikeTint, 16, 4.6f, 0.6f);
            // A blow the shield actually ate. Without this the player has no way to know
            // their flick did anything — the army simply does not shrink, which is
            // indistinguishable from the boss having missed.
            if (blocked)
            {
                _ctx.Ward.FlashBlock();
                _ctx.Dome.FlashBlock();

                // Martyr and Paladin turn a block into an attack. The bolt travels the other
                // way down the lane, which is the only way the player can tell this happened
                // — the boss bar moving on a frame they were defending is easy to miss.
                float reflect = Talents.ReflectedDamage(SpellDamage(),
                    _ctx.CurrentStats.Get(StatIds.ShieldReflect));
                if (reflect > 0f)
                {
                    _ctx.Effects.Bolt(new Vector3(_ctx.Crowd.CenterX, 0f, _ctx.Crowd.FrontZ),
                        _bossPosition, BlockTint, 34f);
                    ApplyBossDamage(reflect);
                }
            }

            if (after <= 0) OnCrowdWiped();
        }

        private void OnBossDefeated()
        {
            _ctx.CameraRig.Apply(CameraFeel.BossDefeated);
            _ctx.CameraRig.SetTelegraph(0f);
            _ctx.Ward.Clear();
            _ctx.Dome.Clear();

            // The beat the whole level builds to: three rings leaving the body at different
            // speeds so the wave has depth rather than being one expanding circle, plus a
            // full pool of debris. This is the one place worth spending every mote.
            Vector3 foot = _bossPosition;
            _ctx.Effects.Shock(foot, DeathTint, 1.5f, 16f, 0.55f);
            _ctx.Effects.Shock(foot, DeathTint, 0.8f, 9f, 0.45f);
            _ctx.Effects.Shock(foot, new Color(1.75f, 1.25f, 0.66f), 0.5f, 5f, 0.32f);
            _ctx.Effects.Burst(foot + Vector3.up * 1.5f, DeathTint, 40, 7.5f, 0.9f);

            _resolved = true;
            // Income SCALES with depth. At a flat 3 a tree of ~240 point-spends would need
            // eighty boss kills to fill, which is not progression, it is a wall with a long
            // approach. Rising by one every two levels puts the tree at roughly thirty kills
            // and hands the rest to paragon, which never runs out.
            int award = _ctx.Config.Balance.StatPointsPerBossKill + _ctx.Profile.CurrentLevelIndex / 2;
            _ctx.Profile.UnspentStatPoints += award;
            _ctx.Machine.TransitionTo(_ctx.LootState);
        }

        private void OnCrowdWiped()
        {
            // Tick() stops here, so the ward would hang lit behind the resurrect
            // modal. CancelActive drops the window WITHOUT refunding the cooldown —
            // ResetForPhase would hand out a free shield on revive.
            _ctx.Shield.CancelActive();
            _ctx.Ward.Clear();
            _ctx.Dome.Clear();
            _ctx.Effects.Clear();
            _ctx.CameraRig.SetTelegraph(0f);

            _awaitingPrompt = true;
            // Same reason as RunnerLoopState.OnForceDepleted: Tick() stops here, so a live
            // shield prompt would sit frozen behind the resurrect modal. This path was the
            // asymmetric one — the runner phase dropped its prompt and the boss phase did not.
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
                    Defeat();
                });
        }

        private void OnResurrectResult(bool granted)
        {
            if (!ReferenceEquals(_ctx.Machine.Current, this)) return;
            if (granted)
            {
                long revived = System.Math.Max(10L, _ctx.CurrentLevel.ParForceAtFinish / 3);
                _ctx.Run.ForceCount = revived;
                _ctx.LastResult.FinalForceCount = revived;
                _ctx.Crowd.SetForce(revived);
                _ctx.Hud.SetForce(revived);
                _awaitingPrompt = false;
            }
            else
            {
                Defeat();
            }
        }

        private void Defeat()
        {
            _resolved = true;
            _ctx.SaveProfile();
            _ctx.Machine.TransitionTo(_ctx.MenuState);
        }
    }
}
