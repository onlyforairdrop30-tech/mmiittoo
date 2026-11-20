using System;
using ProjectInfinity.Animation;
using ProjectInfinity.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace ProjectInfinity.Enemies
{
    /// <summary>
    /// The Rootwarden: a three-phase boss with telegraphed area attacks.
    /// Phase changes are driven by health fraction and alter telegraph time, cooldown, radius and
    /// movement speed, so the fight has readable escalation rather than a stat wall.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class BossBrain : MonoBehaviour
    {
        private enum BossState { Idle, Chase, Telegraph, Recover }

        [SerializeField] private EnemyDefinition definition;
        [SerializeField] private string title = "The Rootwarden";
        [SerializeField] private Transform telegraphRing;
        [SerializeField] private ProceduralAnimator animator;

        public Health Health { get; private set; }
        public EnemyDefinition Definition => definition;
        public int Phase { get; private set; } = 1;
        public string Title => title;
        public event Action<int> PhaseChanged;
        public event Action<BossBrain> Defeated;

        private Health _target;
        private NavMeshAgent _agent;
        private BossState _state = BossState.Idle;
        private float _timer;
        private float _cooldown = 1.5f;
        private bool _resolved;
        private float _attackRadius = 2.6f;
        private float _secondWave;

        private void Awake()
        {
            Health = GetComponent<Health>();
            _agent = GetComponent<NavMeshAgent>();
            Health.Died += _ => OnDefeated();
        }

        public void Configure(EnemyDefinition def, Health target, ProceduralAnimator rig, Transform ring)
        {
            if (Health == null) Health = GetComponent<Health>();
            if (_agent == null) _agent = GetComponent<NavMeshAgent>();

            definition = def;
            _target = target;
            if (rig != null) animator = rig;
            if (ring != null) telegraphRing = ring;

            if (def != null)
            {
                if (Health != null) Health.Configure(def.maxHealth, def.defense, Faction.Hostile);
                if (_agent != null)
                {
                    _agent.speed = def.moveSpeed;
                    _agent.stoppingDistance = 2.4f;
                }
            }

            if (telegraphRing != null) telegraphRing.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (definition == null || Health == null || !Health.IsAlive) return;

            float dt = Time.deltaTime;
            UpdatePhase();

            if (_target == null || !_target.IsAlive)
            {
                SetRingEnabled(false);
                return;
            }

            if (_cooldown > 0f) _cooldown -= dt;

            float distance = Vector3.Distance(transform.position, _target.transform.position);

            switch (_state)
            {
                case BossState.Idle:
                    _state = BossState.Chase;
                    break;

                case BossState.Chase:
                    FaceTarget(dt, 4f);
                    if (_cooldown <= 0f && distance <= _attackRadius + 2.4f)
                    {
                        BeginTelegraph();
                    }
                    else if (distance > 2.8f)
                    {
                        MoveTowards(_target.transform.position);
                    }
                    else
                    {
                        Halt();
                    }
                    break;

                case BossState.Telegraph:
                    _timer -= dt;
                    PulseRing();
                    if (!_resolved && _timer <= TelegraphedDuration() * 0.35f)
                    {
                        ResolveSlam();
                    }
                    if (_timer <= 0f)
                    {
                        _state = BossState.Recover;
                        _timer = 0.9f;
                        _cooldown = CooldownForPhase();
                        SetRingEnabled(false);
                        if (_secondWave > 0f)
                        {
                            _secondWave = 0.45f;
                        }
                    }
                    break;

                case BossState.Recover:
                    _timer -= dt;
                    if (_secondWave > 0f)
                    {
                        _secondWave -= dt;
                        if (_secondWave <= 0f) Shockwave();
                    }
                    if (_timer <= 0f) _state = BossState.Chase;
                    break;
            }
        }

        private void UpdatePhase()
        {
            float fraction = Health.Current / Mathf.Max(1f, Health.Max);
            int next = fraction > 0.66f ? 1 : fraction > 0.33f ? 2 : 3;
            if (next == Phase) return;

            Phase = next;
            _attackRadius = 2.6f + (Phase - 1) * 1.1f;
            if (_agent != null) _agent.speed = definition.moveSpeed * (1f + (Phase - 1) * 0.18f);
            PhaseChanged?.Invoke(Phase);
        }

        private float TelegraphedDuration() => Mathf.Max(0.45f, 1.15f - (Phase - 1) * 0.18f);
        private float CooldownForPhase() => Mathf.Max(1.4f, 3.2f - (Phase - 1) * 0.6f);

        private void BeginTelegraph()
        {
            _state = BossState.Telegraph;
            _timer = TelegraphedDuration();
            _resolved = false;
            _secondWave = Phase >= 2 ? -1f : 0f;
            Halt();
            SetRingEnabled(true);
            if (telegraphRing != null)
            {
                telegraphRing.localScale = new Vector3(_attackRadius * 2f, 0.05f, _attackRadius * 2f);
            }
            if (animator != null) animator.PlayHeavy();
        }

        private void PulseRing()
        {
            if (telegraphRing == null) return;
            float t = 1f - Mathf.Clamp01(_timer / TelegraphedDuration());
            float scale = _attackRadius * 2f * Mathf.Lerp(1.25f, 1f, t);
            telegraphRing.localScale = new Vector3(scale, 0.05f, scale);
        }

        private void ResolveSlam()
        {
            _resolved = true;
            ApplyAreaDamage(_attackRadius, 1.35f);
        }

        private void Shockwave()
        {
            _attackRadius += 1.6f;
            ApplyAreaDamage(_attackRadius, 0.8f);
            if (telegraphRing != null)
            {
                telegraphRing.localScale = new Vector3(_attackRadius * 2f, 0.05f, _attackRadius * 2f);
            }
        }

        private void ApplyAreaDamage(float radius, float power)
        {
            if (_target == null || !_target.IsAlive) return;
            float distance = Vector3.Distance(transform.position, _target.transform.position);
            if (distance > radius + 0.6f) return;

            float amount = CombatFormulas.ComputeDamage(definition.attack, power, _target.Defense, 1.5f, false);
            var info = DamageInfo.Melee(amount, Faction.Hostile, title, _target.transform.position);
            _target.ApplyDamage(in info);
        }

        private void SetRingEnabled(bool value)
        {
            if (telegraphRing != null) telegraphRing.gameObject.SetActive(value);
        }

        private void MoveTowards(Vector3 destination)
        {
            if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
            {
                _agent.isStopped = false;
                _agent.SetDestination(destination);
            }
            else
            {
                Vector3 dir = destination - transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.01f)
                    transform.position += dir.normalized * (definition.moveSpeed * Time.deltaTime);
            }
        }

        private void Halt()
        {
            if (_agent != null && _agent.enabled && _agent.isOnNavMesh) _agent.isStopped = true;
        }

        private void FaceTarget(float dt, float sharpness)
        {
            if (_target == null) return;
            Vector3 dir = _target.transform.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), sharpness * dt);
        }

        private void OnDefeated()
        {
            SetRingEnabled(false);
            if (_agent != null) _agent.enabled = false;
            if (animator != null) animator.PlayDeath();
            Defeated?.Invoke(this);
        }
    }
}
