using ProjectInfinity.Player;
using UnityEngine;

namespace ProjectInfinity.Animation
{
    /// <summary>
    /// Procedural, code-driven animation for the primitive humanoid rig built by the scene builder.
    /// No external animation clips or rigs are used, which keeps the prototype 100% original and
    /// lets enemies and the player share one animator. Replace with Animator Controllers when
    /// Blender rigs land (phase 5 art pass).
    /// </summary>
    public class ProceduralAnimator : MonoBehaviour
    {
        [Header("Rig parts (assigned by the builder)")]
        public Transform torso;
        public Transform head;
        public Transform armLeft;
        public Transform armRight;
        public Transform legLeft;
        public Transform legRight;
        public Transform weapon;

        [Header("Optional drivers (assigned by the scene builder)")]
        public PlayerMotor motor;
        public UnityEngine.AI.NavMeshAgent agent;

        [Header("Tuning")]
        public float runCycleSpeed = 9f;
        public float swingAmplitude = 42f;
        public float idleSway = 2.5f;

        private Quaternion _torsoBase, _headBase, _armLBase, _armRBase, _legLBase, _legRBase, _weaponBase;
        private Vector3 _torsoBasePos;

        private float _phase;
        private float _speed;
        private float _attackTimer;
        private float _attackDuration = 0.3f;
        private float _heavyTimer;
        private float _hitTimer;
        private float _dodgeTimer;
        private float _castTimer;
        private bool _blocking;
        private bool _dead;

        private void Awake()
        {
            if (torso != null) { _torsoBase = torso.localRotation; _torsoBasePos = torso.localPosition; }
            if (head != null) _headBase = head.localRotation;
            if (armLeft != null) _armLBase = armLeft.localRotation;
            if (armRight != null) _armRBase = armRight.localRotation;
            if (legLeft != null) _legLBase = legLeft.localRotation;
            if (legRight != null) _legRBase = legRight.localRotation;
            if (weapon != null) _weaponBase = weapon.localRotation;
        }

        public void PlayAttack(int comboIndex)
        {
            _attackTimer = _attackDuration = 0.26f + 0.03f * comboIndex;
        }

        public void PlayHeavy()
        {
            _attackTimer = _attackDuration = 0.5f;
        }

        public void PlayDodge() => _dodgeTimer = 0.42f;
        public void PlayHit() => _hitTimer = 0.28f;
        public void PlayParry() => _hitTimer = 0.16f;
        public void PlayCast() => _castTimer = 0.42f;
        public void SetBlocking(bool value) => _blocking = value;

        public void PlayDeath()
        {
            _dead = true;
            if (torso != null) torso.localRotation = _torsoBase * Quaternion.Euler(80f, 0f, 0f);
        }

        private void Update()
        {
            if (_dead) return;

            float dt = Time.deltaTime;
            float targetSpeed = 0f;

            var motorDriver = motor != null ? motor : GetComponent<PlayerMotor>();
            if (motorDriver != null)
            {
                targetSpeed = motorDriver.CurrentSpeed;
            }
            else
            {
                var agentDriver = agent != null ? agent : GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agentDriver != null && agentDriver.velocity.sqrMagnitude > 0.01f) targetSpeed = 4f;
            }

            _speed = Mathf.Lerp(_speed, targetSpeed, 8f * dt);
            float locomotion = Mathf.Clamp01(_speed / 5f);

            if (locomotion > 0.05f) _phase += dt * runCycleSpeed * Mathf.Lerp(0.5f, 1.3f, locomotion);
            else _phase += dt * 2f;

            float swing = Mathf.Sin(_phase) * swingAmplitude * locomotion;
            float breath = Mathf.Sin(Time.time * 1.8f) * idleSway;
            float armSwing = swing * 0.75f;

            float attackAngle = 0f;
            if (_attackTimer > 0f)
            {
                _attackTimer -= dt;
                float p = 1f - Mathf.Clamp01(_attackTimer / _attackDuration);
                attackAngle = Mathf.Sin(p * Mathf.PI) * -135f;
            }

            float heavyAngle = 0f;
            if (_heavyTimer > 0f)
            {
                _heavyTimer -= dt;
                heavyAngle = Mathf.Sin((1f - Mathf.Clamp01(_heavyTimer / 0.5f)) * Mathf.PI) * -160f;
            }

            float flinch = 0f;
            if (_hitTimer > 0f)
            {
                _hitTimer -= dt;
                flinch = -14f * Mathf.Clamp01(_hitTimer / 0.28f);
            }

            float roll = 0f;
            if (_dodgeTimer > 0f)
            {
                _dodgeTimer -= dt;
                float p = 1f - Mathf.Clamp01(_dodgeTimer / 0.42f);
                roll = -360f * p;
            }

            float castAngle = 0f;
            if (_castTimer > 0f)
            {
                _castTimer -= dt;
                castAngle = -70f * Mathf.Clamp01(_castTimer / 0.42f);
            }

            if (torso != null)
            {
                torso.localRotation = _torsoBase * Quaternion.Euler(flinch, Mathf.Sin(_phase) * 3f * locomotion,
                                                                     Mathf.Sin(_phase * 2f) * 2f * locomotion);
                torso.localPosition = _torsoBasePos + Vector3.up * (Mathf.Abs(Mathf.Sin(_phase)) * 0.045f * locomotion);
            }

            if (head != null)
            {
                head.localRotation = _headBase * Quaternion.Euler(breath * 0.4f + Mathf.Sin(_phase * 2f) * 3f * locomotion, 0f, 0f);
            }

            if (legLeft != null)
                legLeft.localRotation = _legLBase * Quaternion.Euler(swing, 0f, 0f);

            if (legRight != null)
                legRight.localRotation = _legRBase * Quaternion.Euler(-swing, 0f, 0f);

            if (armLeft != null)
            {
                float blockLeft = _blocking ? -75f : 0f;
                armLeft.localRotation = _armLBase * Quaternion.Euler(-armSwing + blockLeft + castAngle, 0f, _blocking ? 18f : 0f);
            }

            if (armRight != null)
            {
                float blockRight = _blocking ? -75f : 0f;
                float swingArm = attackAngle + heavyAngle;
                armRight.localRotation = _armRBase * Quaternion.Euler(armSwing + blockRight + swingArm + castAngle, 0f,
                                                                      _blocking ? -18f : 0f);
            }

            if (weapon != null)
            {
                weapon.localRotation = _weaponBase * Quaternion.Euler(0f, 0f, 0f);
            }

            if (roll != 0f)
            {
                transform.localRotation = Quaternion.Euler(roll, transform.localEulerAngles.y, 0f);
            }
        }
    }
}
