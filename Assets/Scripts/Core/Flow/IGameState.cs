namespace BattleRunner.Core.Flow
{
    /// <summary>
    /// One phase of the game loop. States own activating/deactivating whatever they
    /// control (scene roots, UI screens); the machine only sequences them.
    /// </summary>
    public interface IGameState
    {
        void Enter();
        void Tick(float deltaTime);
        void Exit();
    }
}
