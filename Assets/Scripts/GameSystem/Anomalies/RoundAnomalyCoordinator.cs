using System.Collections.Generic;
using UnityEngine;

/// <summary>Owns the declared round anomaly; object scanning is only evidence for Visual rounds.</summary>
public class RoundAnomalyCoordinator : MonoBehaviour
{
    [SerializeField] private AudioAnomalyController audioAnomaly;
    [SerializeField] private TimedAnomalyController timedAnomaly;
    [SerializeField] private PlayerAnomalyController playerAnomaly;
    [SerializeField] private bool debugLogs;

    private IRoundAnomaly activeAnomaly;

    public AnomalyCategory CurrentCategory { get; private set; }
    public bool HasConfiguredAnomaly => CurrentCategory != AnomalyCategory.None;
    public string CurrentAnomalyId => activeAnomaly?.AnomalyId ?? (CurrentCategory == AnomalyCategory.Visual ? "visual.scene-item" : "none");
    public bool HasFired => activeAnomaly?.HasFired ?? CurrentCategory == AnomalyCategory.Visual;
    public string TriggerDescription => activeAnomaly?.TriggerDescription ?? (CurrentCategory == AnomalyCategory.Visual ? "入室時から配置済み" : "なし");

    private void Awake()
    {
        audioAnomaly ??= GetComponent<AudioAnomalyController>();
        timedAnomaly ??= GetComponent<TimedAnomalyController>();
        playerAnomaly ??= GetComponent<PlayerAnomalyController>();
    }

    public IReadOnlyList<AnomalyCategory> GetAvailableCategories(bool includeVisual)
    {
        var result = new List<AnomalyCategory>();
        if (includeVisual) result.Add(AnomalyCategory.Visual);
        if (audioAnomaly != null && audioAnomaly.IsAvailable) result.Add(AnomalyCategory.Audio);
        if (timedAnomaly != null && timedAnomaly.IsAvailable) result.Add(AnomalyCategory.Timed);
        if (playerAnomaly != null && playerAnomaly.IsAvailable) result.Add(AnomalyCategory.Player);
        return result;
    }

    public AnomalyCategory ChooseCategory(bool includeVisual)
    {
        var categories = GetAvailableCategories(includeVisual);
        return categories.Count == 0 ? AnomalyCategory.None : categories[Random.Range(0, categories.Count)];
    }

    public void BeginRound(AnomalyCategory category)
    {
        ResetRound();
        audioAnomaly?.BeginReferenceAmbience();
        CurrentCategory = category;
        activeAnomaly = GetController(category);
        activeAnomaly?.BeginAnomaly();
        if (debugLogs || Debug.isDebugBuild)
            Debug.Log($"[RoundAnomaly] category={CurrentCategory}, id={CurrentAnomalyId}, fired={HasFired}, condition={TriggerDescription}");
    }

    public void ResetRound()
    {
        audioAnomaly?.ResetAnomaly();
        timedAnomaly?.ResetAnomaly();
        playerAnomaly?.ResetAnomaly();
        activeAnomaly = null;
        CurrentCategory = AnomalyCategory.None;
    }

    private IRoundAnomaly GetController(AnomalyCategory category)
    {
        return category switch
        {
            AnomalyCategory.Audio => audioAnomaly,
            AnomalyCategory.Timed => timedAnomaly,
            AnomalyCategory.Player => playerAnomaly,
            _ => null
        };
    }

    private void OnDisable() => ResetRound();
    private void OnDestroy() => ResetRound();
}
