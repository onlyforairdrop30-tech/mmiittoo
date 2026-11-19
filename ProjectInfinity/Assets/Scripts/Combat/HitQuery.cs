using System.Collections.Generic;
using UnityEngine;

namespace ProjectInfinity.Combat
{
    /// <summary>Spatial queries for melee/skill hits. Uses a shared buffer to avoid per-frame garbage.</summary>
    public static class HitQuery
    {
        private static readonly Collider[] Buffer = new Collider[48];

        /// <summary>Fills <paramref name="results"/> with living damageables of the given faction.</summary>
        public static int Nearby(Vector3 center, float radius, Faction targetFaction, List<IDamageable> results)
        {
            results.Clear();
            int count = Physics.OverlapSphereNonAlloc(center, radius, Buffer, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var health = Buffer[i].GetComponentInParent<Health>();
                if (health == null || !health.IsAlive || health.Faction != targetFaction) continue;
                if (!results.Contains(health)) results.Add(health);
            }
            return results.Count;
        }

        /// <summary>Closest living damageable of a faction within range (for target locking).</summary>
        public static Health Nearest(Vector3 from, float radius, Faction targetFaction)
        {
            int count = Physics.OverlapSphereNonAlloc(from, radius, Buffer, ~0, QueryTriggerInteraction.Ignore);
            Health best = null;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var health = Buffer[i].GetComponentInParent<Health>();
                if (health == null || !health.IsAlive || health.Faction != targetFaction) continue;
                float sqr = (health.transform.position - from).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = health;
                }
            }
            return best;
        }
    }
}
