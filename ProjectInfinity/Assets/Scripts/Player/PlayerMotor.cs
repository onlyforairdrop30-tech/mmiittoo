using UnityEngine;

namespace ProjectInfinity.Player
{
    /// <summary>
    /// Camera-relative third-person movement on a <see cref="CharacterController"/>:
    /// walk / run / sprint, jump, gravity, dodge impulses and a speed scale used by combat.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMotor : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private InputRouter input;
        [SerializeField] private ThirdPersonCamera cameraRig;

        [Header("Speeds")]
        public float walkSpeed = 2.6f;
        public float runSpeed = 5.2f;
        public float sprintSpeed = 7.4f;
        public float rotationSpeed = 14f;
        public float acceleration = 22f;

        [Header("Air")]
        public float jumpHeight = 1.15f;
        public float gravity = -24f;

        /// <summary>Set by combat: 1 = free movement, 0.2 = attack shuffle, 0 = rooted.</summary>
        [System.NonSerialized] public float SpeedMultiplier = 1f;

        /// <summary>When true all movement intent is ignored (cinematics, UI, death).</summary>
        public bool ControlLocked;

        public Vector3 LastMoveDirection { get; private set; } = Vector3.forward;
        public float CurrentSpeed { get; private set; }
        public bool IsGrounded { get; private set; }

        private CharacterController _controller;
        private Vector3 _impulse;
        private float _verticalVelocity;
        private Vector3 _planarVelocity;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
        }

        public void Bind(InputRouter router, ThirdPersonCamera rig)
        {
            input = router;
            cameraRig = rig;
        }

        private void Update()
        {
            if (input == null) input = InputRouter.Instance;

            float dt = Time.deltaTime;
            IsGrounded = _controller.isGrounded;

            Vector2 moveInput = (input != null && !ControlLocked) ? input.Move : Vector2.zero;
            bool sprinting = input != null && input.Sprint && moveInput.magnitude > 0.1f;

            Vector3 desired = CameraRelative(moveInput) * (sprinting ? sprintSpeed : runSpeed) * SpeedMultiplier;

            if (moveInput.magnitude < 0.05f)
            {
                _planarVelocity = Vector3.MoveTowards(_planarVelocity, Vector3.zero, acceleration * dt);
            }
            else
            {
                _planarVelocity = Vector3.MoveTowards(_planarVelocity, desired, acceleration * dt);
                LastMoveDirection = _planarVelocity.sqrMagnitude > 0.01f
                    ? _planarVelocity.normalized
                    : desired.normalized;

                Vector3 flat = new Vector3(LastMoveDirection.x, 0f, LastMoveDirection.z);
                if (flat.sqrMagnitude > 0.001f)
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation,
                        Quaternion.LookRotation(flat), rotationSpeed * dt);
                }
            }

            if (IsGrounded && _verticalVelocity < 0f) _verticalVelocity = -2f;

            if (input != null && !ControlLocked && input.Jump.Consume() && IsGrounded)
            {
                _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }

            _verticalVelocity += gravity * dt;

            Vector3 motion = _planarVelocity + _impulse + Vector3.up * _verticalVelocity;
            _controller.Move(motion * dt);

            _impulse = Vector3.MoveTowards(_impulse, Vector3.zero, 26f * dt);
            CurrentSpeed = new Vector3(_planarVelocity.x, 0f, _planarVelocity.z).magnitude;
        }

        private Vector3 CameraRelative(Vector2 move)
        {
            if (move.sqrMagnitude < 0.0001f) return Vector3.zero;

            float yaw = cameraRig != null ? cameraRig.Yaw : transform.eulerAngles.y;
            Quaternion flat = Quaternion.Euler(0f, yaw, 0f);
            Vector3 forward = flat * Vector3.forward;
            Vector3 right = flat * Vector3.right;

            return Vector3.ClampMagnitude(forward * move.y + right * move.x, 1f);
        }

        public void ApplyImpulse(Vector3 velocity) => _impulse += velocity;

        public void Teleport(Vector3 position)
        {
            _controller.enabled = false;
            transform.position = position;
            _controller.enabled = true;
            _planarVelocity = Vector3.zero;
            _impulse = Vector3.zero;
            _verticalVelocity = 0f;
        }
    }
}
