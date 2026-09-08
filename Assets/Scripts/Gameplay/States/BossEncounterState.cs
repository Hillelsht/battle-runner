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

        // Archetype state. All of it is inert for a Slam boss, which is the fight the game
        // already had and which must stay exactly as it was.
        private BossArchetype _archetype;
        private float _ward;
        private float _wardMax;
        private int _adds;
        private int _blowsLeft;
        private float _blowTimer;

        public BossEncounterState(GameContext ctx) => _ctx = ctx;

        /// <summary>True while the boss is winding up — the window a shield must land in.</summary>
        public bool TelegraphActive => _boss != null && _attackTimer <= _boss.TelegraphSeconds;

        public void Enter()
        {
            // BossFor, not CurrentLevel.Boss. Taking the boss from the level tied the two
            // cycles together, so a player who saw level 3 twice fought its boss twice —
            // and with the old clamping LevelFor, round six onward was one boss forever.
            _boss = _ctx.Config.BossFor(_ctx.Profile.CurrentLevelIndex) ?? _ctx.CurrentLevel.Boss;
            _archetype = _boss.Archetype;
            _resolved = false;
            _awaitingPrompt = false;

            _bossHpMax = BossSim.BossHp(_boss.BaseHp, _boss.PerLevelGrowth, _ctx.Profile.CurrentLevelIndex);
            _bossHp = _bossHpMax;
            _attackTimer = _boss.AttackIntervalSeconds;

            _wardMax = BossSim.WardPool(_archetype, _bossHpMax);
            _ward = _wardMax;
            _adds = 0;
            _blowsLeft = 0;
            _blowTimer = 0f;

            // Captured, not recomputed. BossView.Show pins the boss HERE for the whole
            // encounter while the crowd keeps ticking, so deriving the position from
            // Crowd.CenterZ later would walk the effects away from the body they belong to.
            _bossPosition = new Vector3(0f, 0f, _ctx.Crowd.CenterZ + 16f);
            _ctx.BossView.Show(_boss, _bossPosition);
            _ctx.Hud.ShowBossBar(_boss.DisplayName);
            _ctx.Hud.SetBossHp(1f);
            _ctx.BossView.SetWard(_wardMax > 0f ? 1f : 0f);

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

            // Sustained crowd damage. Ward multiplier 1 — the grind wears a ward down,
            // it just does not break one, which is what makes the spell the answer.
            float dps = BossSim.PlayerDps(_ctx.LastResult, _ctx.Config.Balance.SoftCap);
            ApplyBossDamage(dps * dt, 1f);
            if (_resolved) return;

            ApplyDrain(dt);
            if (_resolved || _awaitingPrompt) return;

            _ctx.Tutorial.TickBoss(dt, TelegraphActive);

            // A volley's follow-up blows land on their own clock, between telegraphs. One
            // shield raised on the wind-up covers all three, which is the whole trade.
            if (_blowsLeft > 0)
            {
                _blowTimer -= dt;
                if (_blowTimer <= 0f)
                {
                    _blowsLeft--;
                    _blowTimer = BossSim.VolleyGapSeconds(_boss.TelegraphSeconds);
                    LandOneBlow();
                    if (_resolved || _awaitingPrompt) return;
                }
            }

            // Attack cycle with telegraph — the shield-timing game.
            _attackTimer -= dt;
            float telegraph = 1f - Mathf.Clamp01(_attackTimer / _boss.TelegraphSeconds);
            float wind = _attackTimer <= _boss.TelegraphSeconds ? telegraph : 0f;
            _ctx.BossView.SetTelegraph(wind);
            _ctx.CameraRig.SetTelegraph(wind);

            if (_attackTimer <= 0f)
            {
                // Enrage compresses its own cycle as its health falls, so the interval is
                // read fresh every time rather than taken from the definition.
                _attackTimer = BossSim.NextInterval(_archetype, _boss.AttackIntervalSeconds,
                    _bossHpMax > 0f ? _bossHp / _bossHpMax : 0f);
                _ctx.BossView.SetTelegraph(0f);
                _ctx.CameraRig.SetTelegraph(0f);
                LandBossAttack();
            }
        }

        /// <summary>
        /// The Hollow Leech's tick. A shield stops it outright, which is the only reason
        /// the shield is worth holding in that fight — there is nothing to time.
        /// </summary>
        private void ApplyDrain(float dt)
        {
            long before = _ctx.Run.ForceCount;
            long lost = BossSim.DrainTick(_archetype, before, _boss.HitFraction, dt, _ctx.Shield.IsActive);
            if (lost <= 0L) return;

            long after = System.Math.Max(0L, before - lost);
            _ctx.Run.ForceCount = after;
            _ctx.LastResult.FinalForceCount = after;
            _ctx.Crowd.SetForce(after);
            _ctx.Hud.SetForce(after);

            // Deliberately quiet per tick: this fires every frame, and a shockwave at 60 Hz
            // is a strobe. The bleed is legible from the counter and from the crowd itself
            // shrinking; the drama belongs to the blows.
            _drainMotes -= dt;
            if (_drainMotes <= 0f)
            {
                _drainMotes = 0.22f;
                _ctx.Effects.Burst(new Vector3(_ctx.Crowd.CenterX, 0.4f, _ctx.Crowd.CenterZ),
                    DrainTint, 3, 2.6f, 0.5f);
            }

            if (after <= 0) OnCrowdWiped();
        }

        private float _drainMotes;

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
        private static readonly Color DrainTint = new Color(0.42f, 1.55f, 0.60f);
        private static readonly Color SummonTint = new Color(1.15f, 0.55f, 1.65f);
        private static readonly Color WardTint = new Color(0.80f, 0.92f, 1.70f);

        private void OnFlickUp() => _ctx.Spell.TryCast();
        private void OnFlickDown() => _ctx.Shield.TryRaise();

        private void OnSpellCast()
        {
            float hit = SpellDamage();
            bool echo = Talents.Rolls(_ctx.CurrentStats.Get(StatIds.SpellEcho), Random.value);

            // The spell is the ONLY answer to a Pale Shepherd's adds. Not the shield: if
            // one flick handled every archetype there would be no reason to have six.
            if (_adds > 0)
            {
                _ctx.Effects.Burst(_bossPosition + Vector3.up * 1.6f, SummonTint, 18, 5.5f, 0.6f);
                _adds = 0;
            }

            ApplyBossDamage(hit, SpellWardMultiplier);
            // On the boss an echo IS a literal second cast — there is only one target and
            // hitting it twice is exactly what the talent promises. Guarded, because the
            // first hit may already have finished the fight.
            if (echo && !_resolved) ApplyBossDamage(hit, SpellWardMultiplier);
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

        /// <summary>
        /// How much faster a spell strips a ward than the crowd's grind does. Three, so
        /// "break the ward" is an action the player takes rather than something that
        /// merely happens to them while they wait.
        /// </summary>
        private const float SpellWardMultiplier = 3f;

        /// <summary>One spell's worth of damage. Shared with the shield reflect, so the two
        /// can never drift onto different magnitudes.</summary>
        private float SpellDamage()
        {
            float dps = BossSim.PlayerDps(_ctx.LastResult, _ctx.Config.Balance.SoftCap);
            return dps * _ctx.Config.Spells.BossDamageMultiplier
                   * (1f + _ctx.CurrentStats.Get(StatIds.SpellPower));
        }

        private void ApplyBossDamage(float amount, float wardMultiplier)
        {
            if (_resolved) return;

            if (_ward > 0f)
            {
                float toHealth = BossSim.ThroughWard(amount, _ward, wardMultiplier, out float left);
                bool broke = left <= 0f;
                _ward = left;
                _ctx.BossView.SetWard(_wardMax > 0f ? _ward / _wardMax : 0f);
                // Only a SPELL flashes the shell. The crowd's dps arrives every frame, and
                // a shell that strobes at 60 Hz says nothing about what the player just did.
                if (wardMultiplier > 1f) _ctx.BossView.FlashWard();

                if (broke)
                {
                    _ctx.BossView.SetWard(0f);
                    _ctx.Effects.Shock(_bossPosition, WardTint, 1.2f, 13f, 0.5f);
                    _ctx.Effects.Burst(_bossPosition + Vector3.up * 1.8f, WardTint, 26, 6.5f, 0.75f);
                    _ctx.CameraRig.PunchFov(3.0f);
                }

                amount = toHealth;
                if (amount <= 0f) return;
            }

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

        /// <summary>
        /// One attack cycle, fanned out by archetype. Slam falls through to a single blow,
        /// unchanged from the fight the game already had.
        /// </summary>
        private void LandBossAttack()
        {
            int summons = BossSim.AddsPerCycle(_archetype);
            if (summons > 0)
            {
                // Anything called LAST cycle and not answered bites now, and then it calls
                // more. A summoner that only ever summoned would be a boss you could ignore.
                BiteFromAdds();
                if (_resolved || _awaitingPrompt) return;

                _adds += summons;
                _ctx.Effects.Shock(_bossPosition, SummonTint, 0.8f, 8f, 0.5f);
                for (int i = 0; i < summons; i++)
                    _ctx.Effects.Bolt(_bossPosition + Vector3.up * 1.4f,
                        new Vector3(_ctx.Crowd.CenterX + (i - 0.5f) * 2.2f, 0f,
                            _ctx.Crowd.FrontZ + 4f), SummonTint, 16f);
                return;
            }

            // Everything else swings. A volley queues its follow-ups on the blow clock.
            _blowsLeft = BossSim.BlowsPerCycle(_archetype) - 1;
            _blowTimer = BossSim.VolleyGapSeconds(_boss.TelegraphSeconds);
            LandOneBlow();
        }

        /// <summary>
        /// What the adds took while the player was doing something else. The SHIELD does
        /// not stop this on purpose — the spell is the answer to a summoner, and a shield
        /// that covered every archetype would make the other five pointless.
        /// </summary>
        private void BiteFromAdds()
        {
            if (_adds <= 0) return;

            long before = _ctx.Run.ForceCount;
            long bite = BossSim.AddBite(_adds, before);
            if (bite <= 0L) return;

            long after = System.Math.Max(0L, before - bite);
            _ctx.Run.ForceCount = after;
            _ctx.LastResult.FinalForceCount = after;
            _ctx.Crowd.SetForce(after);
            _ctx.Hud.SetForce(after);

            _ctx.CameraRig.Apply(CameraFeel.ForLoss(before, after));
            _ctx.Effects.Burst(new Vector3(_ctx.Crowd.CenterX, 0f, _ctx.Crowd.CenterZ),
                SummonTint, 14, 4.2f, 0.55f);

            if (after <= 0) OnCrowdWiped();
        }

        private void LandOneBlow()
        {
            long before = _ctx.Run.ForceCount;
            long after = BossSim.ApplyBossHit(before, BossSim.BlowFraction(_archetype, _boss.HitFraction),
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
                    ApplyBossDamage(reflect, 1f);
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
            // The stat-point award moved to LootPhaseState, which now runs after BOTH endings
            // and pays every round from RoundRewards. Awarding here as well would pay a boss
            // round twice — and the boss round's share of the curve is already the largest in
            // the act by a wide margin.
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
                long revived = System.Math.Max(10L, _ctx.CurrentPar / 3);
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
