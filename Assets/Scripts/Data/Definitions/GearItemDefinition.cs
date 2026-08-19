using BattleRunner.Core.Loot;
using BattleRunner.Core.Stats;
using UnityEngine;

namespace BattleRunner.Data.Definitions
{
    [CreateAssetMenu(menuName = "BattleRunner/Gear Item", fileName = "Gear")]
    public sealed class GearItemDefinition : ScriptableObject
    {
        public string Id;
        public string DisplayName;
        public GearSlot Slot;
        public Rarity Rarity;
        public StatModifier[] Modifiers;
        [TextArea] public string Flavor;

        public GearItemModel ToModel() => new GearItemModel(Id, Slot, Rarity, Modifiers);
    }
}
