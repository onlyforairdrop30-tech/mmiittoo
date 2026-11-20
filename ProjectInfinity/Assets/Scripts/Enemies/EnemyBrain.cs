using System;
using ProjectInfinity.Animation;
using ProjectInfinity.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace ProjectInfinity.Enemies
{
    /// <summary>
    /// Finite-state enemy AI: Idle → Chase → Windup → Attack → Recover, plus Stagger and Dead.
    /// Uses a <see cref="NavMeshAgent"/> when one is baked; otherwise falls back to direct steering
    /// so the prototype still runs if the NavMesh bake fails.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class EnemyBrain : MonoBehaviour
    {
        public enum BrainState { Idle, Chase, Windup, Recover, Stagger, Dead }

        [SerializeField] private EnemyDefinition definition;
        [SerializeField] private ProceduralAnimator animator;

        public EnemyDefinition Definition => definition;
        public Health Health { get; private set; }
        public Vector3 Home { get; private set; }

        /// <summary>Raised once when this enemy dies; the scene spawner and quest log listen.</summary>
        public event Action<EnemyBrain> Died;

        private NavMeshAgent _agent;
        private Health _target;
        private BrainState _state = BrainState.Idle;
        private float _timer;
        private float _cooldown;
        private float _strafePhase;

        private void Awake()
        {
            Health = GetComponent<Health>();
            _agent = GetComponent<NavMeshAgent>();
            Home = transform.position;
            Health.Died += _ => Die();
        }

        public void Configure(EnemyDefinition def, Health target, ProceduralAnimator rig = null)
        {
            if (Health == null) Health = GetComponent<Health>();
            if (_agent == null) _agent = GetComponent<NavMeshAgent>();

            definition = def;
            _target = target;
            if (rig != null) animator = rig;

            Home = transform.position;
            if (def != null)
            {
                if (Health != null) Health.Configure(def.maxHealth, def.defense, Faction.Hostile);
                if (_agent != null)
                {
                    _agent.speed = def.moveSpeed;
                    _agent.acceleration = 14f;
                    _agent.angularSpeed = 540f;
                    _agent.stoppingDistance = Mathf.Max(0.4f, def.attackRange - 0.5f);
                    _agent.updateRotation = true;
                }
            }
        }

        private void Update()
        {
            if (_state == BrainState.Dead || definition == null) return;

            float dt = Time.deltaTime;
            if (_cooldown > 0f) _cooldown -= dt;

            if (_target == null || !_target.IsAlive)
            {
                _state = BrainState.Idle;
                StopMoving();
                return;
            }

            float distance = Vector3.Distance(transform.position, _target.transform.position);
            bool tooFarFromHome = Vector3.Distance(transform.position, Home) > definition.leashRange;

            switch (_state)
            {
                case BrainState.Idle:
                    if (distance <= definition.aggroRange && !tooFarFromHome) _state = BrainState.Chase;
                    else ReturnHome(dt);
                    break;

                case BrainState.Chase:
                    if (tooFarFromHome)
                    {
                        _state = BrainState.Idle;
                        break;
                    }
                    if (distance <= definition.attackRange && _cooldown <= 0f)
                    {
                        _state = BrainState.Windup;
                        _timer = definition.windup;
                        if (animator != null) animator.PlayAttack(1);
                        StopMoving();
                    }
                    else
                    {
                        Approach(dt, distance);
                    }
                    break;

                case BrainState.Windup:
                    _timer -= dt;
                    FaceTarget(dt, 8f);
                    if (_timer <= 0f)
                    {
                        Strike();
                        _state = BrainState.Recover;
                        _timer = 0.35f;
                        _cooldown = definition.attackCooldown;
                    }
                    break;

                case BrainState.Recover:
                    _timer -= dt;
                    if (_timer <= 0f) _state = BrainState.Chase;
                    break;

                case BrainState.Stagger:
                    _timer -= dt;
                    if (_timer <= 0f) _state = BrainState.Chase;
                    break;
            }
        }

        private void Approach(float dt, float distance)
        {
            Vector3 toTarget = _target.transform.position - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.001f) return;

            _strafePhase += dt * 1.3f;

            Vector3 approach = toTarget.normalized;
            if (definition.archetype == EnemyArchetype.Ranged && distance < 6f)
            {
                approach = -approach; // kiting
            }
            Vector3 strafe = Vector3.Cross(Vector3.up, approach) * Mathf.Sin(_strafePhase) * definition.strafeBias;
            Vector3 destination = transform.position + (approach + strafe).normalized * 2f;

            if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
            {
                _agent.isStopped = false;
                _agent.SetDestination(destination);
            }
            else
            {
                transform.position += (approach + strafe).normalized * (definition.moveSpeed * dt);
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(approach), 8f * dt);
            }
        }

        private void FaceTarget(float dt, float sharpness)
        {
            if (_target == null) return;
            Vector3 dir = _target.transform.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), sharpness * dt);
        }

        private void Strike()
        {
            if (_target == null || !_target.IsAlive) return;
            if (Vector3.Distance(transform.position, _target.transform.position) > definition.attackRange + 0.8f) return;

            float amount = CombatFormulas.ComputeDamage(definition.attack, 1f, _target.Defense, 1.5f, false);
            var info = DamageInfo.Melee(amount, Faction.Hostile, definition.displayName,
                                        _target.transform.position, false);
            _target.ApplyDamage(in info);
        }

        private void ReturnHome(float dt)
        {
            if (Vector3.Distance(transform.position, Home) < 0.6f)
            {
                StopMoving();
                return;
            }

            if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
            {
                _agent.isStopped = false;
                _agent.SetDestination(Home);
            }
            else
            {
                Vector3 dir = Home - transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.01f)
                {
                    transform.position += dir.normalized * (definition.moveSpeed * 0.6f * dt);
                    transform.rotation = Quaternion.Slerp(transform.rotation,
                        Quaternion.LookRotation(dir), 6f * dt);
                }
            }
        }

        private void StopMoving()
        {
            if (_agent != null && _agent.enabled && _agent.isOnNavMesh) _agent.isStopped = true;
        }

        /// <summary>Called by parry/counter effects.</summary>
        public void Stagger(float seconds)
        {
            if (_state == BrainState.Dead) return;
            _state = BrainState.Stagger;
            _timer = seconds;
            StopMoving();
            if (animator != null) animator.PlayHit();
        }

        private void Die()
        {
            if (_state == BrainState.Dead) return;
            _state = BrainState.Dead;
            StopMoving();
            if (_agent != null) _agent.enabled = false;
            if (animator != null) animator.PlayDeath();
            Died?.Invoke(this);

            var collider = GetComponent<Collider>();
            if (collider != null) collider.enabled = false;
            Destroy(gameObject, 2.5f);
        }
    }
}
