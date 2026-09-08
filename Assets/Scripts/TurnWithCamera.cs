using UnityEngine;

public class TurnWithCamera : MonoBehaviour
{
    public Transform cameraTransform;  // Inspectorで Main Camera を指定

    void Update()
    {
        if (Input.GetAxis("Horizontal") != 0 || Input.GetAxis("Vertical") != 0)
        {
            Vector3 forward = cameraTransform.forward;
            forward.y = 0; // 水平面だけを向く
            if (forward.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(forward),
                    Time.deltaTime * 10f
                );
            }
        }
    }
}
