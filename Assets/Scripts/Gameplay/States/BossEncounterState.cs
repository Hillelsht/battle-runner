using BattleRunner.Core.Boss;
using BattleRunner.Core.Flow;
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
        private float _attackTimer;
        private bool _resolved;
        private bool _awaitingPrompt;

        public BossEncounterState(GameContext ctx) => _ctx = ctx;

        public void Enter()
        {
            _boss = _ctx.CurrentLevel.Boss;
            _resolved = false;
            _awaitingPrompt = false;

            _bossHpMax = BossSim.BossHp(_boss.BaseHp, _boss.PerLevelGrowth, _ctx.Profile.CurrentLevelIndex);
            _bossHp = _bossHpMax;
            _attackTimer = _boss.AttackIntervalSeconds;

            _ctx.BossView.Show(_boss, new Vector3(0f, 0f, _ctx.Crowd.CenterZ + 16f));
            _ctx.Hud.ShowBossBar(_boss.DisplayName);
            _ctx.Hud.SetBossHp(1f);

            _ctx.Spell.ResetForPhase();
            _ctx.Shield.ResetForPhase();

            _ctx.LaneTargetChannel.Subscribe(_ctx.Crowd.OnLaneTarget);
            _ctx.FlickUpChannel.Subscribe(OnFlickUp);
            _ctx.FlickDownChannel.Subscribe(OnFlickDown);
            _ctx.Spell.Cast += OnSpellCast;
        }

        public void Exit()
        {
            _ctx.LaneTargetChannel.Unsubscribe(_ctx.Crowd.OnLaneTarget);
            _ctx.FlickUpChannel.Unsubscribe(OnFlickUp);
            _ctx.FlickDownChannel.Unsubscribe(OnFlickDown);
            _ctx.Spell.Cast -= OnSpellCast;

            _ctx.BossView.Hide();
            _ctx.Hud.HideBossBar();
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

            // Attack cycle with telegraph — the shield-timing game.
            _attackTimer -= dt;
            float telegraph = 1f - Mathf.Clamp01(_attackTimer / _boss.TelegraphSeconds);
            _ctx.BossView.SetTelegraph(_attackTimer <= _boss.TelegraphSeconds ? telegraph : 0f);

            if (_attackTimer <= 0f)
            {
                _attackTimer = _boss.AttackIntervalSeconds;
                _ctx.BossView.SetTelegraph(0f);
                LandBossAttack();
            }
        }

        private void OnFlickUp() => _ctx.Spell.TryCast();
        private void OnFlickDown() => _ctx.Shield.TryRaise();

        private void OnSpellCast()
        {
            float dps = BossSim.PlayerDps(_ctx.LastResult, _ctx.Config.Balance.SoftCap);
            ApplyBossDamage(dps * _ctx.Config.Spells.BossDamageMultiplier);
            _ctx.BossView.FlashHit();
        }

        private void ApplyBossDamage(float amount)
        {
            _bossHp -= amount;
            _ctx.Hud.SetBossHp(_bossHp / _bossHpMax);
            if (_bossHp <= 0f) OnBossDefeated();
        }

        private void LandBossAttack()
        {
            long before = _ctx.Run.ForceCount;
            long after = BossSim.ApplyBossHit(before, _boss.HitFraction,
                _ctx.LastResult.HeroStats.Get(BattleRunner.Core.Stats.StatIds.Health),
                _ctx.Shield.IsActive);

            if (after != before)
            {
                _ctx.Run.ForceCount = after;
                _ctx.LastResult.FinalForceCount = after;
                _ctx.Crowd.SetForce(after);
                _ctx.Hud.SetForce(after);
            }

            if (after <= 0) OnCrowdWiped();
        }

        private void OnBossDefeated()
        {
            _resolved = true;
            _ctx.Profile.UnspentStatPoints += _ctx.Config.Balance.StatPointsPerBossKill;
            _ctx.Machine.TransitionTo(_ctx.LootState);
        }

        private void OnCrowdWiped()
        {
            _awaitingPrompt = true;
            _ctx.Resurrect.Show(
                _ctx.Ads.IsRewardedReady(AdPlacement.Resurrect),
                onResurrect: () => _ctx.Ads.ShowRewarded(AdPlacement.Resurrect, granted =>
                {
                    _ctx.Resurrect.Hide();
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
                }),
                onGiveUp: () =>
                {
                    _ctx.Resurrect.Hide();
                    Defeat();
                });
        }

        private void Defeat()
        {
            _resolved = true;
            _ctx.SaveProfile();
            _ctx.Machine.TransitionTo(_ctx.MenuState);
        }
    }
}
