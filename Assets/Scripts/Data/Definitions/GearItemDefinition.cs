using BattleRunner.Core.Loot;
using BattleRunner.Core.Stats;
using UnityEngine;

namespace BattleRunner.Data.Definitions
{
    [CreateAssetMenu(menuName = "BattleRunner/Gear Item", fileName = "Gear")]
    public sealed class GearItemDefinition : ScriptableObject
    {
        public string Id;
        public GearSlot Slot;
        public Rarity Rarity;
        public StatModifier[] Modifiers;

        /// <summary>
        /// The name and the flavour line are keys, resolved on read. These definitions are
        /// created once at bootstrap, so resolved strings here would be baked before the player
        /// has even reached the language control.
        /// </summary>
        public Core.Text.LocKey NameKey;
        public Core.Text.LocKey FlavorKey;

        public string DisplayName => Core.Text.Loc.Get(NameKey);
        public string Flavor => Core.Text.Loc.Get(FlavorKey);

        public GearItemModel ToModel() => new GearItemModel(Id, Slot, Rarity, Modifiers);
    }
}
