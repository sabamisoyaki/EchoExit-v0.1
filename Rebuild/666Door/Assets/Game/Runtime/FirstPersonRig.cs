using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Door666.Runtime
{
    /// <summary>First-person body and eyes. Collider and camera settings live on Assets/Prefabs/Player.prefab,
    /// which every screen scene places; tune them there (or per scene as overrides).</summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonRig : MonoBehaviour
    {
        [SerializeField] private Camera view;
        [Tooltip("叩くときに出す手（カメラの子の FirstPersonHand）。")]
        [SerializeField] private FirstPersonHand hand;

        public Camera View => view;
        public FirstPersonHand Hand => hand;
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
            if (view == null) view = GetComponentInChildren<Camera>(true);
            if (view == null) throw new InvalidOperationException("プレイヤー「" + name + "」にカメラがありません。");
            if (hand == null) hand = GetComponentInChildren<FirstPersonHand>(true);
            if (hand == null)
                GameLog.Warning("起動", "プレイヤー「" + name + "」に叩く手（FirstPersonHand）がありません。叩いても手が出ません。メニュー「666号扉 → プロジェクトを初期化」で追加できます。", this);
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
            View.transform.localRotation = Quaternion.Euler(pitch - (hand != null ? hand.ViewKick : 0), 0, 0);
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

        /// <summary>Swings the hand at what the crosshair ray hit. The ritual takes the hit separately, on the same press.</summary>
        /// <param name="target">The gaze ray's hit within reach; a hit without a collider swings at the air.</param>
        public void Strike(RaycastHit target)
        {
            GameLog.Detail("入力", target.collider != null
                ? "叩く: " + target.collider.name + "（" + target.distance.ToString("F1") + "m）"
                : "叩く: 届く範囲に何もありません");
            if (hand != null) hand.Strike(target);
        }

        public void Teleport(Vector3 position, float yaw = 0)
        {
            if (hand != null) hand.Hide();
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
