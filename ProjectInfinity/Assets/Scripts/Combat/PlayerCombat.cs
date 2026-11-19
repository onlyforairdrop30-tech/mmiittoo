using System;
using System.Collections.Generic;
using ProjectInfinity.Animation;
using ProjectInfinity.Core;
using ProjectInfinity.Player;
using ProjectInfinity.Progression;
using UnityEngine;

namespace ProjectInfinity.Combat
{
    /// <summary>
    /// Player combat: three-hit light combo, heavy attack, dodge with i-frames, block with a parry
    /// window, three abilities and an ultimate.
    /// All damage here is <em>prediction</em>; the authoritative server recomputes the same numbers
    /// from the same content table (see docs/ARCHITECTURE.md).
    /// </summary>
    public class PlayerCombat : MonoBehaviour
    {
        public enum ActionState { Idle, Windup, Recovery, Dodging }

        [Serializable]
        public class AbilitySlot
        {
            public string abilityName = "Ability";
            public string keyLabel = "Q";
            [Tooltip("Damage multiplier applied to Attack.")]
            public float power = 2.6f;
            public float radius = 3.6f;
            public float reach = 3.2f;
            public float cooldown = 6f;
            public float energyCost = 20f;
            [NonSerialized] public float cooldownRemaining;

            public float CooldownFraction => cooldown <= 0f ? 0f : Mathf.Clamp01(cooldownRemaining / cooldown);
        }

        [Header("References")]
        public PlayerProgression progression;
        public PlayerMotor motor;
        public PlayerHealth health;
        public ProceduralAnimator animator;
        public InputRouter input;

        [Header("Basic attacks (power = multiplier on Attack)")]
        public float lightPower = 1.5f;
        public float comboPowerStep = 0.22f;
        public float heavyPower = 3.2f;
        public float lightReach = 2.1f;
        public float lightRadius = 1.1f;
        public float heavyReach = 2.4f;
        public float heavyRadius = 1.6f;

        [Header("Abilities")]
        public AbilitySlot[] abilities = new AbilitySlot[3];
        public float ultimatePower = 8f;
        public float ultimateRadius = 8f;
        public float ultimateCost = 100f;

        public float Stamina { get; private set; }
        public float Energy { get; private set; }
        public bool IsBlocking { get; private set; }
        public int ComboIndex => _comboIndex;
        public int Kills { get; private set; }

        public bool IsInvulnerable => _invulnerableTimer > 0f;
        public float MaxStamina => progression != null ? progression.Stats[StatType.MaxStamina] : 100f;
        public float MaxEnergy => progression != null ? progression.Stats[StatType.MaxEnergy] : 100f;

        public event Action<string, float, bool> HitConfirmed;   // targetName, amount, critical
        public event Action<string> AbilityUsed;
        public event Action GuardBroken;

        private readonly List<IDamageable> _hits = new List<IDamageable>(16);
        private ActionState _action = ActionState.Idle;
        private float _actionTimer;
        private float _parryTimer;
        private float _invulnerableTimer;
        private float _dodgeCooldown;
        private float _comboResetTimer;
        private int _comboIndex;
        private bool _isHeavy;

        private void Awake()
        {
            Stamina = MaxStamina;
            Energy = 0f;
        }

        private void Start()
        {
            if (input == null) input = InputRouter.Instance;
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            if (health != null && !health.IsAlive) return;

            if (input == null) input = InputRouter.Instance;

            Regen(dt);
            TickTimers(dt);
            if (input != null) ReadInput();
            TickAction(dt);
        }

        private void Regen(float dt)
        {
            Stamina = Mathf.Min(MaxStamina, Stamina + GameRules.StaminaRegenPerSecond * dt);
            Energy = Mathf.Min(MaxEnergy, Energy + GameRules.EnergyRegenPerSecond * dt);
        }

