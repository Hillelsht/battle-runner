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

        // --- Mechanics the deep tree hooks into ---------------------------------
        // A tree with hundreds of point-spends cannot be hundreds of "+2 Might" nodes;
        // that is filler, and a player feels it by tier three. Each of these is a NEW
        // thing to modify, consumed at exactly one site, so a deep node can hand the
        // player a toy rather than another number.

        /// <summary>Chance a gate resolves at double value.</summary>
        public const string GateCrit = "gatecrit";
        /// <summary>Fraction of banked overflow released as boss damage.</summary>
        public const string OverflowBank = "overflowbank";
        /// <summary>Extra yield per consecutive x gate, reset by a subtract gate.</summary>
        public const string ChainMultiply = "chainmultiply";
        /// <summary>Widens the lane window a gate will still score from.</summary>
        public const string Magnetism = "magnetism";
        /// <summary>Chance an enemy pack costs nothing and shatters.</summary>
        public const string PackShatter = "packshatter";
        /// <summary>Fraction of force restored by a free once-per-run revive. 0 disables it.</summary>
        public const string SecondWind = "secondwind";
        /// <summary>Boss HP fraction below which it dies outright.</summary>
        public const string Execute = "execute";
        /// <summary>Chance the spell fires a second time.</summary>
        public const string SpellEcho = "spellecho";
        /// <summary>Fraction of a blocked blow returned to the boss as damage.</summary>
        public const string ShieldReflect = "shieldreflect";

        public static readonly string[] All =
        {
            Damage, Health, Cooldown, SpellPower,
            GateYield, RunSpeed, EnemyResist, ShieldDuration, Fortune,
            GateCrit, OverflowBank, ChainMultiply, Magnetism, PackShatter,
            SecondWind, Execute, SpellEcho, ShieldReflect
        };
    }
}
