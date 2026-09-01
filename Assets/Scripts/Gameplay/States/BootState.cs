using BattleRunner.Core.Flow;

namespace BattleRunner.Gameplay.States
{
    /// <summary>Bootstrap already did the heavy lifting in Awake; this state exists so the flow has one entry point.</summary>
    public sealed class BootState : IGameState
    {
        private readonly GameContext _ctx;

        public BootState(GameContext ctx) => _ctx = ctx;

        public void Enter() { }

        // Straight to the slot picker: with save slots there is no "the" profile
        // until the player has said which game they are playing.
        public void Tick(float deltaTime) => _ctx.Machine.TransitionTo(_ctx.SlotState);

        public void Exit() { }
    }
}
