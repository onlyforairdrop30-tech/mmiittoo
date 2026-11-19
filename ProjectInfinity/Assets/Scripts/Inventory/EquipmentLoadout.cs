using System;
using System.Collections.Generic;
using ProjectInfinity.Progression;
using UnityEngine;

namespace ProjectInfinity.Inventory
{
    /// <summary>Equips items by slot and pushes the aggregated bonus into player progression.</summary>
    public class EquipmentLoadout : MonoBehaviour
    {
        [SerializeField] private PlayerProgression progression;

        private readonly Dictionary<EquipmentSlot, ItemDefinition> _equipped =
            new Dictionary<EquipmentSlot, ItemDefinition>();

        public event Action Changed;

        public void Bind(PlayerProgression target) => progression = target;

        public ItemDefinition Get(EquipmentSlot slot) =>
            _equipped.TryGetValue(slot, out var item) ? item : null;

        public bool Equip(ItemDefinition item, int playerLevel)
        {
            if (item == null || !item.IsEquippable) return false;
            if (playerLevel < item.requiredLevel) return false;

            _equipped[item.slot] = item;
            Recalculate();
            return true;
        }

        public bool Unequip(EquipmentSlot slot)
        {
            if (!_equipped.Remove(slot)) return false;
            Recalculate();
            return true;
        }

        public void Recalculate()
        {
            var total = new StatBlock();
            foreach (var item in _equipped.Values) total.Add(item.ToStatBlock());
            if (progression != null) progression.SetEquipmentBlock(total);
            Changed?.Invoke();
        }
    }
}
