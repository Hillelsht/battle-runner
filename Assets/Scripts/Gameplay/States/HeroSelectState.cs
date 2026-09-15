using BattleRunner.Core.Flow;
using BattleRunner.Core.Heroes;
using BattleRunner.Gameplay.Crowd;
using BattleRunner.Meta.Services;

namespace BattleRunner.Gameplay.States
{
    /// <summary>
    /// Between picking a save and the main menu: who is leading this army.
    ///
    /// ONCE PER SAVE, and only for a save that has never been asked. That is what
    /// PlayerProfile.HeroChosen is for and why it exists beside HeroId — "chose the Warden"
    /// and "was never offered a choice" both store HeroId = 0, so the id alone cannot tell
    /// a new save from a returning one, and every existing player would have been sent back
    /// through this screen on the next launch.
    ///
    /// It writes the choice through immediately rather than holding it until the run starts:
    /// the screen is the last thing between the player and the menu, and a choice that only
    /// persisted on some later save would silently revert if they backgrounded the app here.
    /// </summary>
    public sealed class HeroSelectState : IGameState
    {
        private readonly GameContext _ctx;

        public HeroSelectState(GameContext ctx) => _ctx = ctx;

        /// <summary>Whether this profile still owes us a choice.</summary>
        public static bool IsOwed(GameContext ctx) =>
            ctx != null && ctx.Profile != null && !ctx.Profile.HeroChosen;

        public void Enter()
        {
            _ctx.ArenaRoot.SetActive(false);
            _ctx.Hud.Hide();
            _ctx.HeroScreen.Show(OnChoose, _ctx.Profile != null ? _ctx.Profile.HeroId : 0);
        }

        public void Tick(float deltaTime) { }

        public void Exit() => _ctx.HeroScreen.Hide();

        private void OnChoose(int heroId)
        {
            if (_ctx.Profile != null)
            {
                _ctx.Profile.HeroId = (int)HeroRoster.FromSaved(heroId);
                _ctx.Profile.HeroChosen = true;
                _ctx.SaveProfile();
            }

            // The hero's stat block composes through ProfileStatsResolver, so the sheet has
            // to be rebuilt — it was resolved in ActivateSlot, before this choice existed.
            _ctx.CurrentStats = ProfileStatsResolver.Resolve(_ctx.Profile, _ctx.Config);
            HeroOutfit.Apply(_ctx);
            _ctx.Machine.TransitionTo(_ctx.MenuState);
        }
    }
}
