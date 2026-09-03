using UnityEngine;

/// <summary>Applies a subtle FOV mismatch without changing or disabling player input.</summary>
public class PlayerAnomalyController : MonoBehaviour, IRoundAnomaly
{
    [SerializeField] private Camera targetCamera;
    [SerializeField, Range(1f, 10f)] private float fovOffset = 5f;

    private float originalFov;
    private bool stateCaptured;

    public AnomalyCategory Category => AnomalyCategory.Player;
    public bool IsAvailable => targetCamera != null;
    public bool HasFired { get; private set; }
    public string AnomalyId => "player.camera-fov-wide";
    public string TriggerDescription => "ラウンド開始時から視野角がわずかに広い";

    private void Awake()
    {
        if (targetCamera == null) targetCamera = Camera.main;
    }

    public void BeginAnomaly()
    {
        ResetAnomaly();
        if (!IsAvailable) return;
        originalFov = targetCamera.fieldOfView;
        stateCaptured = true;
        targetCamera.fieldOfView = Mathf.Clamp(originalFov + fovOffset, 40f, 90f);
        HasFired = true;
    }

    public void ResetAnomaly()
    {
        HasFired = false;
        if (targetCamera != null && stateCaptured) targetCamera.fieldOfView = originalFov;
        stateCaptured = false;
    }

    private void OnDisable() => ResetAnomaly();
    private void OnDestroy() => ResetAnomaly();
}
