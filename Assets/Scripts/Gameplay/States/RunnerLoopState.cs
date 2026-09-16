using BattleRunner.Core.Audio;
using BattleRunner.Core.Flow;
using BattleRunner.Core.Feel;
using BattleRunner.Core.Heroes;
using BattleRunner.Core.Progression;
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

        // Who is leading, latched at Enter. Read once per round rather than per event: the
        // hero cannot change during a run, and a lookup inside OnGateApplied would be a
        // profile read on the hottest path in the game.
        private HeroClass _hero = HeroRoster.Default;

        // REVENANT. What a loss takes, a share of it walks back a moment later. One debt and
        // one timer rather than a queue: two ambushes half a second apart should return as
        // one wave, and a list of pending refunds would allocate inside the run loop to make
        // a difference nobody can see.
        private double _returnPending;
        private float _returnIn;

        public RunnerLoopState(GameContext ctx) => _ctx = ctx;

        public void Enter()
        {
            _awaitingPrompt = false;
            _finished = false;
            _hero = HeroRoster.FromSaved(_ctx.Profile.HeroId);
            _returnPending = 0.0;
            _returnIn = 0f;

            _ctx.Hud.Show();
            _ctx.Hud.SetForce(_ctx.Run.ForceCount);
            _ctx.Hud.HideBossBar();

            // "3-2  THE BONE WASTES", or on the last round of an act the name of whatever is
            // waiting at the end of it. The build-up only works if the player knows how many
            // rounds are left before the fight.
            RoundPlan plan = RoundPlan.For(_ctx.Profile.CurrentLevelIndex);
            string where = plan.IsBossRound
                ? (_ctx.Config.BossFor(_ctx.Profile.CurrentLevelIndex)?.DisplayName ?? "SOMETHING") + " AWAITS"
                : BattleRunner.Core.World.WorldThemes.For(plan).DisplayName;
            _ctx.Hud.SetRound($"{plan.ActIndex + 1}-{plan.RoundInAct + 1}   {where.ToUpperInvariant()}");

            _ctx.LaneTargetChannel.Subscribe(_ctx.Crowd.OnLaneTarget);
            _ctx.FlickUpChannel.Subscribe(OnFlickUp);
            _ctx.FlickDownChannel.Subscribe(OnFlickDown);
            _ctx.Spell.Cast += OnSpellCast;

            _ctx.TrackController.GateApplied += OnGateApplied;
            _ctx.TrackController.EnemyContact += OnEnemyContact;
            _ctx.TrackController.EliteSwing += OnEliteSwing;
            _ctx.TrackController.EliteDefeated += OnEliteDefeated;
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
            _ctx.TrackController.EliteSwing -= OnEliteSwing;
            _ctx.TrackController.EliteDefeated -= OnEliteDefeated;
            _ctx.TrackController.FinishReached -= OnFinishReached;

            _ctx.Tutorial.Unsubscribe();
            _ctx.Tutorial.EndPhase();
            _ctx.Dome.Clear();
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
            _ctx.TrackController.Tick(_ctx.Crowd, _ctx.CurrentStats.Get(StatIds.Magnetism));

            if (_returnPending > 0.0)
            {
                _returnIn -= dt;
                if (_returnIn <= 0f) PayReturns();
            }

            _ctx.Spell.Tick(dt);
            _ctx.Shield.Tick(dt);
            _ctx.Hud.SetAbilities(_ctx.Spell.Fill, _ctx.Spell.Charges, _ctx.Spell.Capacity,
                _ctx.Shield.Fill, _ctx.Shield.Charges, _ctx.Shield.Capacity, _ctx.Shield.IsActive);
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
        private static readonly Color EchoTint = new Color(1.10f, 0.52f, 1.70f);
        private static readonly Color CritTint = new Color(1.75f, 1.30f, 0.42f);
        private static readonly Color ShatterTint = new Color(0.62f, 1.65f, 1.20f);

        /// <summary>The Revenant's violet — the same hue its army is painted.</summary>
        private static readonly Color ReturnTint = new Color(0.95f, 0.70f, 1.55f);

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
            // FROM THE FRONT OF THE ARMY, not its centre. The crowd's leading plane stands
            // up to CrowdMath.FrontDepthMax (7 m) ahead of the centroid, so sweeping from
            // the centroid spent seven of the spell's fifteen metres on road the army was
            // already standing on. What reached ahead was eight metres — under a second at
            // the run speed — which is why the report was that the spell does nothing.
            float from = _ctx.Crowd.FrontZ;
            // ASHCALLER: the sweep is longer. Applied to the range ITSELF, which is what
            // both the clear and the bolt are computed from, so the effect cannot end up
            // detonating somewhere other than where the road was actually cleared.
            float range = _ctx.Config.Spells.ClearRangeMeters * HeroRoster.SpellReach(_hero);

            _spellHits.Clear();
            _ctx.TrackController.CollectAmbushesAhead(from, range, _spellHits);
            int cleared = _ctx.TrackController.ClearAmbushesAhead(from, range);

            // An echo on the road clears a SECOND, longer sweep rather than firing the same
            // one twice: everything inside the first range is already gone, so a literal
            // second cast would do nothing at all and the talent would read as broken.
            bool echo = Talents.Rolls(_ctx.CurrentStats.Get(StatIds.SpellEcho), Random.value);
            if (echo)
            {
                _ctx.TrackController.CollectAmbushesAhead(from, range * 1.9f, _spellHits);
                cleared += _ctx.TrackController.ClearAmbushesAhead(from, range * 1.9f);
            }

            // A BOLT, not an instant ring. The spell used to be a cause with no middle: you
            // flicked, and packs stopped existing. Now something leaves the hero, travels,
            // and detonates at the spell's ACTUAL clear range — so the player learns how far
            // the flick reaches by watching it rather than by dying to a pack one metre
            // outside it. VfxSystem fires the wall and the embers on arrival, so they cannot
            // drift away from where the bolt actually landed.
            var origin = new Vector3(_ctx.Crowd.CenterX, 0f, _ctx.Crowd.FrontZ);
            _ctx.Audio.Play(AudioCue.SpellCast);
            _ctx.Effects.Burst(origin, SpellTint, 8, 3.2f, 0.35f);
            _ctx.Effects.Bolt(origin, new Vector3(_ctx.Crowd.CenterX, 0f, from + range),
                SpellTint, 45f);
            if (echo)
                _ctx.Effects.Bolt(origin,
                    new Vector3(_ctx.Crowd.CenterX, 0f, from + range * 1.9f),
                    EchoTint, 45f);

            // EVERY THING DESTROYED GETS ITS OWN DETONATION. A single bolt landing in empty
            // road while three red crowds silently stopped existing is exactly as unreadable
            // as the old ring was, and it is the other half of "the spell doesn't destroy
            // enemy packs" — an effect the player cannot see did not happen, as far as they
            // are concerned.
            for (int i = 0; i < _spellHits.Count; i++)
            {
                _ctx.Effects.Shock(_spellHits[i], SpellTint, 0.35f, 5.5f, 0.45f);
                _ctx.Effects.Burst(_spellHits[i], SpellTint, 14, 5f, 0.55f);
            }
            if (cleared > 0) _ctx.Audio.Play(AudioCue.SpellHit, 0.85f);
            _spellHits.Clear();
        }

        /// <summary>Reused so a cast never allocates; cleared on both sides of every use.</summary>
        private readonly System.Collections.Generic.List<Vector3> _spellHits =
            new System.Collections.Generic.List<Vector3>(8);

        /// <summary>
        /// A gate landed. THE NUMBER ON IT IS THE NUMBER APPLIED.
        ///
        /// This used to recompute the effect through `Talents.ApplyGate(live force, …)` while
        /// the sign had been written against whatever the army was when it was last refreshed.
        /// The two agreed closely enough to hide, because the sign was rewritten on every 2%
        /// move — and that rewriting is exactly what the player complained about. Latching the
        /// sign alone would have made them disagree openly, which is how last round's "+1 that
        /// adds nothing" was built. So the latch carries the delta: `reveal.Gain` and
        /// `reveal.Loss` in Core are the only arithmetic here now.
        ///
        /// A RALLY IS THE EXCEPTION AND DOES NOT NEED THE LATCH. Its sign is "x2.00" — a
        /// factor, which cannot pop however the army moves — so it keeps applying a share of
        /// the live army, which is precisely what that sign promises.
        /// </summary>
        private void OnGateApplied(GateOp op, int weight, int depth, Reveal reveal, Vector3 where)
        {
            RunState run = _ctx.Run;
            double before = run.ForceCount;

            // THE SHIELD ANSWERS RED GATES NOW, and it did not before. It stopped an enemy
            // PACK and nothing else, which left the most common red thing on the road —
            // a subtract gate, drawn since v0.20 as a crowd of men — walking straight
            // through a raised shield. From a player's seat those two are the same object
            // with the same colour doing the same thing, so a shield that stops one and not
            // the other does not read as a rule; it reads as a shield that does not work.
            if (op == GateOp.Subtract && _ctx.Shield.IsActive)
            {
                _ctx.Audio.Play(AudioCue.ShieldBlock, 0.85f);
                _ctx.Effects.Shock(where, ShatterTint, 0.5f, 6.5f, 0.5f);
                _ctx.Effects.Burst(where, ShatterTint, 20, 5.8f, 0.6f);
                run.GatesHit++;
                // What the block SAVED, priced through the same function that would have
                // taken it, so the Warden's conversion can never disagree with the loss it
                // is converting.
                // What the block SAVED is what the gate was about to take, which is the
                // number on its sign — so the Warden's conversion cannot disagree with the
                // loss it converts even now that the loss is latched.
                ConvertBlock(reveal.Loss(before, 0f, false), where);
                return;
            }

            // Chain counts the multiplies ALREADY landed, so it is read before this gate is
            // folded in — otherwise the first multiply of a run would pay its own bonus.
            float chain = Talents.ChainYield(run.MultiplyChain,
                _ctx.CurrentStats.Get(StatIds.ChainMultiply));
            bool crit = Talents.Rolls(_ctx.CurrentStats.Get(StatIds.GateCrit), Random.value);

            // HOUNDMASTER: recruit gates pay more, and only recruit gates. Folded into the
            // yield the gate was already going to use, so it composes with talents and crits
            // by the rules that already exist instead of being a second addition bolted on
            // after them.
            float gateYield = _ctx.CurrentStats.Get(StatIds.GateYield);
            if (op == GateOp.Add) gateYield += (float)HeroRoster.RecruitBonus(_hero);

            if (op == GateOp.Multiply)
            {
                run.SetForce(Talents.ApplyGate(run.ForceCount, op, weight, depth,
                    gateYield, chain, crit));
            }
            else
            {
                // The latched men, then the same yield rule a factor-based gate got: yield
                // scales the GAIN, never the loss, so a recruit worth 30 men at 20% yield
                // gives 36 and an ambush is untouched by it.
                float yield = Mathf.Max(0f, gateYield) + Mathf.Max(0f, chain);
                if (crit) yield = yield * 2f + 1f;
                double moved = op == GateOp.Add
                    ? reveal.Gain(yield)
                    : -reveal.Loss(run.ForceCount, 0f, false);
                run.SetForce(System.Math.Max(0.0, run.ForceCount + moved));
            }
            run.MultiplyChain = op == GateOp.Multiply ? run.MultiplyChain + 1 : 0;
            run.GatesHit++;
            _ctx.Crowd.SetForce(run.ForceCount);
            _ctx.Hud.SetForce(run.ForceCount);

            // Scaled by the RATIO, so a gate lands the same at 10 men and at 10 billion.
            _ctx.CameraRig.Apply(CameraFeel.ForGate(op, before, run.ForceCount));
            if (run.ForceCount > before)
                _ctx.CameraRig.PunchFov(1.4f + 2.6f * CameraFeel.ForGate(op, before, run.ForceCount).Trauma);

            float weightFelt = CameraFeel.ForGate(op, before, run.ForceCount).Trauma;
            Color tint = crit ? CritTint : GateTint(op);
            _ctx.Audio.Play(
                op == GateOp.Multiply ? AudioCue.GateMultiply
                    : op == GateOp.Subtract ? AudioCue.GateSubtract : AudioCue.GateAdd,
                0.75f + 0.5f * weightFelt);
            _ctx.Effects.Shock(where, tint, 0.8f, 2.6f + 4.4f * weightFelt, 0.45f + 0.20f * weightFelt);
            if (run.ForceCount > before)
                _ctx.Effects.Burst(where, tint, 4 + Mathf.RoundToInt(10f * weightFelt), 3.4f, 0.45f);

            // A crit that looks like an ordinary gate is a stat the player never learns they
            // have. Second ring, hotter tint, extra kick — the same beat, louder.
            if (crit && run.ForceCount > before)
            {
                _ctx.Effects.Shock(where, CritTint, 0.4f, 7.5f, 0.55f);
                _ctx.Effects.Burst(where, CritTint, 18, 5.5f, 0.7f);
                _ctx.CameraRig.PunchFov(3.4f);
            }

            // Any gate that COST men, not only a subtract: a multiply under one loses an
            // army the same way, and a rule about losses that ignored half of them would
            // read as the rule misfiring.
            if (run.ForceCount < before) Owe(before - run.ForceCount);

            if (run.ForceCount <= 0) OnForceDepleted();
        }

        private void OnEnemyContact(int weight, int depth, Reveal reveal, Vector3 where)
        {
            if (_ctx.Shield.IsActive)
            {
                // Blocking used to be SILENT. The pack simply cost nothing and the player was
                // given no evidence their shield had done anything — which, with the old
                // absolute pack cost of a few dozen men against an army of hundreds, was a
                // difference too small to notice even when it was not blocked. Both halves of
                // "the shield doesn't block against them" were true at once.
                _ctx.Audio.Play(AudioCue.ShieldBlock, 0.85f);
                _ctx.Effects.Shock(where, ShatterTint, 0.5f, 6.5f, 0.5f);
                _ctx.Effects.Burst(where, ShatterTint, 20, 5.8f, 0.6f);
                ConvertBlock(reveal.Loss(_ctx.Run.ForceCount,
                    _ctx.CurrentStats.Get(StatIds.EnemyResist), false), where);
                return;
            }

            RunState run = _ctx.Run;
            // Resist shrugs off part of the bite; a shattered pack costs nothing at all.
            bool shattered = Talents.Rolls(_ctx.CurrentStats.Get(StatIds.PackShatter), Random.value);
            // The men drawn in the road, taken from the army. Not a fresh share of it — the
            // squad standing there committed to a count when it came into view, and taking a
            // different number than the one the player counted is the bug, not the feature.
            double bite = reveal.Loss(run.ForceCount,
                _ctx.CurrentStats.Get(StatIds.EnemyResist), shattered);
            double beforeBite = run.ForceCount;
            run.SetForce(System.Math.Max(0.0, run.ForceCount - bite));
            _ctx.Crowd.SetForce(run.ForceCount);
            _ctx.Hud.SetForce(run.ForceCount);

            _ctx.CameraRig.Apply(CameraFeel.ForLoss(beforeBite, run.ForceCount));

            // A shattered pack is not a quieter loss, it is a different event, and it has to
            // read as one or the talent is invisible: the pack detonates outward in the
            // Warden's colour instead of throwing red debris off the army.
            if (shattered)
            {
                _ctx.Audio.Play(AudioCue.ShieldBlock, 0.8f);
                _ctx.Effects.Shock(where, ShatterTint, 0.5f, 6.5f, 0.5f);
                _ctx.Effects.Burst(where, ShatterTint, 22, 6.2f, 0.65f);
                return;
            }

            // Debris scaled to what the pack actually took, not to its printed cost —
            // Bramble and Undying cut the bite, and the effect should show the bite.
            float loss = CameraFeel.ForLoss(beforeBite, run.ForceCount).Trauma;
            _ctx.Audio.Play(AudioCue.EnemyBite, 0.7f + 0.6f * loss);
            _ctx.Effects.Shock(where, LossTint, 0.6f, 2.2f + 2.6f * loss, 0.42f);
            _ctx.Effects.Burst(where, LossTint, 6 + Mathf.RoundToInt(14f * loss), 3.8f, 0.55f);

            Owe(beforeBite - run.ForceCount);

            if (run.ForceCount <= 0) OnForceDepleted();
        }

        /// <summary>
        /// A champion's swing. One answer now, and it is the shield.
        ///
        /// A barricade spans the road, so `inLane` arrives true for every one of them and the
        /// dodge branch below is dead for authored content. It is kept because an elite without
        /// a barricade would still be a lane decision, and because the block and the dodge have
        /// always paid the same — an answer that only half works teaches the player not to
        /// bother finding it.
        /// </summary>
        private void OnEliteSwing(int weight, int depth, int swings, bool inLane, Vector3 where)
        {
            bool blocked = _ctx.Shield.IsActive;
            bool dodged = !inLane;
            double cost = Elite.SwingCost(_ctx.Run.ForceCount, weight, swings, blocked, dodged);

            if (cost <= 0.0)
            {
                _ctx.Audio.Play(dodged && !blocked ? AudioCue.SpellCast : AudioCue.ShieldBlock, 0.85f);
                _ctx.Effects.Shock(where, ShatterTint, 0.5f, 6.0f, 0.45f);
                _ctx.Effects.Burst(where, ShatterTint, 16, 5.4f, 0.55f);
                // A BLOCK, not a dodge. Stepping out of the lane costs the champion nothing
                // and so earns nothing — the Warden's rule is about standing there and
                // taking it, and paying out for a dodge would hand the bonus to every hero
                // who simply steered well.
                if (blocked)
                    ConvertBlock(Elite.SwingCost(_ctx.Run.ForceCount, weight, swings, false, false), where);
                return;
            }

            RunState run = _ctx.Run;
            double before = run.ForceCount;
            run.SetForce(System.Math.Max(0.0, before - cost));
            _ctx.Crowd.SetForce(run.ForceCount);
            _ctx.Hud.SetForce(run.ForceCount);

            // From the champion TO the army, for the same reason the boss's blow now is: an
            // impact at the player's feet with nothing having arrived reads as the game
            // taking something rather than as a thing that hit them.
            var front = new Vector3(_ctx.Crowd.CenterX, 0f, _ctx.Crowd.FrontZ);
            _ctx.Effects.Bolt(where + Vector3.up * 1.1f, front, LossTint, 42f);
            _ctx.CameraRig.Apply(CameraFeel.ForLoss(before, run.ForceCount));
            _ctx.Audio.Play(AudioCue.BossBlow, 0.8f);

            Owe(before - run.ForceCount);

            if (run.ForceCount <= 0) OnForceDepleted();
        }

        /// <summary>
        /// WARDEN. Part of what a block saved joins the army instead.
        ///
        /// Inert for the other three, which is what makes it safe to call from all three
        /// block paths — the gate, the pack and the champion — rather than picking the one
        /// that happened to be convenient. A rule the player is told applies to blocking has
        /// to apply to every block, or they learn a rule the game does not have.
        /// </summary>
        private void ConvertBlock(double saved, Vector3 where)
        {
            double gained = HeroRoster.BlockConverts(_hero, saved);
            if (gained <= 0.0) return;

            RunState run = _ctx.Run;
            run.SetForce(run.ForceCount + gained);
            _ctx.Crowd.SetForce(run.ForceCount);
            _ctx.Hud.SetForce(run.ForceCount);
            _ctx.Effects.Burst(where, CritTint, 14, 4.6f, 0.65f);
            _ctx.Audio.Play(AudioCue.GateAdd, 0.7f);
        }

        /// <summary>
        /// REVENANT. A share of a loss is owed back, and arrives a moment later.
        ///
        /// The DELAY is the whole point: paid on the same frame it would be indistinguishable
        /// from the loss having been smaller, and the fantasy is the fallen getting up. Inert
        /// for everyone else, so every loss site can call it unconditionally.
        /// </summary>
        private void Owe(double lost)
        {
            double back = HeroRoster.Returns(_hero, lost);
            if (back <= 0.0) return;
            _returnPending += back;
            _returnIn = HeroRoster.ReturnDelaySeconds;
        }

        private void PayReturns()
        {
            RunState run = _ctx.Run;
            double back = _returnPending;
            _returnPending = 0.0;
            _returnIn = 0f;
            if (back <= 0.0 || run == null) return;

            run.SetForce(run.ForceCount + back);
            _ctx.Crowd.SetForce(run.ForceCount);
            _ctx.Hud.SetForce(run.ForceCount);

            var front = new Vector3(_ctx.Crowd.CenterX, 0f, _ctx.Crowd.FrontZ);
            _ctx.Effects.Shock(front, ReturnTint, 0.45f, 5.2f, 0.5f);
            _ctx.Effects.Burst(front, ReturnTint, 16, 4.6f, 0.7f);
            _ctx.Audio.Play(AudioCue.GateAdd, 0.6f);
        }

        /// <summary>A champion is down. It pays, and the payment has to be seen.</summary>
        private void OnEliteDefeated(int weight, int depth, Vector3 where)
        {
            RunState run = _ctx.Run;
            double bounty = Elite.Bounty(run.ForceCount, weight);
            run.SetForce(run.ForceCount + bounty);
            _ctx.Crowd.SetForce(run.ForceCount);
            _ctx.Hud.SetForce(run.ForceCount);

            _ctx.Audio.Play(AudioCue.LootReveal, 0.9f);
            _ctx.Effects.Shock(where, CritTint, 0.5f, 8.5f, 0.6f);
            _ctx.Effects.Burst(where, CritTint, 24, 6.2f, 0.75f);
            _ctx.CameraRig.PunchFov(3.0f);
        }

        private void OnForceDepleted()
        {
            // Second Wind stands the army back up before the ad prompt ever appears. Once
            // per run: a comeback the player earned with points, not a subscription to
            // immortality, and it fires ahead of the rewarded ad so the talent they bought
            // is never quietly replaced by a video.
            // Against the army that WALKED IN, not against par. Par is a share of the best
            // line through the round, and measured against the shipped generator it falls
            // below 1.0 from about round twenty — a revive sized off it would have handed a
            // deep-run player fewer men than they started the round with.
            double revived = Talents.SecondWindForce(_ctx.Run.StartingForce,
                _ctx.CurrentStats.Get(StatIds.SecondWind));
            if (revived > 0 && !_ctx.Run.SecondWindSpent)
            {
                _ctx.Run.SecondWindSpent = true;
                _ctx.Run.SetForce(revived);
                _ctx.Crowd.SetForce(revived);
                _ctx.Hud.SetForce(revived);
                _ctx.CameraRig.PunchFov(4.5f);
                var rally = new Vector3(_ctx.Crowd.CenterX, 0f, _ctx.Crowd.CenterZ);
                _ctx.Effects.Shock(rally, ShatterTint, 1.0f, 9f, 0.6f);
                _ctx.Effects.Burst(rally, ShatterTint, 28, 6f, 0.8f);
                return;
            }

            _awaitingPrompt = true;
            // Tick() stops here, so a live coaching prompt would sit frozen behind the
            // resurrect modal. Drop it; an un-taught step re-arms if the player revives.
            _ctx.Tutorial.EndPhase();
            // Same reason for the ward: CancelActive drops the block window without
            // refunding the cooldown, which ResetForPhase would.
            _ctx.Shield.CancelActive();
            _ctx.Ward.Clear();
            _ctx.Dome.Clear();
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
                // Never below the permanent floor: a player who paid for a revive and came
                // back with less than the army the game promised they could never lose would
                // be right to call that a bug.
                double revived = System.Math.Max(_ctx.ArmyFloor, _ctx.Run.StartingForce * 0.5);
                _ctx.Run.SetForce(revived);
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
                StartingForceCount = _ctx.Run.StartingForce,
                PeakForceCount = _ctx.Run.PeakForce,
                HeroStats = _ctx.CurrentStats,
                SpellChargesRemaining = _ctx.Spell.Charges,
                Distance = _ctx.Run.Distance,
                GatesHit = _ctx.Run.GatesHit,
                ReachedBoss = true
            };

            // The fork the act structure exists for. A boss at the end of EVERY round is a
            // boss that stops being an event; on the other rounds it comes to the finish
            // line, roars, and lets the player past.
            bool fight = RoundPlan.For(_ctx.Profile.CurrentLevelIndex).IsBossRound;
            _ctx.Machine.TransitionTo(fight ? (IGameState)_ctx.BossState : _ctx.ThreatState);
        }
    }
}
