using UnityEngine;

/// <summary>
/// 保存された prefabId から Resources 配下のプレハブを解決する。
/// </summary>
public static class PrefabResolver
{
    private static readonly string[] SearchRoots =
    {
        "Prefabs/Abnormalities/",
        "Prefabs/Structures/",
        "Prefabs/map/",
        "Prefabs/"
    };

    public static GameObject Load(string prefabId)
    {
        return TryLoad(prefabId, out var prefab, out _) ? prefab : null;
    }

    public static bool TryLoad(string prefabId, out GameObject prefab, out string resolvedPath)
    {
        prefab = null;
        resolvedPath = null;

        if (string.IsNullOrWhiteSpace(prefabId))
        {
            return false;
        }

        foreach (string root in SearchRoots)
        {
            string path = root + prefabId;
            prefab = Resources.Load<GameObject>(path);
            if (prefab == null) continue;

            resolvedPath = path;
            return true;
        }

        return false;
    }
}
