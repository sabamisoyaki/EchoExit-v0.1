using UnityEngine;

public class SeenColorShift : MonoBehaviour
{
    public Transform playerCamera;
    public Color unseenColor = Color.white;
    public Color seenColor = Color.red;
    public float visibleAngleThreshold = 30f;
    public float requiredGazeTime = 3.0f;
    public float delayBeforeRed = 2.0f;

    private Renderer objRenderer;
    private bool hasBeenSeen = false;
    private bool isCurrentlyVisible = false;
    private float gazeTimer = 0f;
    private float exitTimer = 0f;

    void Start()
    {
        objRenderer = GetComponent<Renderer>();
        objRenderer.material.color = unseenColor;

        if (playerCamera == null)
        {
            playerCamera = Camera.main.transform;
        }
    }

    void Update()
    {
        Vector3 toObject = (transform.position - playerCamera.position).normalized;
        float angle = Vector3.Angle(playerCamera.forward, toObject);
        bool isVisibleNow = angle < visibleAngleThreshold;

        if (isVisibleNow)
        {
            isCurrentlyVisible = true;
            exitTimer = 0f;

            if (!hasBeenSeen)
            {
                hasBeenSeen = true;
            }

            isCurrentlyVisible = true;

            // 注視時間をカウント
            gazeTimer += Time.deltaTime;

            if (gazeTimer >= requiredGazeTime)
            {
                objRenderer.material.color = unseenColor;
            }
        }
        else
        {
            if (hasBeenSeen && isCurrentlyVisible)
            {
                exitTimer += Time.deltaTime;

                // ← 一定時間視線を外していたら赤に
                if (exitTimer >= delayBeforeRed)
                {
                    objRenderer.material.color = seenColor;
                    isCurrentlyVisible = false;
                }
            }

            gazeTimer = 0f;
        }
    }
}
