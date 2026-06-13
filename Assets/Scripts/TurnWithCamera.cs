using UnityEngine;

public class TurnWithCamera : MonoBehaviour
{
    public Transform cameraTransform;  // Inspector‚Å Main Camera ‚ðŽw’è

    void Update()
    {
        if (Input.GetAxis("Horizontal") != 0 || Input.GetAxis("Vertical") != 0)
        {
            Vector3 forward = cameraTransform.forward;
            forward.y = 0; // …•½–Ê‚¾‚¯‚ðŒü‚­
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
