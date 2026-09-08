using UnityEngine;

namespace BattleRunner.Gameplay
{
    /// <summary>Hosts the state machine: the only gameplay Update in the project ticks everything (doc 04).</summary>
    public sealed class GameFlowController : MonoBehaviour
    {
        private GameContext _ctx;

        public void Initialize(GameContext ctx) => _ctx = ctx;

        private void Update()
        {
            if (_ctx == null) return;
            float dt = Time.deltaTime;
            _ctx.Machine.Tick(dt);
            _ctx.Ads.Tick(dt);
            // UNSCALED. The music cross-fade must not stall if anything ever pauses the
            // game, and Time.timeScale appears nowhere in this project precisely so that
            // nothing quietly depends on it.
            (_ctx.Audio as Meta.Services.AudioDirector)?.Tick(Time.unscaledDeltaTime);
        }
    }
}
