using System;
using ProjectInfinity.Progression;
using UnityEngine;

namespace ProjectInfinity.Inventory
{
    public enum ItemCategory { Material, Consumable, Weapon, Armor, Accessory, Quest, Cosmetic }

    public enum EquipmentSlot { None, Weapon, Offhand, Head, Chest, Hands, Legs, Feet, Ring, Amulet }

    [Serializable]
    public struct StatBonus
    {
        public StatType stat;
        public float amount;
    }

    [CreateAssetMenu(menuName = "Project Infinity/Item", fileName = "Item_")]
    public class ItemDefinition : ScriptableObject
    {
        [Tooltip("Stable id shared with the server. Never rename after release.")]
        public string itemId = "item.new";

        public string displayName = "New Item";
        [TextArea] public string description;
        public ItemCategory category = ItemCategory.Material;
        public EquipmentSlot slot = EquipmentSlot.None;
        public int maxStack = 1;
        public int goldValue = 1;
        public int requiredLevel = 1;
        public StatBonus[] bonuses = Array.Empty<StatBonus>();

        public bool IsEquippable => slot != EquipmentSlot.None;

        public StatBlock ToStatBlock()
        {
            var block = new StatBlock();
            if (bonuses == null) return block;
            foreach (var bonus in bonuses) block.Add(bonus.stat, bonus.amount);
            return block;
        }
    }
}
