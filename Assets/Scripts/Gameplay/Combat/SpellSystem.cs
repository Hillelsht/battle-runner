using System;
using BattleRunner.Core.Stats;
using BattleRunner.Data.Definitions;
using UnityEngine;

namespace BattleRunner.Gameplay.Combat
{
    /// <summary>
    /// Flick-up spell: cooldown gating lives here at the system, not in the classifier
    /// (doc 02) — an intent during cooldown is swallowed and the UI pulses instead.
    /// </summary>
    public sealed class SpellSystem
    {
        private readonly SpellDefinition _def;
        private float _cooldownRemaining;
        private float _cooldownScale = 1f;

        /// <summary>Raised only when a cast actually fires; the active state applies the phase-specific effect.</summary>
        public event Action Cast;

        public float CooldownRemaining => _cooldownRemaining;
        public bool Ready => _cooldownRemaining <= 0f;

        public SpellSystem(SpellDefinition def) => _def = def;

        /// <summary>The Cooldown stat is a fractional reduction, capped at 60%.</summary>
        public void ApplyStats(StatSheet stats) =>
            _cooldownScale = 1f - Mathf.Min(0.6f, stats?.Get(StatIds.Cooldown) ?? 0f);

        public void ResetForPhase() => _cooldownRemaining = 0f;

        public void TryCast()
        {
            if (!Ready) return;
            _cooldownRemaining = _def.CooldownSeconds * _cooldownScale;
            Cast?.Invoke();
        }

        public void Tick(float dt)
        {
            if (_cooldownRemaining > 0f) _cooldownRemaining -= dt;
        }
    }
}
