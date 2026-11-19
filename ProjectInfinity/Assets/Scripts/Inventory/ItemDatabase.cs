using System.Collections.Generic;
using UnityEngine;

namespace ProjectInfinity.Inventory
{
    [CreateAssetMenu(menuName = "Project Infinity/Item Database", fileName = "ItemDatabase")]
    public class ItemDatabase : ScriptableObject
    {
        [SerializeField] private List<ItemDefinition> items = new List<ItemDefinition>();

        private Dictionary<string, ItemDefinition> _index;

        public IReadOnlyList<ItemDefinition> All => items;

        public ItemDefinition Get(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;
            if (_index == null) BuildIndex();
            return _index.TryGetValue(itemId, out var item) ? item : null;
        }

        public void Register(ItemDefinition item)
        {
            if (item == null || string.IsNullOrEmpty(item.itemId)) return;
            if (!items.Contains(item)) items.Add(item);
            _index = null;
        }

        private void BuildIndex()
        {
            _index = new Dictionary<string, ItemDefinition>();
            foreach (var item in items)
            {
                if (item != null && !string.IsNullOrEmpty(item.itemId)) _index[item.itemId] = item;
            }
        }

        private void OnEnable() => _index = null;
    }
}
