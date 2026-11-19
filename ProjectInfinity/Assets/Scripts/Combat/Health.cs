using System;
using UnityEngine;

namespace ProjectInfinity.Combat
{
    /// <summary>Health for any combatant. Damage resolution is virtual so guards can intercept it.</summary>
    public class Health : MonoBehaviour, IDamageable
    {
        [SerializeField] protected Faction faction = Faction.Hostile;
        [SerializeField] protected float maxHealth = 100f;
        [SerializeField] protected float defense;

        public float Current { get; protected set; }
        public float Max => maxHealth;
        public Faction Faction => faction;
        public bool IsAlive => Current > 0f;

        public float Defense
        {
            get => defense;
            set => defense = Mathf.Max(0f, value);
        }

        public event Action<float, float> HealthChanged;
        public event Action<DamageInfo, DamageResult> Damaged;
        public event Action<DamageInfo> Died;

        protected virtual void Awake()
        {
            Current = maxHealth;
        }

        public void Configure(float max, float def, Faction who)
        {
            maxHealth = Mathf.Max(1f, max);
            defense = Mathf.Max(0f, def);
            faction = who;
            Current = maxHealth;
            HealthChanged?.Invoke(Current, maxHealth);
        }

        public DamageResult ApplyDamage(in DamageInfo info)
        {
            var result = Resolve(info);

            if (result.outcome == DamageOutcome.Applied || result.outcome == DamageOutcome.Blocked ||
                result.outcome == DamageOutcome.Parried)
            {
                HealthChanged?.Invoke(Current, maxHealth);
                Damaged?.Invoke(info, result);
            }

            if (result.outcome == DamageOutcome.Applied && !IsAlive) Died?.Invoke(info);
            return result;
        }

        protected virtual DamageResult Resolve(in DamageInfo info)
        {
            if (!IsAlive) return new DamageResult { outcome = DamageOutcome.Dead };

            float dealt = Mathf.Max(1f, CombatFormulas.Mitigate(info.amount, defense));
            Current = Mathf.Max(0f, Current - dealt);

            return new DamageResult
            {
                outcome = Current > 0f ? DamageOutcome.Applied : DamageOutcome.Dead,
                dealt = dealt
            };
        }

        public void Heal(float amount)
        {
            if (amount <= 0f || !IsAlive) return;
            Current = Mathf.Min(maxHealth, Current + amount);
            HealthChanged?.Invoke(Current, maxHealth);
        }

        public void ReviveFull()
        {
            Current = maxHealth;
            HealthChanged?.Invoke(Current, maxHealth);
        }

        public void RaiseHealthChanged() => HealthChanged?.Invoke(Current, maxHealth);
    }
}
