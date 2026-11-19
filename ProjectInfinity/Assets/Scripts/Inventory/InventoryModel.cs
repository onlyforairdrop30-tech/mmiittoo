using System;
using System.Collections.Generic;

namespace ProjectInfinity.Inventory
{
    [Serializable]
    public class ItemStack
    {
        public string itemId;
        public int count;

        public ItemStack(string id, int amount)
        {
            itemId = id;
            count = amount;
        }

        public ItemStack Clone() => new ItemStack(itemId, count);
    }

    /// <summary>
    /// Engine-free inventory used by both the client and the tests. The server keeps the same
    /// rules in <c>server/src/sim/inventory.js</c>; the client copy is a prediction only.
    /// </summary>
    public class InventoryModel
    {
        private readonly List<ItemStack> _slots = new List<ItemStack>();

        public InventoryModel(int capacity)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            Capacity = capacity;
        }

        public int Capacity { get; }
        public IReadOnlyList<ItemStack> Slots => _slots;
        public int UsedSlots => _slots.Count;

        public event Action Changed;

        /// <summary>Adds items and returns the amount that did not fit.</summary>
        public int Add(string itemId, int count, int maxStack = 1)
        {
            if (string.IsNullOrEmpty(itemId) || count <= 0) return count;
            if (maxStack < 1) maxStack = 1;

            int remaining = count;

            if (maxStack > 1)
            {
                foreach (var slot in _slots)
                {
                    if (remaining == 0) break;
                    if (slot.itemId != itemId || slot.count >= maxStack) continue;
                    int space = maxStack - slot.count;
                    int moved = Math.Min(space, remaining);
                    slot.count += moved;
                    remaining -= moved;
                }
            }

            while (remaining > 0 && _slots.Count < Capacity)
            {
                int placed = Math.Min(maxStack, remaining);
                _slots.Add(new ItemStack(itemId, placed));
                remaining -= placed;
            }

            if (remaining != count) Changed?.Invoke();
            return remaining;
        }

        public bool Remove(string itemId, int count)
        {
            if (count <= 0) return true;
            if (CountOf(itemId) < count) return false;

            int remaining = count;
            for (int i = _slots.Count - 1; i >= 0 && remaining > 0; i--)
            {
                if (_slots[i].itemId != itemId) continue;
                int taken = Math.Min(_slots[i].count, remaining);
                _slots[i].count -= taken;
                remaining -= taken;
                if (_slots[i].count == 0) _slots.RemoveAt(i);
            }

            Changed?.Invoke();
            return true;
        }

        public int CountOf(string itemId)
        {
            int total = 0;
            foreach (var slot in _slots)
            {
                if (slot.itemId == itemId) total += slot.count;
            }
            return total;
        }

        public bool HasSpaceFor(string itemId, int count, int maxStack)
        {
            int free = (Capacity - _slots.Count) * Math.Max(1, maxStack);
            foreach (var slot in _slots)
            {
                if (slot.itemId == itemId) free += Math.Max(0, maxStack - slot.count);
            }
            return free >= count;
        }

        public List<ItemStack> Snapshot()
        {
            var copy = new List<ItemStack>(_slots.Count);
            foreach (var slot in _slots) copy.Add(slot.Clone());
            return copy;
        }
    }
}
