using System.Collections;
using UnityEngine;

/// <summary>Dims a scene light after a deterministic observation window.</summary>
public class TimedAnomalyController : MonoBehaviour, IRoundAnomaly
{
    [SerializeField] private Light targetLight;
    [SerializeField, Min(1f)] private float delaySeconds = 8f;
    [SerializeField, Range(0.1f, 0.9f)] private float intensityMultiplier = 0.45f;

    private float originalIntensity;
    private Color originalColor;
    private Coroutine routine;
    private bool stateCaptured;

    public AnomalyCategory Category => AnomalyCategory.Timed;
    public bool IsAvailable => targetLight != null;
    public bool HasFired { get; private set; }
    public string AnomalyId => "timed.light-dim";
    public string TriggerDescription => $"入室から{delaySeconds:0.#}秒後に照明が暗くなる";

    private void Awake()
    {
        if (targetLight == null) targetLight = FindFirstObjectByType<Light>();
    }

    public void BeginAnomaly()
    {
        ResetAnomaly();
        if (!IsAvailable) return;
        CaptureOriginalState();
        stateCaptured = true;
        routine = StartCoroutine(FireAfterDelay());
    }

    public void ResetAnomaly()
    {
        if (routine != null) StopCoroutine(routine);
        routine = null;
        HasFired = false;
        if (targetLight != null && stateCaptured)
        {
            targetLight.intensity = originalIntensity;
            targetLight.color = originalColor;
        }
        stateCaptured = false;
    }

    private IEnumerator FireAfterDelay()
    {
        yield return new WaitForSecondsRealtime(delaySeconds);
        if (targetLight == null) yield break;
        targetLight.intensity = originalIntensity * intensityMultiplier;
        HasFired = true;
        routine = null;
    }

    private void CaptureOriginalState()
    {
        if (targetLight == null) return;
        originalIntensity = targetLight.intensity;
        originalColor = targetLight.color;
    }

    private void OnDisable() => ResetAnomaly();
    private void OnDestroy() => ResetAnomaly();
}
