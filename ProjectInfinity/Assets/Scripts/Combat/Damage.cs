using UnityEngine;

namespace ProjectInfinity.Combat
{
    public enum Faction { Player, Hostile, Neutral }

    public enum DamageOutcome { Applied, Blocked, Parried, Invulnerable, Immune, Dead }

    public struct DamageInfo
    {
        public float amount;
        public Faction sourceFaction;
        public string sourceName;
        public bool isCritical;
        public bool isSkill;
        public Vector3 hitPoint;
        public Vector3 knockback;

        public static DamageInfo Melee(float amount, Faction faction, string source, Vector3 at, bool crit = false)
        {
            return new DamageInfo
            {
                amount = amount,
                sourceFaction = faction,
                sourceName = source,
                isCritical = crit,
                hitPoint = at,
                knockback = Vector3.zero
            };
        }
    }

    public struct DamageResult
    {
        public DamageOutcome outcome;
        public float dealt;

        public bool WasNegated => outcome != DamageOutcome.Applied;
    }

    public interface IDamageable
    {
        Faction Faction { get; }
        bool IsAlive { get; }
        DamageResult ApplyDamage(in DamageInfo info);
    }
}
