using NUnit.Framework;
using ProjectInfinity.Combat;

namespace ProjectInfinity.Tests
{
    public class CombatFormulasTests
    {
        [Test]
        public void Mitigate_HalvesDamageAtHundredDefense()
        {
            Assert.AreEqual(50f, CombatFormulas.Mitigate(100f, 100f), 0.01f);
        }

        [Test]
        public void Mitigate_HasDiminishingReturns()
        {
            float at100 = CombatFormulas.Mitigate(100f, 100f);
            float at200 = CombatFormulas.Mitigate(100f, 200f);
            float at400 = CombatFormulas.Mitigate(100f, 400f);

            Assert.Less(at200, at100);
            Assert.Less(at400, at200);
            Assert.Greater(at400, 0f, "Mitigation must never fully negate damage");
        }

        [Test]
        public void Mitigate_IgnoresNegativeDefense()
        {
            Assert.AreEqual(100f, CombatFormulas.Mitigate(100f, -50f), 0.01f);
        }

        [Test]
        public void ComputeDamage_AppliesCriticalMultiplier()
        {
            float normal = CombatFormulas.ComputeDamage(50f, 1f, 0f, 1.5f, false);
            float critical = CombatFormulas.ComputeDamage(50f, 1f, 0f, 1.5f, true);

            Assert.AreEqual(50f, normal, 0.05f);
            Assert.AreEqual(75f, critical, 0.05f);
        }

        [Test]
        public void ComputeDamage_ScalesWithPowerMultiplier()
        {
            float light = CombatFormulas.ComputeDamage(40f, 1.5f, 0f, 1.5f, false);
            float heavy = CombatFormulas.ComputeDamage(40f, 3.2f, 0f, 1.5f, false);

            Assert.AreEqual(60f, light, 0.05f);
            Assert.AreEqual(128f, heavy, 0.1f);
            Assert.Greater(heavy, light * 2f);
        }

        [Test]
        public void ComputeDamage_IsNeverNegative()
        {
            Assert.AreEqual(0f, CombatFormulas.ComputeDamage(-10f, 1f, 100f, 1.5f, false), 0.001f);
        }

        [Test]
        public void RollCritical_RespectsChanceBoundaries()
        {
            Assert.IsFalse(CombatFormulas.RollCritical(0f, 0f));
            Assert.IsTrue(CombatFormulas.RollCritical(1f, 0.999f));
            Assert.IsTrue(CombatFormulas.RollCritical(0.3f, 0.29f));
            Assert.IsFalse(CombatFormulas.RollCritical(0.3f, 0.31f));
        }

        [Test]
        public void BlockStaminaCost_ScalesWithHitSize()
        {
            Assert.Greater(CombatFormulas.BlockStaminaCost(100f), CombatFormulas.BlockStaminaCost(10f));
        }
    }
}
