using System.Collections.Generic;
using BattleRunner.Core.Flow;
using BattleRunner.Core.Save;
using BattleRunner.Meta.Services;

namespace BattleRunner.Gameplay.States
{
    /// <summary>
    /// The first screen: which of the three saves to play. Choosing one makes it the active
    /// profile for the session; the menu that follows acts on whatever was chosen here.
    /// </summary>
    public sealed class SlotSelectState : IGameState
    {
        private readonly GameContext _ctx;

        public SlotSelectState(GameContext ctx) => _ctx = ctx;

        public void Enter()
        {
            _ctx.ArenaRoot.SetActive(false);
            _ctx.Hud.Hide();
            _ctx.SlotScreen.Show(OnPlay, OnErase);
            Refresh();
        }

        public void Tick(float deltaTime) => _ctx.SlotScreen.Tick(deltaTime);

        public void Exit() => _ctx.SlotScreen.Hide();

        private void Refresh()
        {
            var summaries = new List<SaveSlotSummary>(SaveSlots.Count);
            for (int slot = 0; slot < SaveSlots.Count; slot++)
            {
                FileSaveService service = FileSaveService.ForSlot(slot);
                bool exists = service.Exists();
                // Reading the profile is the only way to describe it, and three small JSON
                // files on a menu screen is not a budget worth optimising.
                PlayerProfile profile = exists ? service.Load() : null;
                summaries.Add(SaveSlots.Summarize(slot, profile, exists));
            }
            _ctx.SlotScreen.Refresh(summaries);
        }

        private void OnPlay(int slot)
        {
            _ctx.ActivateSlot(slot);
            _ctx.Machine.TransitionTo(_ctx.MenuState);
        }

        private void OnErase(int slot)
        {
            FileSaveService.ForSlot(slot).Delete();
            Refresh();
        }
    }
}
