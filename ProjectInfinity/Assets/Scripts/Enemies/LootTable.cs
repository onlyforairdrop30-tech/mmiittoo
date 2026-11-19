using System.Collections.Generic;

namespace ProjectInfinity.Enemies
{
    /// <summary>
    /// Loot rolls. Engine-free and seedable so the same algorithm runs on the authoritative server
    /// (server/src/sim/loot.js) and can be asserted in tests.
    /// </summary>
    public static class LootTable
    {
        public struct Drop
        {
            public string itemId;
            public int count;
        }

        public static List<Drop> Roll(EnemyDefinition definition, System.Random rng)
        {
            var drops = new List<Drop>();
            if (definition == null || definition.loot == null || rng == null) return drops;

            foreach (var entry in definition.loot)
            {
                if (string.IsNullOrEmpty(entry.itemId)) continue;
                if (rng.NextDouble() > entry.chance) continue;

                int min = entry.min < 1 ? 1 : entry.min;
                int max = entry.max < min ? min : entry.max;
                int count = min == max ? min : rng.Next(min, max + 1);
                drops.Add(new Drop { itemId = entry.itemId, count = count });
            }

            return drops;
        }

        public static int RollGold(EnemyDefinition definition, System.Random rng)
        {
            if (definition == null || definition.goldReward <= 0 || rng == null) return 0;
            int baseGold = definition.goldReward;
            return baseGold + rng.Next(0, baseGold / 2 + 1);
        }
    }
}
