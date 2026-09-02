using UnityEngine;
using UnityEngine.InputSystem;

namespace OfflineExtraction.UI
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class HubPlayerController : MonoBehaviour
    {
        public float speed = 3.2f;
        public float mouseSensitivity = .12f;
        private CharacterController controller;
        private Camera view;
        private HubInteraction interaction;
        private float pitch;
        private float seatedLookYaw;
        private bool wasSeated;

        public float LookPitch => pitch;
        public float SeatedLookYaw => seatedLookYaw;
        private float verticalVelocity;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            view = GetComponentInChildren<Camera>();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            LobbyPrototype panel = FindFirstObjectByType<LobbyPrototype>();
            if ((panel != null && panel.IsVisible) || HubWorkbenchUI.IsOpen) return;
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (keyboard == null || view == null) return;
            if (controller != null && controller.enabled)
            {
                Vector2 input = Vector2.zero;
                if (keyboard.wKey.isPressed) input.y++;
                if (keyboard.sKey.isPressed) input.y--;
                if (keyboard.dKey.isPressed) input.x++;
                if (keyboard.aKey.isPressed) input.x--;
                input = Vector2.ClampMagnitude(input, 1f);
                if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
                verticalVelocity += -22f * Time.deltaTime;
                Vector3 motion = (transform.forward * input.y + transform.right * input.x) * speed;
                motion.y = verticalVelocity;
                controller.Move(motion * Time.deltaTime);
            }
            if (mouse == null || Cursor.lockState != CursorLockMode.Locked) return;
            Vector2 look = mouse.delta.ReadValue() * mouseSensitivity;
            pitch = Mathf.Clamp(pitch - look.y, -82f, 82f);
            interaction ??= GetComponent<HubInteraction>();
            bool isSeated = interaction != null && interaction.IsSeated;
            if (isSeated)
            {
                seatedLookYaw = Mathf.Clamp(seatedLookYaw + look.x, -105f, 105f);
                view.transform.localRotation = Quaternion.Euler(pitch, seatedLookYaw, 0f);
            }
            else
            {
                if (wasSeated) seatedLookYaw = 0f;
                transform.Rotate(0f, look.x, 0f);
                view.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }
            wasSeated = isSeated;
        }
    }
}
