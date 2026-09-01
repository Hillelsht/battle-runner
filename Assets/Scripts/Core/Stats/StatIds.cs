namespace BattleRunner.Core.Stats
{
    /// <summary>Canonical stat identifiers. Definitions (display name, icon) live in the Data layer.</summary>
    public static class StatIds
    {
        // --- Boss-fight axes ----------------------------------------------------
        public const string Damage = "damage";
        public const string Health = "health";
        public const string Cooldown = "cooldown";
        /// <summary>Multiplies the spell's burst against a boss.</summary>
        public const string SpellPower = "spellpower";

        // --- Run axes -----------------------------------------------------------
        // These exist because the original three only mattered during the boss fight:
        // nothing a player bought changed the forty seconds of running that is most of
        // the game. Everything below pays off on the road.

        /// <summary>Fraction of extra force taken from every + and x gate.</summary>
        public const string GateYield = "gateyield";
        /// <summary>Fraction of extra run speed. Faster road, less reaction time.</summary>
        public const string RunSpeed = "runspeed";
        /// <summary>Fraction of an enemy pack's bite that is shrugged off.</summary>
        public const string EnemyResist = "enemyresist";
        /// <summary>Extra seconds a raised shield holds.</summary>
        public const string ShieldDuration = "shieldduration";
        /// <summary>Weights the loot roll toward rarer items.</summary>
        public const string Fortune = "fortune";

        public static readonly string[] All =
        {
            Damage, Health, Cooldown, SpellPower,
            GateYield, RunSpeed, EnemyResist, ShieldDuration, Fortune
        };
    }
}
