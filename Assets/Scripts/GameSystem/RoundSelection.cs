using System;
using System.Collections.Generic;
using System.Linq;

public readonly struct RoundSelection
{
    public RoundSelection(int sceneId, bool hasAnomaly, bool usedFallback, bool isValid)
    {
        SceneId = sceneId;
        HasAnomaly = hasAnomaly;
        UsedFallback = usedFallback;
        IsValid = isValid;
    }

    public int SceneId { get; }
    public bool HasAnomaly { get; }
    public bool UsedFallback { get; }
    public bool IsValid { get; }

    public static RoundSelection Invalid => new RoundSelection(-1, false, false, false);
}

public static class RoundSelectionUtility
{
    public static RoundSelection Pick(
        IReadOnlyList<int> anomalySceneIds,
        IReadOnlyList<int> normalSceneIds,
        bool wantAnomaly,
        int excludeSceneId,
        Func<int, int> pickIndex)
    {
        if (pickIndex == null) throw new ArgumentNullException(nameof(pickIndex));

        var desiredPool = wantAnomaly ? anomalySceneIds : normalSceneIds;
        var desiredCandidates = BuildCandidates(desiredPool, excludeSceneId);
        if (desiredCandidates.Count > 0)
        {
            return new RoundSelection(
                PickFrom(desiredCandidates, pickIndex),
                wantAnomaly,
                usedFallback: false,
                isValid: true);
        }

        var alternatePool = wantAnomaly ? normalSceneIds : anomalySceneIds;
        var alternateCandidates = BuildCandidates(alternatePool, excludeSceneId);
        if (alternateCandidates.Count > 0)
        {
            return new RoundSelection(
                PickFrom(alternateCandidates, pickIndex),
                !wantAnomaly,
                usedFallback: true,
                isValid: true);
        }

        return RoundSelection.Invalid;
    }

    private static List<int> BuildCandidates(IReadOnlyList<int> source, int excludeSceneId)
    {
        var candidates = source?.Distinct().ToList() ?? new List<int>();
        if (excludeSceneId >= 0 && candidates.Count > 1)
        {
            candidates.Remove(excludeSceneId);
        }

        return candidates;
    }

    private static int PickFrom(IReadOnlyList<int> candidates, Func<int, int> pickIndex)
    {
        int index = pickIndex(candidates.Count);
        if (index < 0 || index >= candidates.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(pickIndex), index, "抽選インデックスが候補範囲外です。");
        }

        return candidates[index];
    }
}
