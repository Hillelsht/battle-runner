using System;
using BattleRunner.Core.Run;
using BattleRunner.Core.Stats;
using BattleRunner.Data.Definitions;
using UnityEngine;

namespace BattleRunner.Gameplay.Combat
{
    /// <summary>
    /// Flick-down shield: a block window, drawn from a magazine of raises.
    ///
    /// Three separate things are now buyable here, which is the point of the change: how
    /// LONG the window holds (ShieldDuration), how MANY raises are held at once
    /// (ShieldCharges), and how fast they come back (Cooldown). A player who has bought
    /// none of them gets exactly the shield that shipped before.
    /// </summary>
    public sealed class ShieldSystem
    {
        private readonly SpellDefinition _def;
        private readonly Magazine _magazine = new Magazine();
        private float _activeRemaining;
        private float _durationBonus;
        private float _activeDuration = 1f;

        public event Action Raised;

        public bool IsActive => _activeRemaining > 0f;

        /// <summary>Seconds of block window left; 0 when down. The ward animates on this.</summary>
        public float ActiveRemaining => _activeRemaining > 0f ? _activeRemaining : 0f;

        /// <summary>What the window was when raised — stat bonuses make it variable.</summary>
        public float ActiveDuration => _activeDuration;

        public float CooldownRemaining => _magazine.RefillRemaining;

        /// <summary>How full the magazine is, 0..1 — what the HUD dial reads.</summary>
        public float Fill => _magazine.Fill;

        public int Charges => _magazine.Charges;
        public int Capacity => _magazine.Capacity;

        /// <summary>
        /// A raise is available AND one is not already up.
        ///
        /// Holding the second condition even with charges in hand is deliberate: stacking
        /// two windows would spend a charge for no extra cover, and a player cannot see
        /// that they have wasted it.
        /// </summary>
        public bool Ready => _magazine.Ready && !IsActive;

        public ShieldSystem(SpellDefinition def) => _def = def;

        public void ApplyStats(StatSheet stats)
        {
            float scale = 1f - Mathf.Min(0.6f, stats?.Get(StatIds.Cooldown) ?? 0f);
            int extra = Mathf.Max(0, Mathf.RoundToInt(stats?.Get(StatIds.ShieldCharges) ?? 0f));
            _durationBonus = Mathf.Max(0f, stats?.Get(StatIds.ShieldDuration) ?? 0f);
            _magazine.Configure(1 + extra, _def.ShieldCooldownSeconds * scale);
        }

        public void ResetForPhase()
        {
            _magazine.Refill();
            _activeRemaining = 0f;
        }

        /// <summary>
        /// Drop the block window WITHOUT refunding the charge, for a phase that stops
        /// ticking — the resurrect modal — so the ward does not hang lit behind it.
        /// ResetForPhase would refill the magazine too and hand out a free shield.
        /// </summary>
        public void CancelActive() => _activeRemaining = 0f;

        public void TryRaise()
        {
            if (IsActive || !_magazine.TrySpend()) return;
            _activeDuration = _def.ShieldDurationSeconds + _durationBonus;
            _activeRemaining = _activeDuration;
            Raised?.Invoke();
        }

        public void Tick(float dt)
        {
            if (_activeRemaining > 0f) _activeRemaining -= dt;
            _magazine.Tick(dt);
        }
    }
}
