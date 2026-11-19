namespace ProjectInfinity.Progression
{
    /// <summary>
    /// Level 1..60 progression with diminishing returns.
    /// Deliberately engine-free so EditMode tests and the backend can share the same numbers.
    /// </summary>
    public static class XpCurve
    {
        public const int MaxLevel = 60;

        private const double Base = 90.0;
        private const double Growth = 1.115;

        /// <summary>XP required to go from <paramref name="level"/> to the next level.</summary>
        public static long XpToNext(int level)
        {
            if (level >= MaxLevel) return 0;
            if (level < 1) level = 1;
            return (long)System.Math.Round(Base * System.Math.Pow(Growth, level - 1));
        }

        /// <summary>Total XP needed to have reached <paramref name="level"/> from level 1.</summary>
        public static long TotalXpForLevel(int level)
        {
            if (level <= 1) return 0;
            if (level > MaxLevel) level = MaxLevel;
            long total = 0;
            for (int i = 1; i < level; i++) total += XpToNext(i);
            return total;
        }

        public static int LevelForXp(long totalXp)
        {
            int level = 1;
            long remaining = totalXp < 0 ? 0 : totalXp;
            while (level < MaxLevel)
            {
                long need = XpToNext(level);
                if (remaining < need) break;
                remaining -= need;
                level++;
            }
            return level;
        }

        /// <summary>XP earned inside the current level (for the HUD bar).</summary>
        public static long XpIntoLevel(long totalXp)
        {
            if (totalXp < 0) return 0;
            return totalXp - TotalXpForLevel(LevelForXp(totalXp));
        }

        /// <summary>
        /// Kill reward falls off when the player out-levels the content, so farming grey mobs
        /// can never be the fastest route to power.
        /// </summary>
        public static long KillXp(int playerLevel, int enemyLevel, long baseXp)
        {
            int diff = enemyLevel - playerLevel;
            double scale;
            if (diff >= 3) scale = 1.0;
            else if (diff >= 0) scale = 0.80 + diff * 0.10;
            else if (diff >= -5) scale = System.Math.Max(0.25, 0.80 + diff * 0.12);
            else scale = 0.10;

            return (long)System.Math.Round(baseXp * scale);
        }
    }
}
