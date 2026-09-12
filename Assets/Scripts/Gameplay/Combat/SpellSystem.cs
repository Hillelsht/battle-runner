using System;
using BattleRunner.Core.Run;
using BattleRunner.Core.Stats;
using BattleRunner.Data.Definitions;
using UnityEngine;

namespace BattleRunner.Gameplay.Combat
{
    /// <summary>
    /// Flick-up spell: a MAGAZINE of casts, gated here at the system rather than in the
    /// classifier (doc 02) — an intent with an empty magazine is swallowed and the UI
    /// pulses instead.
    ///
    /// It was a single cast on a cooldown. See Core/Run/Magazine for why the charge count
    /// is a stat worth spending points on, and the tree's Arsenal branch for what buys it.
    /// </summary>
    public sealed class SpellSystem
    {
        private readonly SpellDefinition _def;
        private readonly Magazine _magazine = new Magazine();

        /// <summary>Raised only when a cast actually fires; the active state applies the phase-specific effect.</summary>
        public event Action Cast;

        /// <summary>Seconds until the next charge returns. Zero when the magazine is full.</summary>
        public float CooldownRemaining => _magazine.RefillRemaining;

        /// <summary>How full the magazine is, 0..1 — what the HUD dial reads.</summary>
        public float Fill => _magazine.Fill;

        /// <summary>Casts in hand.</summary>
        public int Charges => _magazine.Charges;

        /// <summary>Casts in hand when full — how many pips the HUD draws.</summary>
        public int Capacity => _magazine.Capacity;

        public bool Ready => _magazine.Ready;

        public SpellSystem(SpellDefinition def) => _def = def;

        /// <summary>
        /// Cooldown is a fractional reduction capped at 60%; SpellCharges is a count of
        /// EXTRA casts, so a player who has bought none behaves exactly as before.
        /// </summary>
        public void ApplyStats(StatSheet stats)
        {
            float scale = 1f - Mathf.Min(0.6f, stats?.Get(StatIds.Cooldown) ?? 0f);
            int extra = Mathf.Max(0, Mathf.RoundToInt(stats?.Get(StatIds.SpellCharges) ?? 0f));
            _magazine.Configure(1 + extra, _def.CooldownSeconds * scale);
        }

        public void ResetForPhase() => _magazine.Refill();

        public void TryCast()
        {
            if (!_magazine.TrySpend()) return;
            Cast?.Invoke();
        }

        public void Tick(float dt) => _magazine.Tick(dt);
    }
}
