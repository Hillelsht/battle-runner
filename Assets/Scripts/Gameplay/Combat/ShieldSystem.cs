using System;
using BattleRunner.Core.Stats;
using BattleRunner.Data.Definitions;
using UnityEngine;

namespace BattleRunner.Gameplay.Combat
{
    /// <summary>Flick-down shield: a short invulnerability window on its own cooldown.</summary>
    public sealed class ShieldSystem
    {
        private readonly SpellDefinition _def;
        private float _cooldownRemaining;
        private float _activeRemaining;
        private float _cooldownScale = 1f;

        public event Action Raised;

        public bool IsActive => _activeRemaining > 0f;
        public float CooldownRemaining => _cooldownRemaining;
        public bool Ready => _cooldownRemaining <= 0f && !IsActive;

        public ShieldSystem(SpellDefinition def) => _def = def;

        public void ApplyStats(StatSheet stats) =>
            _cooldownScale = 1f - Mathf.Min(0.6f, stats?.Get(StatIds.Cooldown) ?? 0f);

        public void ResetForPhase()
        {
            _cooldownRemaining = 0f;
            _activeRemaining = 0f;
        }

        public void TryRaise()
        {
            if (!Ready) return;
            _activeRemaining = _def.ShieldDurationSeconds;
            _cooldownRemaining = _def.ShieldCooldownSeconds * _cooldownScale;
            Raised?.Invoke();
        }

        public void Tick(float dt)
        {
            if (_activeRemaining > 0f) _activeRemaining -= dt;
            if (_cooldownRemaining > 0f) _cooldownRemaining -= dt;
        }
    }
}
