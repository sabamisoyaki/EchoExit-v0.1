using UnityEngine;

/// <summary>Maintains a stable reference ambience and applies one subtle, identifiable pitch anomaly.</summary>
public class AudioAnomalyController : MonoBehaviour, IRoundAnomaly
{
    [SerializeField] private AudioSource ambienceSource;
    [SerializeField] private AudioClip referenceAmbience;
    [SerializeField, Range(0f, 1f)] private float referenceVolume = 0.28f;
    [SerializeField, Range(0.8f, 1.2f)] private float anomalousPitch = 0.92f;

    private float originalVolume;
    private float originalPitch;
    private float originalSpatialBlend;
    private bool originalLoop;
    private AudioClip originalClip;

    public AnomalyCategory Category => AnomalyCategory.Audio;
    public bool IsAvailable => ambienceSource != null && (referenceAmbience != null || ambienceSource.clip != null);
    public bool HasFired { get; private set; }
    public string AnomalyId => "audio.ambience-pitch-low";
    public string TriggerDescription => "ラウンド開始時から環境音のPitchが低い";

    private void Awake()
    {
        if (ambienceSource == null) ambienceSource = GetComponent<AudioSource>();
        if (ambienceSource == null) ambienceSource = gameObject.AddComponent<AudioSource>();
        CaptureOriginalState();
    }

    public void BeginReferenceAmbience()
    {
        ResetAnomaly();
        if (referenceAmbience != null) ambienceSource.clip = referenceAmbience;
        if (ambienceSource.clip == null) return;
        ambienceSource.loop = true;
        ambienceSource.spatialBlend = 0f;
        ambienceSource.volume = referenceVolume;
        ambienceSource.pitch = 1f;
        if (!ambienceSource.isPlaying) ambienceSource.Play();
    }

    public void BeginAnomaly()
    {
        BeginReferenceAmbience();
        if (!IsAvailable) return;
        ambienceSource.pitch = anomalousPitch;
        HasFired = true;
    }

    public void ResetAnomaly()
    {
        HasFired = false;
        if (ambienceSource == null) return;
        ambienceSource.Stop();
        ambienceSource.clip = originalClip;
        ambienceSource.volume = originalVolume;
        ambienceSource.pitch = originalPitch;
        ambienceSource.spatialBlend = originalSpatialBlend;
        ambienceSource.loop = originalLoop;
    }

    private void CaptureOriginalState()
    {
        originalClip = ambienceSource.clip;
        originalVolume = ambienceSource.volume;
        originalPitch = ambienceSource.pitch;
        originalSpatialBlend = ambienceSource.spatialBlend;
        originalLoop = ambienceSource.loop;
    }

    private void OnDisable() => ResetAnomaly();
    private void OnDestroy() => ResetAnomaly();
}
