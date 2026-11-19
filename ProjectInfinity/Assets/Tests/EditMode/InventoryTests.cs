using NUnit.Framework;
using ProjectInfinity.Inventory;
using ProjectInfinity.Progression;

namespace ProjectInfinity.Tests
{
    public class InventoryModelTests
    {
        [Test]
        public void Add_StacksUpToMaxStack()
        {
            var inventory = new InventoryModel(4);
            inventory.Add("material.ember_shard", 7, 5);

            Assert.AreEqual(7, inventory.CountOf("material.ember_shard"));
            Assert.AreEqual(2, inventory.UsedSlots, "7 items with max stack 5 need two slots");
        }

        [Test]
        public void Add_ReturnsLeftoverWhenFull()
        {
            var inventory = new InventoryModel(2);
            int leftover = inventory.Add("material.ember_shard", 10, 3);

            Assert.AreEqual(4, leftover, "2 slots x 3 stack = 6 stored, 4 rejected");
            Assert.AreEqual(6, inventory.CountOf("material.ember_shard"));
        }

        [Test]
        public void Add_NonStackableItemsUseOneSlotEach()
        {
            var inventory = new InventoryModel(5);
            inventory.Add("weapon.verdant_blade", 2, 1);

            Assert.AreEqual(2, inventory.UsedSlots);
        }

        [Test]
        public void Remove_TakesFromTheEndAndFreesSlots()
        {
            var inventory = new InventoryModel(4);
            inventory.Add("material.ember_shard", 6, 3);

            Assert.IsTrue(inventory.Remove("material.ember_shard", 6));
            Assert.AreEqual(0, inventory.CountOf("material.ember_shard"));
            Assert.AreEqual(0, inventory.UsedSlots);
        }

        [Test]
        public void Remove_FailsWhenNotEnough()
        {
            var inventory = new InventoryModel(4);
            inventory.Add("material.ember_shard", 2, 9);

            Assert.IsFalse(inventory.Remove("material.ember_shard", 3));
            Assert.AreEqual(2, inventory.CountOf("material.ember_shard"), "Failed removal must not change state");
        }

        [Test]
        public void HasSpaceFor_AccountsForExistingPartialStacks()
        {
            var inventory = new InventoryModel(1);
            inventory.Add("material.ember_shard", 2, 5);

            Assert.IsTrue(inventory.HasSpaceFor("material.ember_shard", 3, 5));
            Assert.IsFalse(inventory.HasSpaceFor("material.ember_shard", 4, 5));
            Assert.IsFalse(inventory.HasSpaceFor("weapon.verdant_blade", 1, 1));
        }

        [Test]
        public void Changed_FiresOnlyWhenSomethingMoved()
        {
            var inventory = new InventoryModel(1);
            int changes = 0;
            inventory.Changed += () => changes++;

            inventory.Add("material.ember_shard", 1, 1);
            inventory.Add("material.ember_shard", 1, 1);

            Assert.AreEqual(1, changes);
        }
    }

    public class StatBlockTests
    {
        [Test]
        public void Add_AccumulatesPerStat()
        {
            var block = new StatBlock();
            block.Add(StatType.Attack, 8f);
            block.Add(StatType.Attack, 4f);
            block.Add(StatType.Defense, 3f);

            Assert.AreEqual(12f, block[StatType.Attack], 0.001f);
            Assert.AreEqual(3f, block[StatType.Defense], 0.001f);
        }

        [Test]
        public void Add_MergesAnotherBlock()
        {
            var equipment = new StatBlock();
            equipment.Add(StatType.MaxHealth, 30f);

            var total = new StatBlock();
            total.Add(StatType.MaxHealth, 120f);
            total.Add(equipment);

            Assert.AreEqual(150f, total[StatType.MaxHealth], 0.001f);
        }

        [Test]
        public void Clone_DoesNotShareState()
        {
            var original = new StatBlock();
            original.Add(StatType.Attack, 5f);

            var copy = original.Clone();
            copy.Add(StatType.Attack, 5f);

            Assert.AreEqual(5f, original[StatType.Attack], 0.001f);
            Assert.AreEqual(10f, copy[StatType.Attack], 0.001f);
        }
    }
}
