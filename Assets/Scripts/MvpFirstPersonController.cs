using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public sealed class MvpFirstPersonController : MonoBehaviour
{
    [Header("移動")]
    [SerializeField, Min(0.1f)] private float moveSpeed = 5f;
    [SerializeField] private float gravity = -19.62f;

    [Header("視点")]
    [SerializeField] private Camera viewCamera;
    [SerializeField] private float eyeHeight = 0.65f;
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float gamepadLookSpeed = 120f;
    [SerializeField] private Vector2 pitchLimits = new Vector2(-80f, 80f);

    private CharacterController characterController;
    private float verticalVelocity;
    private float pitch;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        if (viewCamera == null)
        {
            viewCamera = GetComponentInChildren<Camera>(true);
        }

        // 旧操作系との二重入力を避ける。ファイル自体は既存シーン互換のため残す。
        var legacyMovement = GetComponent<PlayerMovement>();
        if (legacyMovement != null)
        {
            legacyMovement.enabled = false;
        }

        if (viewCamera != null)
        {
            var legacyCamera = viewCamera.GetComponent<CameraController>();
            if (legacyCamera != null)
            {
                legacyCamera.enabled = false;
            }

            viewCamera.transform.localPosition = new Vector3(0f, eyeHeight, 0f);
            viewCamera.transform.localRotation = Quaternion.identity;
        }
    }

    private void OnEnable()
    {
        LockCursor();
    }

    private void Update()
    {
        HandleCursor();
        if (Cursor.lockState != CursorLockMode.Locked) return;

        UpdateLook();
        UpdateMovement();
    }

    private void UpdateMovement()
    {
        Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        if (Gamepad.current != null)
        {
            Vector2 stick = Gamepad.current.leftStick.ReadValue();
            if (stick.sqrMagnitude > input.sqrMagnitude)
            {
                input = stick;
            }
        }

        Vector3 planarMove = transform.right * input.x + transform.forward * input.y;
        planarMove = Vector3.ClampMagnitude(planarMove, 1f);

        if (characterController.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }

        Vector3 motion = planarMove * moveSpeed + Vector3.up * verticalVelocity;
        characterController.Move(motion * Time.deltaTime);
    }

    private void UpdateLook()
    {
        float yawDelta = Input.GetAxisRaw("Mouse X") * mouseSensitivity;
        float pitchDelta = Input.GetAxisRaw("Mouse Y") * mouseSensitivity;

        if (Gamepad.current != null)
        {
            Vector2 stick = Gamepad.current.rightStick.ReadValue();
            yawDelta += stick.x * gamepadLookSpeed * Time.deltaTime;
            pitchDelta += stick.y * gamepadLookSpeed * Time.deltaTime;
        }

        transform.Rotate(Vector3.up, yawDelta, Space.World);
        pitch = Mathf.Clamp(pitch - pitchDelta, pitchLimits.x, pitchLimits.y);
        if (viewCamera != null)
        {
            viewCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
    }

    private static void HandleCursor()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked)
        {
            LockCursor();
        }
    }

    private static void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
