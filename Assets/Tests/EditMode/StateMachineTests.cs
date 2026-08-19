using System.Collections.Generic;
using BattleRunner.Core.Flow;
using NUnit.Framework;

namespace BattleRunner.Tests
{
    [TestFixture]
    public class StateMachineTests
    {
        private sealed class RecordingState : IGameState
        {
            private readonly string _name;
            private readonly List<string> _log;
            public GameStateMachine Machine;
            public IGameState TransitionOnEnter;

            public RecordingState(string name, List<string> log)
            {
                _name = name;
                _log = log;
            }

            public void Enter()
            {
                _log.Add($"{_name}:enter");
                if (TransitionOnEnter != null) Machine.TransitionTo(TransitionOnEnter);
            }

            public void Tick(float dt) => _log.Add($"{_name}:tick");
            public void Exit() => _log.Add($"{_name}:exit");
        }

        [Test]
        public void Transition_ExitsOldStateBeforeEnteringNew()
        {
            var log = new List<string>();
            var machine = new GameStateMachine();
            var a = new RecordingState("a", log);
            var b = new RecordingState("b", log);

            machine.TransitionTo(a);
            machine.TransitionTo(b);

            CollectionAssert.AreEqual(new[] { "a:enter", "a:exit", "b:enter" }, log);
            Assert.AreSame(b, machine.Current);
        }

        [Test]
        public void Tick_ReachesOnlyCurrentState()
        {
            var log = new List<string>();
            var machine = new GameStateMachine();
            machine.TransitionTo(new RecordingState("a", log));
            machine.Tick(0.016f);
            CollectionAssert.AreEqual(new[] { "a:enter", "a:tick" }, log);
        }

        [Test]
        public void TransitionToSameState_IsIgnored()
        {
            var log = new List<string>();
            var machine = new GameStateMachine();
            var a = new RecordingState("a", log);
            machine.TransitionTo(a);
            machine.TransitionTo(a);
            CollectionAssert.AreEqual(new[] { "a:enter" }, log);
        }

        [Test]
        public void TransitionRequestedDuringEnter_DefersToNextTick()
        {
            var log = new List<string>();
            var machine = new GameStateMachine();
            var c = new RecordingState("c", log);
            var b = new RecordingState("b", log) { Machine = machine, TransitionOnEnter = c };

            machine.TransitionTo(b); // b's Enter asks for c — must not recurse mid-Enter
            CollectionAssert.AreEqual(new[] { "b:enter" }, log);

            machine.Tick(0.016f); // deferred transition lands here
            CollectionAssert.AreEqual(new[] { "b:enter", "b:exit", "c:enter", "c:tick" }, log);
            Assert.AreSame(c, machine.Current);
        }

        [Test]
        public void FullGameLoopSequence_RunsInOrder()
        {
            var log = new List<string>();
            var machine = new GameStateMachine();
            string[] phases = { "boot", "menu", "loading", "run", "boss", "loot", "upgrade", "menu2" };

            foreach (string phase in phases)
                machine.TransitionTo(new RecordingState(phase, log));

            Assert.AreEqual("menu2:enter", log[log.Count - 1]);
            // Every intermediate state exited exactly once, in order.
            for (int i = 0; i < phases.Length - 1; i++)
                Assert.Contains($"{phases[i]}:exit", log);
        }
    }
}