        private void TickTimers(float dt)
        {
            if (_invulnerableTimer > 0f) _invulnerableTimer -= dt;
            if (_parryTimer > 0f) _parryTimer -= dt;
            if (_dodgeCooldown > 0f) _dodgeCooldown -= dt;

            if (_comboResetTimer > 0f)
            {
                _comboResetTimer -= dt;
                if (_comboResetTimer <= 0f) _comboIndex = 0;
            }

            if (abilities != null)
            {
                foreach (var ability in abilities)
                {
                    if (ability != null && ability.cooldownRemaining > 0f) ability.cooldownRemaining -= dt;
                }
            }
        }

        private void ReadInput()
        {
            if (input.Dodge.Consume()) TryDodge();
            if (input.Light.Consume()) TryLight();
            if (input.Heavy.Consume()) TryHeavy();
            if (input.Ability1.Consume()) TryAbility(0);
            if (input.Ability2.Consume()) TryAbility(1);
            if (input.Ability3.Consume()) TryAbility(2);
            if (input.Ultimate.Consume()) TryUltimate();

            bool wantsBlock = input.BlockHeld && _action == ActionState.Idle;
            if (wantsBlock && !IsBlocking) BeginBlock();
            else if (!wantsBlock && IsBlocking) IsBlocking = false;
        }

        // ---------- basic attacks ----------

        public void TryLight()
        {
            if (_action == ActionState.Dodging || _action == ActionState.Windup) return;

            if (_action == ActionState.Recovery && !_isHeavy && _comboIndex < 3)
            {
                StartLight(_comboIndex + 1);
                return;
            }

            if (_action == ActionState.Idle) StartLight(1);
        }

        private void StartLight(int index)
        {
            _comboIndex = Mathf.Clamp(index, 1, 3);
            _isHeavy = false;
            _action = ActionState.Windup;
            _actionTimer = 0.13f + 0.02f * _comboIndex;
            _comboResetTimer = GameRules.ComboWindow + 0.6f;

            IsBlocking = false;
            if (motor != null) motor.SpeedMultiplier = 0.3f;
            if (animator != null) animator.PlayAttack(_comboIndex - 1);
        }

        public void TryHeavy()
        {
            if (_action != ActionState.Idle) return;
            _isHeavy = true;
            _comboIndex = 0;
            _action = ActionState.Windup;
            _actionTimer = 0.34f;

            IsBlocking = false;
            if (motor != null) motor.SpeedMultiplier = 0.15f;
            if (animator != null) animator.PlayHeavy();
        }

        private void TickAction(float dt)
        {
            if (_action == ActionState.Idle) return;

            _actionTimer -= dt;
            if (_actionTimer > 0f) return;

            switch (_action)
            {
                case ActionState.Windup:
                    ResolveSwing(_isHeavy, _comboIndex);
                    _action = ActionState.Recovery;
                    _actionTimer = _isHeavy ? 0.40f : 0.22f;
                    break;

                case ActionState.Recovery:
                case ActionState.Dodging:
                    _action = ActionState.Idle;
                    if (motor != null) motor.SpeedMultiplier = 1f;
                    break;
            }
        }

        private void ResolveSwing(bool heavy, int combo)
        {
            if (progression == null) return;

            float power = heavy
                ? heavyPower
                : lightPower * (1f + comboPowerStep * Mathf.Max(0, combo - 1));

            float reach = heavy ? heavyReach : lightReach;
            float radius = heavy ? heavyRadius : lightRadius;

            DealAreaDamage(transform.position + Vector3.up * 1.1f + transform.forward * (reach * 0.55f),
                           radius + reach * 0.45f, power, heavy ? "Heavy" : "Slash");
        }

        // ---------- guard ----------

        private void BeginBlock()
        {
            IsBlocking = true;
            _parryTimer = GameRules.ParryWindow;
            if (animator != null) animator.SetBlocking(true);
        }

        /// <summary>True once per parry window: the incoming hit is negated and the attacker staggers.</summary>
        public bool ConsumeParryWindow()
        {
            if (_parryTimer <= 0f) return false;
            _parryTimer = 0f;
            Stamina = Mathf.Min(MaxStamina, Stamina + 10f);
            Energy = Mathf.Min(MaxEnergy, Energy + 12f);
            if (animator != null) animator.PlayParry();
            return true;
        }

