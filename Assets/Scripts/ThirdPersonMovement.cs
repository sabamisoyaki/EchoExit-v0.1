using UnityEngine;

public class ThirdPersonMovement : MonoBehaviour
{
    public Transform playerBody;
    public float mouseSensitivity = 2f;
    float xRotation = 0f;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        // 上下の視点（カメラのX回転）
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -80f, 80f);
        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        // 左右の視点（キャラのY回転）
        playerBody.Rotate(Vector3.up * mouseX);
    }
}
