using UnityEngine;

namespace ProjectInfinity.Player
{
    /// <summary>
    /// Orbit camera driven by <see cref="InputRouter"/>'s look delta, with sphere-cast collision so
    /// it never clips through terrain. Cinemachine is available in the project for cutscenes/follow
    /// rigs; gameplay uses this rig because it is deterministic and cheap on mobile.
    /// </summary>
    public class ThirdPersonCamera : MonoBehaviour
    {
        public Transform target;
        [SerializeField] private InputRouter input;

        public float distance = 5.4f;
        public float height = 1.65f;
        public float minPitch = -18f;
        public float maxPitch = 62f;
        public float sensitivity = 1f;
        public float followSharpness = 14f;
        public float collisionRadius = 0.3f;
        public LayerMask collisionMask = ~0;

        private float _yaw;
        private float _pitch = 16f;

        /// <summary>Yaw in degrees; the motor uses it for camera-relative movement.</summary>
        public float Yaw => _yaw;

        public void Bind(Transform follow, InputRouter router)
        {
            target = follow;
            input = router;
        }

        public void SetOrbit(float yawDegrees, float pitchDegrees)
        {
            _yaw = yawDegrees;
            _pitch = Mathf.Clamp(pitchDegrees, minPitch, maxPitch);
        }

        private void Start()
        {
            if (input == null) input = InputRouter.Instance;
            if (target != null) _yaw = target.eulerAngles.y;
        }

        private void LateUpdate()
        {
            if (input == null) input = InputRouter.Instance;
            if (input != null)
            {
                Vector2 look = input.ConsumeLook() * sensitivity;
                _yaw += look.x;
                _pitch = Mathf.Clamp(_pitch - look.y, minPitch, maxPitch);
            }

            if (target == null) return;

            Vector3 pivot = target.position + Vector3.up * height;
            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 desired = pivot - rotation * Vector3.forward * distance;

            Vector3 offset = desired - pivot;
            float length = offset.magnitude;
            if (length > 0.01f &&
                Physics.SphereCast(pivot, collisionRadius, offset / length, out RaycastHit hit,
                                   length, collisionMask, QueryTriggerInteraction.Ignore))
            {
                desired = pivot + offset / length * Mathf.Max(0.65f, hit.distance - 0.08f);
            }

            float t = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, desired, t);
            transform.rotation = Quaternion.LookRotation(pivot - transform.position, Vector3.up);
        }
    }
}
