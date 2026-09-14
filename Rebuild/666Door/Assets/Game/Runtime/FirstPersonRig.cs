using UnityEngine;
using UnityEngine.InputSystem;

namespace Door666.Runtime
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonRig : MonoBehaviour
    {
        public Camera View { get; private set; }
        public float Speed { get; private set; }
        public float MouseSensitivity { get; set; } = .085f;
        public float GamepadSensitivity { get; set; } = 125f;
        private CharacterController controller;
        private float pitch;
        private float verticalSpeed;
        private float stepDistance;

        public void Initialize()
        {
            controller = GetComponent<CharacterController>();
            controller.height = 1.75f;
            controller.radius = .28f;
            controller.center = Vector3.up * .875f;
            controller.stepOffset = .22f;
            controller.skinWidth = .025f;
            var eye = new GameObject("Eyes", typeof(Camera), typeof(AudioListener));
            eye.transform.SetParent(transform, false);
            eye.transform.localPosition = Vector3.up * 1.6f;
            eye.tag = "MainCamera";
            View = eye.GetComponent<Camera>();
            View.fieldOfView = 72f;
            View.nearClipPlane = .05f;
            View.farClipPlane = 70f;
            View.backgroundColor = new Color(.018f, .02f, .013f);
            View.clearFlags = CameraClearFlags.SolidColor;
            MouseSensitivity = PlayerPrefs.GetFloat(GameConstants.SensitivityPreference, .085f);
            GamepadSensitivity = PlayerPrefs.GetFloat(GameConstants.GamepadSensitivityPreference, 125f);
        }

        public void Tick(PlayerInputReader input, float walkSpeed, float deltaTime)
        {
            var look = input.Look.ReadValue<Vector2>();
            bool stick = input.Look.activeControl != null && input.Look.activeControl.device is Gamepad;
            float factor = stick ? GamepadSensitivity * deltaTime : MouseSensitivity;
            transform.Rotate(0, look.x * factor, 0);
            pitch = Mathf.Clamp(pitch - look.y * factor, -82f, 82f);
            View.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
            Vector2 move = Vector2.ClampMagnitude(input.Move.ReadValue<Vector2>(), 1f);
            Vector3 horizontal = (transform.right * move.x + transform.forward * move.y) * walkSpeed;
            if (controller.isGrounded && verticalSpeed < 0) verticalSpeed = -2f;
            verticalSpeed += Physics.gravity.y * deltaTime;
            var before = transform.position;
            controller.Move((horizontal + Vector3.up * verticalSpeed) * deltaTime);
            var displacement = transform.position - before;
            displacement.y = 0;
            Speed = deltaTime > 0 ? displacement.magnitude / deltaTime : 0;
            stepDistance += displacement.magnitude;
            if (stepDistance >= 1.6f && controller.isGrounded)
            {
                stepDistance = 0;
                SpatialAudio.Emit(transform, transform.position + Vector3.up * .1f, "footstep", .16f);
            }
        }

        public void Teleport(Vector3 position, float yaw = 0)
        {
            controller.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
            controller.enabled = true;
            verticalSpeed = 0;
            pitch = 0;
            Speed = 0;
            stepDistance = 0;
            View.transform.localRotation = Quaternion.identity;
        }
    }
}
