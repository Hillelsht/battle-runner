using BattleRunner.Core.Feel;
using BattleRunner.Core.Flow;
using BattleRunner.Core.Progression;
using BattleRunner.Data.Definitions;
using UnityEngine;

namespace BattleRunner.Gameplay.States
{
    /// <summary>
    /// The act's boss, at the end of a road it is not going to let you leave by.
    ///
    /// WHY THIS EXISTS. A boss at the end of every single round is a boss that stops being an
    /// event: `RunnerLoopState.OnFinishReached` transitioned unconditionally into a fight, so
    /// the sixth Bone Colossus of the evening was exactly as significant as the first. Fights
    /// are now the last round of an act — but a boss the player has never seen is not
    /// something they are building toward either. So on every other round it comes to the
    /// finish line, wakes up, roars, and lets them past.
    ///
    /// IT COMES NEARER EACH TIME. ThreatStep counts rounds into the act, and the boss stands
    /// closer and hits the camera harder on each one. By the last threat before the fight it
    /// is close enough to read every horn, which is the entire point of spending three rounds
    /// not fighting it.
    ///
    /// DELIBERATELY NOT WIRED TO THE TUTORIAL COACH. The coach arms its shield lesson on the
    /// first telegraph it sees, and a threat telegraphs without ever landing a blow — teaching
    /// "flick down to block" against an attack that cannot arrive, and asking a finished run
    /// to hold while it does.
    /// </summary>
    public sealed class BossThreatState : IGameState
    {
        private const float SettleSeconds = 0.45f;
        private const float WindSeconds = 1.20f;
        private const float HoldSeconds = 0.70f;

        /// <summary>How far out it stands on the first threat of an act, and how much it closes each round.</summary>
        private const float FirstStandoffMeters = 40f;
        private const float ClosePerStepMeters = 7f;
        private const float NearestStandoffMeters = 17f;

        private static readonly Color RoarTint = new Color(1.70f, 0.42f, 0.22f);

        private readonly GameContext _ctx;
        private BossDefinition _boss;
        private Vector3 _position;
        private float _elapsed;
        private float _menace;
        private bool _roared;
        private bool _leaving;

        public BossThreatState(GameContext ctx) => _ctx = ctx;

        public void Enter()
        {
            _elapsed = 0f;
            _roared = false;
            _leaving = false;

            RoundPlan plan = RoundPlan.For(_ctx.Profile.CurrentLevelIndex);
            _boss = _ctx.Config.BossFor(_ctx.Profile.CurrentLevelIndex);

            // 0 on the first threat of an act, 1 on the last before the fight. Everything
            // that escalates reads off this rather than off the raw step, so an act of three
            // and an act of five both build to the same peak.
            int steps = Mathf.Max(1, plan.ActLength - 1);
            _menace = Mathf.Clamp01(plan.ThreatStep / (float)steps);

            float standoff = Mathf.Max(NearestStandoffMeters,
                FirstStandoffMeters - ClosePerStepMeters * plan.ThreatStep);
            _position = new Vector3(0f, 0f, _ctx.Crowd.CenterZ + standoff);

            if (_boss != null)
            {
                _ctx.BossView.Show(_boss, _position);
                // Named, but with no health bar: there is nothing to whittle down, and a full
                // red bar would promise a fight that is not going to happen this round.
                _ctx.Hud.ShowBossBar(_boss.DisplayName, withHealth: false);
            }
        }

        public void Tick(float dt)
        {
            if (_leaving) return;

            // The army keeps moving, so it walks up to the thing rather than stopping dead in
            // front of it. Nothing else ticks: no spell, no shield, no gates.
            _ctx.Crowd.Tick(dt);
            _elapsed += dt;

            float wind = Mathf.Clamp01((_elapsed - SettleSeconds) / WindSeconds);
            _ctx.BossView.SetTelegraph(wind);
            _ctx.CameraRig.SetTelegraph(wind);

            if (!_roared && _elapsed >= SettleSeconds + WindSeconds)
            {
                _roared = true;
                Roar();
            }

            if (_elapsed >= SettleSeconds + WindSeconds + HoldSeconds)
            {
                _leaving = true;
                _ctx.Machine.TransitionTo(_ctx.LootState);
            }
        }

        private void Roar()
        {
            // Two rings at different speeds so the wave has depth, exactly as the boss death
            // beat does — this is the same language, turned down, and turned up again on each
            // successive threat until the fight itself.
            float force = 0.55f + 0.45f * _menace;
            _ctx.Effects.Shock(_position, RoarTint, 1.2f, 9f + 7f * _menace, 0.55f);
            _ctx.Effects.Shock(_position, RoarTint, 0.6f, 5f + 4f * _menace, 0.40f);
            _ctx.Effects.Burst(_position + Vector3.up * 1.6f, RoarTint,
                10 + Mathf.RoundToInt(18f * _menace), 5.5f, 0.7f);

            _ctx.CameraRig.AddTrauma(0.30f * force);
            _ctx.CameraRig.PunchFov(2.0f + 2.4f * _menace);
        }

        public void Exit()
        {
            _ctx.BossView.SetTelegraph(0f);
            _ctx.BossView.Hide();
            _ctx.Hud.HideBossBar();
            _ctx.CameraRig.SetTelegraph(0f);
            _ctx.Effects.Clear();
        }
    }
}
