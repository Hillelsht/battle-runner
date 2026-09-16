using BattleRunner.Core.Flow;
using BattleRunner.Core.Heroes;
using BattleRunner.Gameplay.Crowd;
using BattleRunner.Gameplay.Menu;
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
            // PARKED, because the follow never stops on its own: the crowd component lives on
            // a disabled GameObject during the menus, so it is not null and the rig would
            // happily keep easing toward a frozen army three hundred metres from the stage.
            _ctx.CameraRig?.Park(HeroStage.Eye, HeroStage.Aim);
            _ctx.HeroScreen.Show(OnChoose, OnPreview, OnFight,
                _ctx.Profile != null ? _ctx.Profile.HeroId : 0);
        }

        public void Tick(float deltaTime) { }

        public void Exit()
        {
            _ctx.HeroScreen.Hide();
            _ctx.HeroStage?.Hide();
            _ctx.CameraRig?.Release();
        }

        /// <summary>
        /// Bring a hero forward. Fires on every selection including the first, so the stage is
        /// never empty while the screen is up.
        ///
        /// The colours come off the profile rather than from the stage, so the figure on the
        /// plinth is painted the same two colours the army will be — the select screen teaches
        /// the palette the run is played in.
        /// </summary>
        private void OnPreview(int heroId)
        {
            if (_ctx.HeroStage == null) return;
            HeroClass hero = HeroRoster.FromSaved(heroId);
            HeroProfile profile = HeroRoster.For(hero);
            _ctx.HeroStage.Show(hero, ThemePalette.ToColor(profile.Body),
                ThemePalette.ToColor(profile.Glow));
            _ctx.Audio?.Play(Core.Audio.AudioCue.UiTap, 0.7f);
        }

        /// <summary>Replay the attack — the one act that will not play on its own.</summary>
        private void OnFight()
        {
            if (_ctx.HeroStage == null) return;
            _ctx.HeroStage.Play(HeroAct.Fight);
            _ctx.Audio?.Play(Core.Audio.AudioCue.EnemyBite, 0.75f);
        }

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
