using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    public float speed = 5f;
    public float gravity = -9.81f;
    public float groundCheckOffset = -0.1f; // 接地補正

    private CharacterController controller;
    private Vector3 velocity;

    void Start()
    {
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        Transform cam = Camera.main.transform;

        Vector3 forward = cam.forward;
        Vector3 right = cam.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 move = forward * z + right * x;

        // 移動
        controller.Move(move * speed * Time.deltaTime);

        // 接地判定
        if (controller.isGrounded && velocity.y < 0)
        {
            velocity.y = groundCheckOffset; // わずかに下向きで地面に貼り付ける
        }
        else
        {
            velocity.y += gravity * Time.deltaTime; // 空中なら重力加算
        }

        // 重力適用
        controller.Move(velocity * Time.deltaTime);
    }
}


