using UnityEngine;

public class CameraController : MonoBehaviour
{
    public Transform target;        // PlayerのTransform
    public float distance = 4f;
    public Vector2 pitchLimits = new Vector2(-20f, 60f);
    public float mouseSensitivity = 60f;  // 少し低め
    public float smoothTime = 0.1f; // 慣性の強さ（大きいほど鈍い）

    float yaw;
    float pitch;

    float yawSmoothVelocity;
    float pitchSmoothVelocity;
    float currentYaw;
    float currentPitch;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
    }

    void LateUpdate()
    {
        // 入力から目標角度を計算
        float targetYaw = yaw + Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float targetPitch = pitch - Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;
        targetPitch = Mathf.Clamp(targetPitch, pitchLimits.x, pitchLimits.y);

        // 補間して「重さ」を出す
        currentYaw = Mathf.SmoothDamp(currentYaw, targetYaw, ref yawSmoothVelocity, smoothTime);
        currentPitch = Mathf.SmoothDamp(currentPitch, targetPitch, ref pitchSmoothVelocity, smoothTime);

        // 内部値も更新
        yaw = targetYaw;
        pitch = targetPitch;

        // カメラの回転・位置を更新
        Quaternion rot = Quaternion.Euler(currentPitch, currentYaw, 0);
        Vector3 pos = target.position + rot * new Vector3(0, 0, -distance);

        transform.rotation = rot;
        transform.position = pos;
    }
}
