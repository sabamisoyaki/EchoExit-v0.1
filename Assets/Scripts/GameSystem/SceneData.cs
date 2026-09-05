using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// プレイ用 JSON のルート。既存の JSON キーを維持した共有データモデル。
/// </summary>
[Serializable]
public sealed class SceneDataFile
{
    public List<SceneDataEntry> scenes = new List<SceneDataEntry>();

    public void Normalize()
    {
        scenes ??= new List<SceneDataEntry>();

        foreach (var scene in scenes)
        {
            if (scene != null)
            {
                scene.items ??= new List<SceneItemData>();
            }
        }
    }

    public List<int> GetSceneIds()
    {
        Normalize();
        return scenes
            .Where(scene => scene != null && scene.sceneId > 0)
            .Select(scene => scene.sceneId)
            .Distinct()
            .OrderBy(sceneId => sceneId)
            .ToList();
    }

    public int GetNextAvailableSceneId()
    {
        var usedIds = new HashSet<int>(GetSceneIds());
        int sceneId = 1;

        while (usedIds.Contains(sceneId))
        {
            sceneId++;
        }

        return sceneId;
    }

    /// <summary>
    /// 同じ Scene ID の既存ブロックをまとめて編集用の1レコードにする。
    /// </summary>
    public SceneDataEntry GetMergedScene(int sceneId)
    {
        Normalize();
        var matches = scenes.Where(scene => scene != null && scene.sceneId == sceneId).ToList();
        if (matches.Count == 0)
        {
            return null;
        }

        var merged = new SceneDataEntry { sceneId = sceneId };
        foreach (var scene in matches)
        {
            foreach (var item in scene.items ?? new List<SceneItemData>())
            {
                if (item == null) continue;
                merged.items.Add(item.Clone());
                merged.anomalyHouse |= item.isAnomaly;
            }
        }

        return merged;
    }

    /// <summary>
    /// 同じ Scene ID のレコードをすべて置き換え、ID順に整列する。
    /// </summary>
    public void Upsert(SceneDataEntry replacement)
    {
        if (replacement == null) throw new ArgumentNullException(nameof(replacement));

        Normalize();
        replacement.items ??= new List<SceneItemData>();
        scenes.RemoveAll(scene => scene != null && scene.sceneId == replacement.sceneId);
        scenes.Add(replacement);
        scenes = scenes
            .Where(scene => scene != null)
            .OrderBy(scene => scene.sceneId)
            .ToList();
    }
}

[Serializable]
public sealed class SceneDataEntry
{
    public int sceneId;
    public bool anomalyHouse;
    public List<SceneItemData> items = new List<SceneItemData>();
}

[Serializable]
public sealed class SceneItemData
{
    public string prefabId;
    public Vector3 position;
    public Vector3 rotation;
    public bool isAnomaly;

    public SceneItemData Clone()
    {
        return new SceneItemData
        {
            prefabId = prefabId,
            position = position,
            rotation = rotation,
            isAnomaly = isAnomaly
        };
    }
}
