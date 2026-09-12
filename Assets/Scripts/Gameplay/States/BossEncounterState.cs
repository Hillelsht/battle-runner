using BattleRunner.Core.Feel;
using BattleRunner.Core.Audio;
using BattleRunner.Core.Boss;
using BattleRunner.Core.Flow;
using BattleRunner.Core.Progression;
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
        private BossAffix _affix;
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
            _armyAdvance = 0f;
            _skirmishSeconds = 0f;
            _swingsDone = 0;
            _openingForce = _ctx.Run.ForceCount;
            // BossFor, not CurrentLevel.Boss. Taking the boss from the level tied the two
            // cycles together, so a player who saw level 3 twice fought its boss twice —
            // and with the old clamping LevelFor, round six onward was one boss forever.
            _boss = _ctx.Config.BossFor(_ctx.Profile.CurrentLevelIndex) ?? _ctx.CurrentLevel.Boss;
            _archetype = _boss.Archetype;
            int actIndex = RoundPlan.For(_ctx.Profile.CurrentLevelIndex).ActIndex;
            _affix = BossAffixes.For(actIndex);
            _resolved = false;
            _awaitingPrompt = false;

            // THE ACT INDEX, NOT THE ROUND INDEX. This one argument was the whole difficulty
            // treadmill: a boss is fought once per act, and acts are three to five rounds, so
            // compounding its health on the round counter multiplied it by about 3.0x between
            // consecutive fights against a player who grows 1.2-1.7x — and only 1.06x once the
            // soft cap flattens the army. See BossSim.BossHp.
            BalanceSettings balance = _ctx.Config.Balance;
            _bossHpMax = BossAffixes.BossHp(_boss.BaseHp, _boss.PerLevelGrowth, actIndex,
                BossSim.StatDamageAtAct(actIndex, balance.BaseDamage,
                    balance.DamagePerPoint, balance.StatPointsPerBossKill),
                BossSim.StatDamageAtAct(0, balance.BaseDamage,
                    balance.DamagePerPoint, balance.StatPointsPerBossKill),
                balance.SoftCap, _affix);
            _bossHp = _bossHpMax;
            _attackTimer = _boss.AttackIntervalSeconds;

            _wardMax = BossAffixes.WardPool(_archetype, _affix, _bossHpMax);
            _ward = _wardMax;
            _adds = 0;
            _blowsLeft = 0;
            _blowTimer = 0f;

            // Captured, not recomputed. BossView.Show pins the boss HERE for the whole
            // encounter while the crowd keeps ticking, so deriving the position from
            // Crowd.CenterZ later would walk the effects away from the body they belong to.
            // CLOSED FROM 16 TO 11 METRES. The army presses forward about 4.5 m over a
            // fight, so at 16 the two sides never came within ten metres of each other and
            // there was physically nowhere for a melee to happen: two objects at opposite
            // ends of an empty road, one of them changing colour. At 11 the skirmish line
            // spans a gap the eye reads as contact.
            _bossPosition = new Vector3(0f, 0f, _ctx.Crowd.CenterZ + ArenaMetres);
            _ctx.BossView.Show(_boss, _bossPosition, _affix);
            _ctx.Audio.SetCombat(true);
            _ctx.Hud.ShowBossBar(BossAffixes.Decorate(_affix, _boss.DisplayName),
                fillTint: BossThreatState.AffixTint(_affix));
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
            // UNWIND THE ADVANCE. AdvanceZ moves the crowd's centre permanently, so leaving
            // the encounter without putting the army back on its mark would carry the offset
            // into the next round — and into the one after that, cumulatively.
            if (_armyAdvance != 0f)
            {
                _ctx.Crowd.AdvanceZ(-_armyAdvance);
                _armyAdvance = 0f;
            }
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
            // The skirmish line is drawn from state this object owns, so it has to stop being
            // drawn here — otherwise thirty soldiers keep fighting an empty stretch of road
            // through the loot screen and into the next round.
            _ctx.TrackController?.Squads?.ClearBossSkirmish();
        }

        /// <summary>Where the army currently stands relative to its mark, in metres.</summary>
        private float _armyAdvance;

        /// <summary>
        /// Where the boss stands, in metres ahead of the crowd's mark.
        ///
        /// PAIRED with BossChoreography.MaxPress, which is why that constant is named. The
        /// arena the skirmish line has to span is the difference between the two: at 16 and
        /// 3.2 it was thirteen metres and never closed below ten, so there was physically
        /// nowhere for a melee to happen and the encounter could only ever be two objects at
        /// opposite ends of an empty road. At 11 and 4.5 it closes to about six and a half,
        /// which a line of men can be seen to cross.
        /// </summary>
        private const float ArenaMetres = 11f;

        /// <summary>The army that walked into this fight. See LandOneBlow.</summary>
        private long _openingForce;

        /// <summary>Seconds of skirmish, and how many of its beats have been paid out.</summary>
        private float _skirmishSeconds;
        private int _swingsDone;

        /// <summary>
        /// The army lands a volley on the boss.
        ///
        /// Everything here is presentation — the damage is already applied by the caller. The
        /// strengths are deliberately small: this fires about twice a second for the whole
        /// fight, and anything loud enough to be satisfying once is intolerable forty times.
        /// </summary>
        private void OnArmyVolley()
        {
            // A THIRD of a flash, not a whole one. At full strength these would pin the
            // boss's _EmissionFlat near maximum from the first second to the last, and the
            // SPELL — the one thing that is supposed to read as special — would land on a
            // shell already at full glow and produce no visible change at all.
            _ctx.BossView.FlashHit(0.30f);
            _ctx.Audio.Play(AudioCue.BossHit);
            // At the boss's feet, which is where the men are. Small: a burst big enough to
            // be worth looking at once a fight is a strobe at this rate.
            _ctx.Effects.Burst(_bossPosition + Vector3.up * 0.5f, SummonTint, 5, 2.6f, 0.30f);

            // AND HE SMACKS BACK, ON THE SAME BEAT. The boss's only attack was the
            // telegraphed blow every few seconds; between them it stood and was hit. This is
            // the constant attrition of standing next to something that large — unblockable
            // by design, because a shield that answered it would make the shield's real job
            // (the telegraphed blow) unreadable, and small enough that it can never be the
            // thing that kills you.
            ApplyMaul();
        }

        /// <summary>
        /// The boss swats at the men at its feet: a small, constant force loss with a body
        /// movement and a victim, on the same beat as the army's volley.
        /// </summary>
        private void ApplyMaul()
        {
            long before = _ctx.Run.ForceCount;
            if (before <= 0) return;

            // Per SWING, so the rate is per-second and independent of the beat length. Scaled
            // by Health exactly as a real blow is, so the stat means the same thing here.
            float health = _ctx.LastResult.HeroStats.Get(BattleRunner.Core.Stats.StatIds.Health);
            long bite = BossSim.MaulBite(before, MaulFractionPerSecond * BossMelee.SwingSeconds,
                health);
            if (bite <= 0L) return;

            long after = System.Math.Max(0L, before - bite);
            _ctx.Run.ForceCount = after;
            _ctx.LastResult.FinalForceCount = after;
            _ctx.Crowd.SetForce(after);
            _ctx.Hud.SetForce(after);

            _ctx.BossView.Maul();
            // The men it actually killed go down and stay down. A blow that removes force and
            // nothing else is a number; this is what makes it something that happened.
            _ctx.TrackController?.Squads?.BossKilled(bite >= 3 ? 2 : 1);
            _ctx.Effects.Burst(_bossPosition + Vector3.up * 0.6f, DeathTint, 4, 2.2f, 0.26f);

            if (after <= 0) OnCrowdWiped();
        }

        /// <summary>
        /// How much of the army the boss grinds away per second just by being fought.
        ///
        /// Small ON PURPOSE and it is the number most likely to want a device pass. Over a
        /// twenty-second fight this is about 5% of the army before Health — real enough to
        /// feel, nowhere near enough to decide the fight, which belongs to the telegraphed
        /// blows the player can actually answer.
        /// </summary>
        private const float MaulFractionPerSecond = 0.0026f;

        public void Tick(float dt)
        {
            if (_resolved || _awaitingPrompt) return;

            _ctx.Crowd.Tick(dt);

            // THE ARMY ADVANCES. It used to stand on its mark for the whole fight while the
            // boss stood on its own eleven metres away, which is half of why the encounter
            // read as two objects near each other rather than as a battle. It presses forward
            // as the fight goes on and is driven back by a blow — hard by one it did not
            // block. Applied as an OFFSET from the mark the encounter parked it on, so the
            // arithmetic that placed the boss relative to the crowd stays true.
            float advance = _ctx.BossView.ArmyAdvance;
            _ctx.Crowd.AdvanceZ(advance - _armyAdvance);
            _armyAdvance = advance;

            _ctx.Spell.Tick(dt);
            _ctx.Shield.Tick(dt);
            _ctx.Hud.SetCooldowns(_ctx.Spell.CooldownRemaining, _ctx.Shield.CooldownRemaining, _ctx.Shield.IsActive);

            // THE ARMY'S DAMAGE, ON A BEAT INSTEAD OF PER FRAME.
            //
            // It was `dps * dt` applied sixty times a second, writing nothing but the HUD
            // bar. A thing that happens sixty times a second cannot be seen, heard or felt —
            // the whole of the player's contribution to a boss fight was invisible, which is
            // what "no visual fighting with the boss" describes.
            //
            // Same dps. Same total. Delivered on a beat, with a flash, a sound, a burst and a
            // line of men swinging. BossMelee.SwingsBy is stepped rather than a timer that
            // resets, so the sum over a fight is exactly dps * elapsed to within the beat
            // currently in flight — pinned by a test, because a presentation change that
            // quietly alters the balance is a balance change wearing a disguise.
            float dps = BossSim.PlayerDps(_ctx.LastResult, _ctx.Config.Balance.SoftCap);
            _skirmishSeconds += dt;
            int swings = BossMelee.SwingsBy(_skirmishSeconds);
            if (swings > _swingsDone)
            {
                ApplyBossDamage(BossMelee.DamageForSwings(_swingsDone, swings, dps), 1f);
                _swingsDone = swings;
                if (!_resolved) OnArmyVolley();
            }
            if (_resolved) return;

            // The skirmish line itself. Told, not asked: this state owns the clock, so a
            // fight that ends mid-frame cannot leave a soldier standing in an empty arena.
            _ctx.TrackController?.Squads?.SetBossSkirmish(
                _bossPosition, _skirmishSeconds, _ctx.Run.ForceCount);

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
            float wasTimer = _attackTimer;
            _attackTimer -= dt;
            float telegraph = 1f - Mathf.Clamp01(_attackTimer / _boss.TelegraphSeconds);
            // ONCE per wind-up, on the frame the telegraph opens. The obvious place is the
            // per-frame telegraph update, which would retrigger it sixty times a second and
            // turn the one warning the player has into a drone.
            if (wasTimer > _boss.TelegraphSeconds && _attackTimer <= _boss.TelegraphSeconds)
                _ctx.Audio.Play(AudioCue.BossTelegraph);
            float wind = _attackTimer <= _boss.TelegraphSeconds ? telegraph : 0f;
            _ctx.BossView.SetTelegraph(wind);
            _ctx.CameraRig.SetTelegraph(wind);

            if (_attackTimer <= 0f)
            {
                // Enrage compresses its own cycle as its health falls, so the interval is
                // read fresh every time rather than taken from the definition.
                _attackTimer = BossAffixes.NextInterval(_archetype, _affix,
                    _boss.AttackIntervalSeconds, _bossHpMax > 0f ? _bossHp / _bossHpMax : 0f);
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
        private static readonly Color VampireTint = new Color(1.60f, 0.14f, 0.48f);

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
            _ctx.Audio.Play(AudioCue.SpellHit);
            // FULL strength. The melee volleys ask for 0.30 precisely so this one still
            // reads as a different event when it arrives forty blows into a fight.
            _ctx.BossView.FlashHit(1f);
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
            int summons = BossAffixes.AddsPerCycle(_archetype, _affix);

            // Anything called LAST cycle and not answered bites now, whatever called it. A
            // summoner that only ever summoned would be a boss you could ignore.
            if (_adds > 0)
            {
                BiteFromAdds();
                if (_resolved || _awaitingPrompt) return;
            }

            if (summons > 0)
            {
                _adds += summons;
                _ctx.Effects.Shock(_bossPosition, SummonTint, 0.8f, 8f, 0.5f);
                for (int i = 0; i < summons; i++)
                    _ctx.Effects.Bolt(_bossPosition + Vector3.up * 1.4f,
                        new Vector3(_ctx.Crowd.CenterX + (i - 0.5f) * 2.2f, 0f,
                            _ctx.Crowd.FrontZ + 4f), SummonTint, 16f);
            }

            // A true SUMMONER spends its cycle calling rather than swinging. Everything else
            // swings as well — the Haunted affix hangs adds on a boss, it does not excuse the
            // boss from attacking, and gating the swing on "did anything get summoned" would
            // have turned every Haunted Colossus into a creature that never lands a blow.
            if (_archetype == BossArchetype.Summoner) return;

            // A volley queues its follow-ups on the blow clock.
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
            // The army that WALKED IN, captured on Enter. A blow is measured partly against
            // it rather than wholly against what is left, or the sequence of blows approaches
            // zero without ever reaching it — twenty-four unblocked blows to a wipe, in a
            // fight that lasts fifteen seconds. See BossSim.ApplyBossHit.
            long after = BossSim.ApplyBossHit(before,
                BossAffixes.BlowFraction(_archetype, _affix, _boss.HitFraction),
                _ctx.LastResult.HeroStats.Get(BattleRunner.Core.Stats.StatIds.Health),
                _ctx.Shield.IsActive, _openingForce);

            bool blocked = _ctx.Shield.IsActive;

            if (after != before)
            {
                _ctx.Run.ForceCount = after;
                _ctx.LastResult.FinalForceCount = after;
                _ctx.Crowd.SetForce(after);
                _ctx.Hud.SetForce(after);
            }

            _ctx.CameraRig.Apply(CameraFeel.ForBossStrike(before, after, blocked));
            // The boss SWINGS. Its attack used to produce no geometry at its own end of the
            // arena at all — every effect was centred on the crowd, so the blow appeared to
            // come from nowhere.
            _ctx.BossView.Strike(blocked);

            // A Vampiric boss feeds on what it takes, and blocking is what starves it — which
            // is the whole reason the affix exists rather than being another damage number.
            float heal = BossAffixes.HealOnHit(_affix, _bossHpMax, blocked);
            if (heal > 0f && !_resolved)
            {
                _bossHp = Mathf.Min(_bossHpMax, _bossHp + heal);
                _ctx.Hud.SetBossHp(_bossHp / _bossHpMax);
                _ctx.Effects.Bolt(new Vector3(_ctx.Crowd.CenterX, 0.8f, _ctx.Crowd.CenterZ),
                    _bossPosition + Vector3.up * 1.2f, VampireTint, 28f);
            }

            // A landed blow throws debris off the ARMY; a blocked one rings off the shield
            // instead. Two different events that used to look the same except for a number.
            _ctx.Audio.Play(blocked ? AudioCue.ShieldBlock : AudioCue.BossBlow);
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
            // BEFORE Exit, not in it. Tick returns early once the fight is resolved or a
            // prompt is up, so the skirmish would simply stop being updated and thirty
            // soldiers would stand frozen mid-swing at a collapsing boss for the whole of the
            // victory beat and the loot screen behind it.
            _ctx.TrackController?.Squads?.ClearBossSkirmish();

            // The beat the whole level builds to: three rings leaving the body at different
            // speeds so the wave has depth rather than being one expanding circle, plus a
            // full pool of debris. This is the one place worth spending every mote.
            Vector3 foot = _bossPosition;
            _ctx.Audio.Play(AudioCue.BossDeath);
            // Falls, rather than ceasing to be drawn.
            _ctx.BossView.Collapse();
            _ctx.Audio.SetCombat(false);
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
            // Same reason as OnBossDefeated: Tick stops here, so the line has to be told to
            // stop rather than left to freeze behind the resurrect modal.
            _ctx.TrackController?.Squads?.ClearBossSkirmish();
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
