public enum AnomalyCategory
{
    None,
    Visual,
    Audio,
    Timed,
    Player
}

public interface IRoundAnomaly
{
    AnomalyCategory Category { get; }
    bool IsAvailable { get; }
    bool HasFired { get; }
    string AnomalyId { get; }
    string TriggerDescription { get; }
    void BeginAnomaly();
    void ResetAnomaly();
}
