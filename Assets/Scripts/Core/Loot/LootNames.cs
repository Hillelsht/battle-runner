using BattleRunner.Core.Text;

namespace BattleRunner.Core.Loot
{
    /// <summary>
    /// What a rarity and a slot are CALLED.
    ///
    /// THESE SEVEN STRINGS HAD NO LITERAL IN THE CODEBASE. LootScreen rendered them straight out
    /// of `enum.ToString()` — `$"{item.Rarity} {item.Slot}"` — so "Legendary" and "Weapon" reached
    /// the player's eye without ever being written down anywhere a search could find them. Any
    /// extraction pass misses them in silence, and the only way to notice is to play the game in
    /// another language and read the loot card.
    ///
    /// Naming them here rather than at the call site so the next thing that shows a rarity finds
    /// the words already have a home.
    /// </summary>
    public static class LootNames
    {
        public static string Rarity(Core.Loot.Rarity rarity)
        {
            switch (rarity)
            {
                case Core.Loot.Rarity.Rare: return Loc.Get(LocKey.RarityRare);
                case Core.Loot.Rarity.Epic: return Loc.Get(LocKey.RarityEpic);
                case Core.Loot.Rarity.Legendary: return Loc.Get(LocKey.RarityLegendary);
                default: return Loc.Get(LocKey.RarityCommon);
            }
        }

        public static string Slot(GearSlot slot)
        {
            switch (slot)
            {
                case GearSlot.Armor: return Loc.Get(LocKey.GearSlotArmor);
                case GearSlot.Relic: return Loc.Get(LocKey.GearSlotRelic);
                default: return Loc.Get(LocKey.GearSlotWeapon);
            }
        }
    }
}
