namespace BattleRunner.Core.Stats
{
    /// <summary>Canonical stat identifiers. Definitions (display name, icon) live in the Data layer.</summary>
    public static class StatIds
    {
        public const string Damage = "damage";
        public const string Health = "health";
        public const string Cooldown = "cooldown";

        public static readonly string[] All = { Damage, Health, Cooldown };
    }
}
