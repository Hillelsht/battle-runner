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
        private float _durationBonus;
        private float _activeDuration = 1f;

        public event Action Raised;

        public bool IsActive => _activeRemaining > 0f;

        /// <summary>Seconds of block window left; 0 when down. The ward animates on this.</summary>
        public float ActiveRemaining => _activeRemaining > 0f ? _activeRemaining : 0f;

        /// <summary>What the window was when raised — stat bonuses make it variable.</summary>
        public float ActiveDuration => _activeDuration;
        public float CooldownRemaining => _cooldownRemaining;
        public bool Ready => _cooldownRemaining <= 0f && !IsActive;

        public ShieldSystem(SpellDefinition def) => _def = def;

        public void ApplyStats(StatSheet stats)
        {
            _cooldownScale = 1f - Mathf.Min(0.6f, stats?.Get(StatIds.Cooldown) ?? 0f);
            _durationBonus = Mathf.Max(0f, stats?.Get(StatIds.ShieldDuration) ?? 0f);
        }

        public void ResetForPhase()
        {
            _cooldownRemaining = 0f;
            _activeRemaining = 0f;
        }

        /// <summary>
        /// Drop the block window WITHOUT refunding the cooldown, for a phase that stops
        /// ticking — the resurrect modal — so the ward does not hang lit behind it.
        /// ResetForPhase would zero the cooldown too and hand out a free shield.
        /// </summary>
        public void CancelActive() => _activeRemaining = 0f;

        public void TryRaise()
        {
            if (!Ready) return;
            _activeDuration = _def.ShieldDurationSeconds + _durationBonus;
            _activeRemaining = _activeDuration;
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
