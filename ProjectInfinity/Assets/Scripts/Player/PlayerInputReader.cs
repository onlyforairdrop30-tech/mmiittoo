using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectInfinity.Player
{
    /// <summary>
    /// Reads keyboard and mouse through the Input System package and pushes into
    /// <see cref="InputRouter"/>. Touch input is written by <c>TouchControls</c>, which uses the
    /// same router, so neither device has a private code path.
    /// </summary>
    public class PlayerInputReader : MonoBehaviour
    {
        [SerializeField] private InputRouter router;
        [SerializeField] private float mouseSensitivity = 0.14f;

        public void Bind(InputRouter target) => router = target;

        private void Update()
        {
            if (router == null) router = InputRouter.Instance;
            if (router == null) return;

            var keyboard = Keyboard.current;
            var mouse = Mouse.current;

            if (keyboard != null)
            {
                Vector2 move = Vector2.zero;
                if (keyboard.wKey.isPressed) move.y += 1f;
                if (keyboard.sKey.isPressed) move.y -= 1f;
                if (keyboard.dKey.isPressed) move.x += 1f;
                if (keyboard.aKey.isPressed) move.x -= 1f;
                router.KeyboardMove = move;

                router.KeyboardSprint = keyboard.leftShiftKey.isPressed;

                if (keyboard.spaceKey.wasPressedThisFrame) router.Dodge.Press();
                if (keyboard.eKey.wasPressedThisFrame) router.Heavy.Press();
                if (keyboard.qKey.wasPressedThisFrame) router.Ability1.Press();
                if (keyboard.rKey.wasPressedThisFrame) router.Ability2.Press();
                if (keyboard.tKey.wasPressedThisFrame) router.Ability3.Press();
                if (keyboard.fKey.wasPressedThisFrame) router.Ultimate.Press();
                if (keyboard.gKey.wasPressedThisFrame) router.Interact.Press();
                if (keyboard.tabKey.wasPressedThisFrame) router.ToggleInventory.Press();
                if (keyboard.escapeKey.wasPressedThisFrame)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
            }

            if (mouse != null)
            {
                router.KeyboardBlock = mouse.rightButton.isPressed;
                if (mouse.leftButton.wasPressedThisFrame) router.Light.Press();

                if (Cursor.lockState == CursorLockMode.Locked)
                {
                    router.AddLook(mouse.delta.ReadValue() * mouseSensitivity);
                }
                else if (mouse.leftButton.wasPressedThisFrame)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }
        }
    }
}
