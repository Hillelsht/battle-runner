using BattleRunner.Core.Crowd;
using BattleRunner.Core.Tutorial;
using BattleRunner.Meta.UI;
using UnityEngine;

namespace BattleRunner.Gameplay
{
    /// <summary>
    /// Wires the engine-free <see cref="TutorialDirector"/> to the running game: arms each
    /// step when the situation it teaches actually arrives, reports the player's actions
    /// back, and drives the overlay.
    ///
    /// Holding the run is done by zeroing the run's forward speed, never by
    /// <c>Time.timeScale</c>. The world stops advancing while cooldowns, the HUD, the ad
    /// service and input all keep running — and a timeScale of 0 would stall a rewarded-ad
    /// callback, which is the one thing that must never wedge.
    /// </summary>
    public sealed class TutorialCoach
    {
        // How close the thing being taught must be before its prompt appears.
        private const float EnemyPromptMeters = 12f;   // inside the spell's real 15 m clear window
        private const float GatePromptMeters = 20f;
        private const float SteerPromptAfterMeters = 8f;

        private readonly GameContext _ctx;
        private readonly TutorialOverlay _overlay;
        private TutorialDirector _director;
        private int _laneAtArm;

        public TutorialCoach(GameContext ctx, TutorialOverlay overlay, int completedMask)
        {
            _ctx = ctx;
            _overlay = overlay;
            _director = new TutorialDirector(completedMask);
        }

        /// <summary>True while the run should stand still for a prompt.</summary>
        public bool HoldsRun => _director.HoldsRun;

        public bool IsComplete => _director.IsComplete;

        public void Subscribe()
        {
            _ctx.Spell.Cast += OnSpellCast;
            _ctx.Shield.Raised += OnShieldRaised;
            _ctx.TrackController.GateApplied += OnGateApplied;
        }

        public void Unsubscribe()
        {
            _ctx.Spell.Cast -= OnSpellCast;
            _ctx.Shield.Raised -= OnShieldRaised;
            _ctx.TrackController.GateApplied -= OnGateApplied;
        }

        /// <summary>
        /// Called every runner frame with UNSCALED dt, so a held prompt still times out even
        /// though the run itself is not advancing.
        /// </summary>
        public void TickRunner(float unscaledDt)
        {
            if (_director.IsComplete) { _overlay.Hide(); return; }

            ArmRunnerSteps();
            DetectLaneChange();
            _director.Tick(unscaledDt);
            Present();
        }

        /// <summary>Called every boss frame; arms the shield lesson on the first wind-up.</summary>
        public void TickBoss(float unscaledDt, bool telegraphActive)
        {
            if (_director.IsComplete) { _overlay.Hide(); return; }

            if (telegraphActive && _ctx.Shield.Ready)
                _director.TryArm(TutorialStep.Shield);

            _director.Tick(unscaledDt);
            Present();
        }

        /// <summary>A run ended (death, give-up, finish). Drop any prompt and bank progress.</summary>
        public void EndPhase()
        {
            _director.Dismiss();
            _overlay.Hide();
            Persist();
        }

        /// <summary>Write the taught steps onto the profile. The caller saves.</summary>
        public void Persist() => _ctx.Profile.TutorialMask = _director.CompletedMask;

        /// <summary>
        /// Forget everything taught, for a new game. The director captures its progress at
        /// construction, so without this the coach keeps the previous player's taught steps.
        ///
        /// Takes no mask on purpose. It used to accept one and the caller passed
        /// Profile.TutorialMask — but SaveProfile persists the coach onto the profile first,
        /// so by then the fresh profile's 0 had already been overwritten with the OLD
        /// director's mask, and the reset read it straight back. A new game is untaught by
        /// definition; there is no mask worth passing.
        /// </summary>
        /// <summary>
        /// Re-latch against the context's current profile, for switching save slots. Each
        /// slot has its own tutorial progress, and the director captures it at construction.
        /// </summary>
        public void AdoptProfile()
        {
            _director = new TutorialDirector(_ctx.Profile?.TutorialMask ?? 0);
            _overlay.Hide();
        }

        public void ResetProgress()
        {
            _director = new TutorialDirector();
            _overlay.Hide();
            Persist();
        }

        private void ArmRunnerSteps()
        {
            if (_director.HasPrompt) return;

            // Steer first, but only once the road has visibly moved — a prompt on frame one
            // reads as a menu, not as a thing about the world.
            if (!_director.IsDone(TutorialStep.Steer) && _ctx.Run.Distance >= SteerPromptAfterMeters)
            {
                if (_director.TryArm(TutorialStep.Steer)) LatchLane();
                return;
            }

            float front = _ctx.Crowd.FrontZ;

            // The spell, at a pack close enough that casting visibly removes it.
            if (!_director.IsDone(TutorialStep.Spell) && _ctx.Spell.Ready)
            {
                float toEnemy = _ctx.TrackController.DistanceToNextEnemy(front);
                if (toEnemy > 0f && toEnemy <= EnemyPromptMeters)
                {
                    _director.TryArm(TutorialStep.Spell);
                    return;
                }
            }

            // Gate vocabulary last of the runner steps, and never blocking: by now the player
            // has already taken gates, so this names what they saw rather than gating on it.
            if (!_director.IsDone(TutorialStep.Gate))
            {
                float toGate = _ctx.TrackController.DistanceToNextGate(front);
                if (toGate > 0f && toGate <= GatePromptMeters) _director.TryArm(TutorialStep.Gate);
            }
        }

        private void LatchLane() =>
            _laneAtArm = CrowdMath.LaneIndex(_ctx.Crowd.CenterX, _ctx.Config.Balance.LaneWidthMeters);

        private void DetectLaneChange()
        {
            if (!_director.HasPrompt || _director.ActiveStep != TutorialStep.Steer) return;

            // Completed by the crowd ARRIVING in another lane, not by the drag event: the
            // lesson is "my army moves", and the channel is wired straight to the crowd so
            // the state never sees lane events anyway.
            int lane = CrowdMath.LaneIndex(_ctx.Crowd.CenterX, _ctx.Config.Balance.LaneWidthMeters);
            if (lane != _laneAtArm) _director.Observe(TutorialSignal.LaneChanged);
        }

        private void Present()
        {
            if (!_director.HasPrompt) { _overlay.Hide(); return; }
            _overlay.Show(_director.Headline, _director.Detail);
            _overlay.SetPatience(_director.TimeoutProgress);
        }

        private void OnSpellCast() => _director.Observe(TutorialSignal.SpellCast);
        private void OnShieldRaised() => _director.Observe(TutorialSignal.ShieldRaised);
        private void OnGateApplied(Core.Run.GateOp op, int value) =>
            _director.Observe(TutorialSignal.GatePassed);
    }
}
