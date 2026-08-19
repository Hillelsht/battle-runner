using System;

namespace BattleRunner.Core.Flow
{
    /// <summary>
    /// Plain C# state machine: Boot -> MainMenu -> RunLoading -> RunnerLoop ->
    /// BossEncounter -> LootPhase -> StatUpgrade -> MainMenu. Transitions requested
    /// during Enter/Exit are deferred to the next Tick so states can't recurse.
    /// </summary>
    public sealed class GameStateMachine
    {
        private IGameState _current;
        private IGameState _pending;
        private bool _transitioning;

        public IGameState Current => _current;

        public void TransitionTo(IGameState next)
        {
            if (next == null) throw new ArgumentNullException(nameof(next));
            if (ReferenceEquals(next, _current)) return;

            if (_transitioning)
            {
                _pending = next;
                return;
            }

            _transitioning = true;
            try
            {
                _current?.Exit();
                _current = next;
                _current.Enter();
            }
            finally
            {
                _transitioning = false;
            }
        }

        public void Tick(float deltaTime)
        {
            if (_pending != null)
            {
                IGameState next = _pending;
                _pending = null;
                TransitionTo(next);
            }
            _current?.Tick(deltaTime);
        }
    }
}
