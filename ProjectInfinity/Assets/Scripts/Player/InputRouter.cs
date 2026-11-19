using System;
using UnityEngine;

namespace ProjectInfinity.Player
{
    /// <summary>One-shot button that survives until its owner consumes it.</summary>
    [Serializable]
    public struct ButtonBuffer
    {
        [SerializeField] private bool _pressed;

        public void Press() => _pressed = true;
        public void Clear() => _pressed = false;

        public bool Consume()
        {
            if (!_pressed) return false;
            _pressed = false;
            return true;
        }

        public bool IsPressed => _pressed;
    }

    /// <summary>
    /// Single funnel for keyboard/mouse, touch and (later) gamepad. Gameplay code never reads
    /// devices directly, so mobile and PC share one code path.
    /// </summary>
    public class InputRouter : MonoBehaviour
    {
        public static InputRouter Instance { get; private set; }

        [Header("Continuous")]
        public Vector2 KeyboardMove;
        public Vector2 TouchMove;
        public bool KeyboardSprint;
        public bool TouchSprint;
        public bool KeyboardBlock;
        public bool TouchBlock;

        [Header("One-shot")]
        public ButtonBuffer Light;
        public ButtonBuffer Heavy;
        public ButtonBuffer Dodge;
        public ButtonBuffer Jump;
        public ButtonBuffer Ability1;
        public ButtonBuffer Ability2;
        public ButtonBuffer Ability3;
        public ButtonBuffer Ultimate;
        public ButtonBuffer Interact;
        public ButtonBuffer ToggleInventory;

        private Vector2 _look;

        public Vector2 Move => Vector2.ClampMagnitude(KeyboardMove + TouchMove, 1f);
        public bool Sprint => KeyboardSprint || TouchSprint;
        public bool Block => KeyboardBlock || TouchBlock;

        private void Awake() => Instance = this;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void AddLook(Vector2 delta) => _look += delta;

        public Vector2 ConsumeLook()
        {
            Vector2 value = _look;
            _look = Vector2.zero;
            return value;
        }

        public void ClearAll()
        {
            KeyboardMove = Vector2.zero;
            TouchMove = Vector2.zero;
            KeyboardSprint = TouchSprint = false;
            KeyboardBlock = TouchBlock = false;
            Light.Clear();
            Heavy.Clear();
            Dodge.Clear();
            Jump.Clear();
            Ability1.Clear();
            Ability2.Clear();
            Ability3.Clear();
            Ultimate.Clear();
            Interact.Clear();
            ToggleInventory.Clear();
            _look = Vector2.zero;
        }
    }
}
