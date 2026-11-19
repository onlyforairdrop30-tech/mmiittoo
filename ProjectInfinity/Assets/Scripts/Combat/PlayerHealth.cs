using UnityEngine;

namespace ProjectInfinity.Combat
{
    /// <summary>
    /// Intercepts incoming damage with the guard states owned by <see cref="PlayerCombat"/>:
    /// dodge invulnerability, parry window, then block.
    /// </summary>
    [RequireComponent(typeof(PlayerCombat))]
    public class PlayerHealth : Health
    {
        [SerializeField] private PlayerCombat combat;

        protected override void Awake()
        {
            base.Awake();
            if (combat == null) combat = GetComponent<PlayerCombat>();
        }

        public void Bind(PlayerCombat owner) => combat = owner;

        protected override DamageResult Resolve(in DamageInfo info)
        {
            if (!IsAlive) return new DamageResult { outcome = DamageOutcome.Dead };

            if (combat != null)
            {
                if (combat.IsInvulnerable)
                    return new DamageResult { outcome = DamageOutcome.Invulnerable, dealt = 0f };

                if (combat.ConsumeParryWindow())
                    return new DamageResult { outcome = DamageOutcome.Parried, dealt = 0f };

                if (combat.IsBlocking)
                {
                    float cost = CombatFormulas.BlockStaminaCost(info.amount);
                    if (combat.TrySpendStamina(cost))
                    {
                        float chip = Mathf.Max(0f, CombatFormulas.Mitigate(info.amount, defense) * 0.22f);
                        Current = Mathf.Max(0f, Current - chip);
                        return new DamageResult
                        {
                            outcome = Current > 0f ? DamageOutcome.Blocked : DamageOutcome.Dead,
                            dealt = chip
                        };
                    }
                    combat.BreakGuard();
                }
            }

            return base.Resolve(info);
        }
    }
}
