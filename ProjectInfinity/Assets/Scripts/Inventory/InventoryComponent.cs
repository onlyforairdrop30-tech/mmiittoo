using System;
using UnityEngine;

namespace ProjectInfinity.Inventory
{
    /// <summary>Scene-side owner of the player's inventory, gold and equipment.</summary>
    public class InventoryComponent : MonoBehaviour
    {
        [SerializeField] private ItemDatabase database;
        [SerializeField] private int capacity = 24;
        [SerializeField] private int startingGold = 0;

        public InventoryModel Model { get; private set; }
        public int Gold { get; private set; }
        public ItemDatabase Database => database;

        public event Action Changed;
        public event Action<ItemDefinition, int> ItemAdded;
        public event Action<int> GoldChanged;

        public void Bind(ItemDatabase db) => database = db;

        private void Awake()
        {
            Model = new InventoryModel(capacity);
            Model.Changed += () => Changed?.Invoke();
            Gold = startingGold;
        }

        /// <summary>Adds an item by id. Returns false when nothing could be added (full).</summary>
        public bool TryGive(string itemId, int count = 1)
        {
            var def = database != null ? database.Get(itemId) : null;
            int maxStack = def != null ? Mathf.Max(1, def.maxStack) : 1;
            int leftover = Model.Add(itemId, count, maxStack);
            int added = count - leftover;
            if (added <= 0) return false;

            ItemAdded?.Invoke(def, added);
            return true;
        }

        public bool TryConsume(string itemId, int count = 1) => Model.Remove(itemId, count);

        public void AddGold(int amount)
        {
            if (amount == 0) return;
            Gold = Mathf.Max(0, Gold + amount);
            GoldChanged?.Invoke(Gold);
        }

        public bool TrySpendGold(int amount)
        {
            if (amount < 0 || Gold < amount) return false;
            AddGold(-amount);
            return true;
        }
    }
}
