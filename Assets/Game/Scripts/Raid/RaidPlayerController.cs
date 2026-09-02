using UnityEngine;
using UnityEngine.InputSystem;
using OfflineExtraction.Core;

namespace OfflineExtraction.Raid
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class RaidPlayerController : MonoBehaviour
    {
        public float walkSpeed = 3.6f;
        public float sprintSpeed = 6.2f;
        public float crouchSpeed = 2.1f;
        public float proneSpeed = .9f;
        public float leanAngle = 30f;
        public float leanOffset = .28f;
        public float mouseSensitivity = .12f;
        public float gravity = -24f;
        public float jumpHeight = 1.15f;

        private CharacterController controller;
        private Camera viewCamera;
        private float pitch;
        private float verticalVelocity;
        private float standingHeight;
        private float standingRadius;
        private float standingCameraY;
        private float standingCameraX;
        private float currentLean;
        private Stance stance;
        public bool IsStealth { get; private set; }
        public bool IsAiming { get; set; }
        public bool IsHoldingBreath { get; private set; }
        public float SprintStamina { get; private set; } = 100f;
        public float BreathStamina { get; private set; } = 100f;
        public float NoiseRadius { get; private set; }

        private enum Stance { Standing, Crouched, Prone }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            viewCamera = GetComponentInChildren<Camera>();
            standingHeight = controller.height;
            standingRadius = controller.radius;
            standingCameraY = viewCamera != null ? viewCamera.transform.localPosition.y : standingHeight * .85f;
            standingCameraX = viewCamera != null ? viewCamera.transform.localPosition.x : 0f;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            if (RaidInventoryUI.IsOpen || RaidBootstrap.IsPaused || RaidBootstrap.IsDeploymentLocked) { IsStealth = false; IsHoldingBreath = false; return; }
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (keyboard == null) return;

            if (keyboard.cKey.wasPressedThisFrame) stance = stance == Stance.Crouched ? Stance.Standing : Stance.Crouched;
            if (keyboard.zKey.wasPressedThisFrame) stance = stance == Stance.Prone ? Stance.Standing : Stance.Prone;
            bool crouched = stance == Stance.Crouched;
            bool prone = stance == Stance.Prone;
            IsStealth = keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed;
            IsHoldingBreath = IsAiming && keyboard.leftShiftKey.isPressed && BreathStamina > .1f;
            bool sprinting = keyboard.leftShiftKey.isPressed && stance == Stance.Standing && !IsStealth && !IsAiming && SprintStamina > .1f;
            float leanDirection = (keyboard.qKey.isPressed ? -1f : 0f) + (keyboard.eKey.isPressed ? 1f : 0f);
            currentLean = Mathf.Lerp(currentLean, leanDirection * leanAngle, Time.deltaTime * 10f);
            float heightRatio = prone ? .16f : crouched ? .62f : 1f;
            controller.height = Mathf.Lerp(controller.height, standingHeight * heightRatio, Time.deltaTime * 12f);
            float targetRadius = prone ? Mathf.Min(standingRadius, standingHeight * heightRatio * .45f) : standingRadius;
            controller.radius = Mathf.Lerp(controller.radius, targetRadius, Time.deltaTime * 12f);
            controller.center = Vector3.up * controller.height * .5f;
            if (viewCamera != null)
            {
                Vector3 cameraPosition = viewCamera.transform.localPosition;
                cameraPosition.y = Mathf.Lerp(cameraPosition.y, standingCameraY * heightRatio, Time.deltaTime * 12f);
                cameraPosition.x = Mathf.Lerp(cameraPosition.x, standingCameraX + leanDirection * leanOffset, Time.deltaTime * 10f);
                viewCamera.transform.localPosition = cameraPosition;
            }

            Vector2 input = Vector2.zero;
            if (keyboard.wKey.isPressed) input.y += 1;
            if (keyboard.sKey.isPressed) input.y -= 1;
            if (keyboard.dKey.isPressed) input.x += 1;
            if (keyboard.aKey.isPressed) input.x -= 1;
            input = Vector2.ClampMagnitude(input, 1f);
            float speed = prone ? proneSpeed : crouched ? crouchSpeed : sprinting ? sprintSpeed : walkSpeed;
            if (IsStealth && !prone) speed *= .5f;
            PlayerVitals vitals = RaidContext.Loadout?.vitals;
            if (vitals != null)
            {
                float legHealth = Mathf.Min(vitals.leftLeg / 65f, vitals.rightLeg / 65f);
                speed *= Mathf.Lerp(.55f, 1f, legHealth);
                if (vitals.fracturedParts != null && (vitals.fracturedParts.Contains("leftLeg") || vitals.fracturedParts.Contains("rightLeg"))) speed *= .68f;
            }
            bool running = sprinting && input.sqrMagnitude > .01f;
            if (input.sqrMagnitude < .01f) NoiseRadius = 0f;
            else if (prone) NoiseRadius = 2.5f;
            else if (IsStealth) NoiseRadius = crouched ? 3.5f : 5f;
            else if (running) NoiseRadius = 18f;
            else NoiseRadius = crouched ? 7f : 10f;
            SprintStamina = Mathf.Clamp(SprintStamina + (running ? -15f : 9f) * Time.deltaTime, 0f, 100f);
            BreathStamina = Mathf.Clamp(BreathStamina + (IsHoldingBreath ? -18f : 12f) * Time.deltaTime, 0f, 100f);
            if (BreathStamina <= .1f) IsHoldingBreath = false;
            Vector3 motion = (transform.forward * input.y + transform.right * input.x) * speed;
            if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            if (controller.isGrounded && keyboard.spaceKey.wasPressedThisFrame && stance == Stance.Standing)
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            verticalVelocity += gravity * Time.deltaTime;
            motion.y = verticalVelocity;
            controller.Move(motion * Time.deltaTime);

            if (mouse != null && Cursor.lockState == CursorLockMode.Locked)
            {
                Vector2 look = mouse.delta.ReadValue() * mouseSensitivity;
                transform.Rotate(0f, look.x, 0f);
                pitch = Mathf.Clamp(pitch - look.y, -85f, 85f);
                viewCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, -currentLean);
            }
            float targetFieldOfView = IsAiming ? 56f : sprinting && input.y > 0 ? 79f : 74f;
            viewCamera.fieldOfView = Mathf.Lerp(viewCamera.fieldOfView, targetFieldOfView, Time.deltaTime * (IsAiming ? 10f : 5f));
        }

        public void AddRecoil(float vertical, float horizontal)
        {
            pitch = Mathf.Clamp(pitch - vertical, -85f, 85f);
            transform.Rotate(0f, horizontal, 0f);
        }
    }
}