        public bool TrySpendStamina(float cost)
        {
            if (Stamina < cost) return false;
            Stamina -= cost;
            return true;
        }

        public void BreakGuard()
        {
            IsBlocking = false;
            Stamina = 0f;
            _action = ActionState.Recovery;
            _actionTimer = 0.6f;
            if (animator != null) animator.PlayHit();
            GuardBroken?.Invoke();
        }

        // ---------- dodge ----------

        public void TryDodge()
        {
            if (_dodgeCooldown > 0f || _action == ActionState.Dodging) return;
            if (!TrySpendStamina(GameRules.DodgeStaminaCost)) return;

            _dodgeCooldown = GameRules.DodgeCooldown;
            _invulnerableTimer = GameRules.DodgeInvulnerability;
            _action = ActionState.Dodging;
            _actionTimer = GameRules.DodgeDuration;
            _parryTimer = 0f;
            IsBlocking = false;

            Vector3 dir = motor != null ? motor.LastMoveDirection : transform.forward;
            if (dir.sqrMagnitude < 0.01f) dir = -transform.forward;

            if (motor != null)
            {
                motor.ApplyImpulse(dir.normalized * 9f);
                motor.SpeedMultiplier = 1f;
            }
            if (animator != null) animator.PlayDodge();
        }

        // ---------- abilities ----------

        public void TryAbility(int index)
        {
            if (abilities == null || index < 0 || index >= abilities.Length) return;
            var ability = abilities[index];
            if (ability == null || ability.cooldownRemaining > 0f) return;
            if (_action == ActionState.Dodging) return;
            if (Energy < ability.energyCost) return;

            Energy -= ability.energyCost;
            ability.cooldownRemaining = ability.cooldown;

            Vector3 center = transform.position + Vector3.up * 1f + transform.forward * ability.reach;
            DealAreaDamage(center, ability.radius, ability.power, ability.abilityName);

            if (animator != null) animator.PlayCast();
            AbilityUsed?.Invoke(ability.abilityName);
        }

        public void TryUltimate()
        {
            if (Energy < ultimateCost) return;

            Energy = 0f;
            DealAreaDamage(transform.position + Vector3.up * 1f, ultimateRadius, ultimatePower, "Awakening Burst");
            if (animator != null) animator.PlayCast();
            AbilityUsed?.Invoke("Awakening Burst");
        }

        private void DealAreaDamage(Vector3 center, float radius, float power, string label)
        {
            if (progression == null) return;

            float attack = progression.Stats[StatType.Attack];
            float critChance = progression.Stats[StatType.CritChance];
            float critMultiplier = progression.Stats[StatType.CritDamage];
            float skillPower = Mathf.Max(0.1f, progression.Stats[StatType.SkillPower]);

            HitQuery.Nearby(center, radius, Faction.Hostile, _hits);

            for (int i = 0; i < _hits.Count; i++)
            {
                var target = _hits[i];
                var targetHealth = target as Health;
                float defense = targetHealth != null ? targetHealth.Defense : 0f;

                bool critical = CombatFormulas.RollCritical(critChance, UnityEngine.Random.value);
                float amount = CombatFormulas.ComputeDamage(attack * skillPower, power, defense,
                                                            critMultiplier, critical);

                var info = DamageInfo.Melee(amount, Faction.Player, label, center, critical);
                target.ApplyDamage(in info);

                Energy = Mathf.Min(MaxEnergy, Energy + 2.5f);
                HitConfirmed?.Invoke(targetHealth != null ? targetHealth.name : "Target", amount, critical);

                if (targetHealth != null && !targetHealth.IsAlive) Kills++;
            }
        }

        // ---------- helper for the HUD ----------

        public float AbilityCooldownFraction(int index)
        {
            if (abilities == null || index < 0 || index >= abilities.Length || abilities[index] == null) return 0f;
            return abilities[index].CooldownFraction;
        }

        public string AbilityLabel(int index)
        {
            if (abilities == null || index < 0 || index >= abilities.Length || abilities[index] == null) return "-";
            return abilities[index].abilityName;
        }
    }
}
