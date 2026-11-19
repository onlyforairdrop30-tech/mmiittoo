using System;

namespace ProjectInfinity.Combat
{
    /// <summary>
    /// Damage math. Engine-free on purpose: the same formula runs on the authoritative server
    /// (server/src/sim/combat.js) and in the EditMode tests, so prediction and authority agree.
    /// </summary>
    public static class CombatFormulas
    {
        /// <summary>Diminishing-return mitigation: 100 defence halves incoming damage.</summary>
        public static float Mitigate(float raw, float defense)
        {
            if (raw <= 0f) return 0f;
            float d = defense < 0f ? 0f : defense;
            return raw * (100f / (100f + d));
        }

        public static float ComputeDamage(float attack, float skillMultiplier, float defense,
                                          float critMultiplier, bool critical)
        {
            float raw = Math.Max(0f, attack) * Math.Max(0f, skillMultiplier);
            if (critical) raw *= Math.Max(1f, critMultiplier);
            return (float)Math.Round(Mitigate(raw, defense), 1, MidpointRounding.AwayFromZero);
        }

        public static bool RollCritical(float chance, float roll01)
        {
            if (chance <= 0f) return false;
            if (chance >= 1f) return true;
            return roll01 < chance;
        }

        /// <summary>Stamina cost of blocking a hit of the given size. A parried hit costs nothing.</summary>
        public static float BlockStaminaCost(float incoming) => 8f + incoming * 0.45f;

        /// <summary>PvP damage is damped so duels last long enough to reward skill.</summary>
        public static float PvpScale(float amount) => amount * 0.72f;
    }
}
