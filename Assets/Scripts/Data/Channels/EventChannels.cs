using System;
using BattleRunner.Core.Run;
using UnityEngine;

namespace BattleRunner.Data.Channels
{
    /// <summary>
    /// ScriptableObject event channels: publishers raise, subscribers listen, neither
    /// references the other. Instances are created at runtime by the bootstrap for the
    /// greybox build; they can be promoted to authored assets without code changes.
    /// </summary>
    public abstract class EventChannel<T> : ScriptableObject
    {
        private Action<T> _listeners;

        public void Raise(T payload) => _listeners?.Invoke(payload);

        public void Subscribe(Action<T> listener) => _listeners += listener;
        public void Unsubscribe(Action<T> listener) => _listeners -= listener;
    }

    public sealed class VoidEventChannel : ScriptableObject
    {
        private Action _listeners;

        public void Raise() => _listeners?.Invoke();
        public void Subscribe(Action listener) => _listeners += listener;
        public void Unsubscribe(Action listener) => _listeners -= listener;
    }

    public sealed class FloatEventChannel : EventChannel<float> { }

    public sealed class LongEventChannel : EventChannel<long> { }

    public sealed class RunResultEventChannel : EventChannel<RunResult> { }
}
