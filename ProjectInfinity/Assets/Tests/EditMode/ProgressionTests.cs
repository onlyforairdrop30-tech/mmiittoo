using NUnit.Framework;
using ProjectInfinity.Progression;

namespace ProjectInfinity.Tests
{
    public class XpCurveTests
    {
        [Test]
        public void XpToNext_IncreasesWithLevel()
        {
            for (int level = 1; level < XpCurve.MaxLevel; level++)
            {
                Assert.Less(XpCurve.XpToNext(level), XpCurve.XpToNext(level + 1),
                    $"Level {level} should require less XP than {level + 1}");
            }
        }

        [Test]
        public void XpToNext_IsZeroAtMaxLevel()
        {
            Assert.AreEqual(0, XpCurve.XpToNext(XpCurve.MaxLevel));
        }

        [Test]
        public void TotalXpForLevel_IsCumulative()
        {
            long expected = 0;
            for (int level = 1; level < 10; level++)
            {
                Assert.AreEqual(expected, XpCurve.TotalXpForLevel(level), $"Cumulative XP at level {level}");
                expected += XpCurve.XpToNext(level);
            }
        }

        [Test]
        public void LevelForXp_IsTheInverseOfTotalXpForLevel()
        {
            for (int level = 1; level <= XpCurve.MaxLevel; level += 3)
            {
                Assert.AreEqual(level, XpCurve.LevelForXp(XpCurve.TotalXpForLevel(level)),
                    $"Level {level} round-trip");
            }
        }

        [Test]
        public void LevelForXp_ClampsToMaxLevel()
        {
            Assert.AreEqual(XpCurve.MaxLevel, XpCurve.LevelForXp(long.MaxValue / 2));
        }

        [Test]
        public void LevelForXp_NegativeXpIsLevelOne()
        {
            Assert.AreEqual(1, XpCurve.LevelForXp(-500));
        }

        [Test]
        public void XpIntoLevel_NeverExceedsRequirement()
        {
            for (long xp = 0; xp < 20000; xp += 137)
            {
                int level = XpCurve.LevelForXp(xp);
                long into = XpCurve.XpIntoLevel(xp);
                Assert.GreaterOrEqual(into, 0);
                Assert.Less(into, System.Math.Max(1, XpCurve.XpToNext(level)));
            }
        }

        [Test]
        public void KillXp_PenalisesTrivialContent()
        {
            long evenLevel = XpCurve.KillXp(20, 20, 100);
            long greyContent = XpCurve.KillXp(20, 5, 100);
            long dangerous = XpCurve.KillXp(20, 25, 100);

            Assert.Less(greyContent, evenLevel, "Grey mobs must give less XP");
            Assert.Less(evenLevel, dangerous, "Higher level enemies must give more XP");
            Assert.Greater(greyContent, 0, "Grey mobs still give something");
        }

        [Test]
        public void KillXp_IsNeverNegative()
        {
            Assert.GreaterOrEqual(XpCurve.KillXp(60, 1, 0), 0);
            Assert.GreaterOrEqual(XpCurve.KillXp(1, 60, 100), 0);
        }
    }
}
