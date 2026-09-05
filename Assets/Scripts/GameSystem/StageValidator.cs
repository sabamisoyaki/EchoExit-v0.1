using System.Collections.Generic;
using System.Linq;

public sealed class StageValidationResult
{
    public List<string> Errors { get; } = new List<string>();
    public List<string> Warnings { get; } = new List<string>();
    public bool IsValid => Errors.Count == 0;
}

/// <summary>共有前に、ステージがMVPのプレイ条件を満たすか検証する。</summary>
public static class StageValidator
{
    public static StageValidationResult Validate(SceneDataEntry scene, int maxAnomalies)
    {
        var result = new StageValidationResult();
        if (scene == null)
        {
            result.Errors.Add("ステージデータがありません。");
            return result;
        }

        if (scene.sceneId <= 0)
        {
            result.Errors.Add("Scene ID は1以上である必要があります。");
        }

        var items = scene.items?.Where(item => item != null).ToList() ?? new List<SceneItemData>();
        if (items.Count == 0)
        {
            result.Errors.Add("オブジェクトを1個以上配置してください。");
            return result;
        }

        int anomalyCount = items.Count(item => item.isAnomaly);
        if (anomalyCount > maxAnomalies)
        {
            result.Errors.Add($"異変は最大{maxAnomalies}個までです（現在{anomalyCount}個）。");
        }

        int aggressiveCount = items.Count(item =>
            item.isAnomaly && AnomalyRuntimeFactory.GetProfile(item.prefabId).Aggressive);
        if (aggressiveCount > 1)
        {
            result.Errors.Add($"追跡型の異変は1ステージにつき1個までです（現在{aggressiveCount}個）。");
        }

        int missingIdCount = items.Count(item => string.IsNullOrWhiteSpace(item.prefabId));
        if (missingIdCount > 0)
        {
            result.Errors.Add($"Prefab ID が空のオブジェクトが{missingIdCount}個あります。");
        }

        int unresolvedCount = items.Count(item =>
            !string.IsNullOrWhiteSpace(item.prefabId) && PrefabResolver.Load(item.prefabId) == null);
        if (unresolvedCount > 0)
        {
            result.Warnings.Add($"この環境で見つからないPrefabが{unresolvedCount}個あります。データは保持されます。");
        }

        return result;
    }
}
