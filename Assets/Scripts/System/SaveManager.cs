using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

public static class SaveManager
{
    private static string SaveDirectory => SavePathProvider.SaveDirectory;

    private static string GetSlotPath(int slotIndex) =>
        Path.Combine(SaveDirectory, $"slot_{slotIndex:D2}.json");

    // ============================
    // データ定義（内部クラス群）
    // ============================

    [Serializable]
    public class SaveMeta
    {
        public int slotIndex;
        public string title;
        public string savedAtIso;
        public int sceneCount;
        public string version = "1.0.0";
    }

    [Serializable]
    public class ItemData
    {
        public string prefabId;
        public Vector3 position;
        public Vector3 rotation;
        public bool isAnomaly;
    }

    [Serializable]
    public class SceneFileDto
    {
        public int sceneId;
        public string sceneName;
        public bool anomalyHouse;
        public List<ItemData> items = new();
    }

    [Serializable]
    public class SaveSlotData
    {
        public SaveMeta meta = new();
        public List<SceneFileDto> scenes = new();
    }

    // ============================
    // Save / Load 系
    // ============================

    public static void SaveSlot(int slotIndex, SaveSlotData slotData)
    {
        try
        {
            if (!Directory.Exists(SaveDirectory))
                Directory.CreateDirectory(SaveDirectory);

            slotData.meta.slotIndex = slotIndex;
            slotData.meta.savedAtIso = DateTime.UtcNow.ToString("o");
            slotData.meta.sceneCount = slotData.scenes?.Count ?? 0;

            string json = JsonUtility.ToJson(slotData, true);
            File.WriteAllText(GetSlotPath(slotIndex), json);
            Debug.Log($"💾 Saved slot {slotIndex} at {GetSlotPath(slotIndex)}");
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ Save failed: {e.Message}");
        }
    }

    public static SaveSlotData LoadSlot(int slotIndex)
    {
        string path = GetSlotPath(slotIndex);
        if (!File.Exists(path))
        {
            Debug.LogWarning($"⚠ Slot {slotIndex} not found.");
            return null;
        }

        try
        {
            string json = File.ReadAllText(path);
            var slot = JsonUtility.FromJson<SaveSlotData>(json);
            if (slot.meta.sceneCount == 0 && slot.scenes != null)
                slot.meta.sceneCount = slot.scenes.Count;
            return slot;
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ Load failed: {e.Message}");
            return null;
        }
    }

    public static List<SaveMeta> GetAllSlotMeta(int slotCount = 20)
    {
        var list = new List<SaveMeta>();
        for (int i = 0; i < slotCount; i++)
        {
            string path = GetSlotPath(i);
            if (!File.Exists(path))
            {
                list.Add(new SaveMeta
                {
                    slotIndex = i,
                    title = $"Empty Slot {i:D2}",
                    savedAtIso = "",
                    sceneCount = 0
                });
                continue;
            }

            try
            {
                var json = File.ReadAllText(path);
                var slot = JsonUtility.FromJson<SaveSlotData>(json);
                list.Add(slot.meta);
            }
            catch
            {
                list.Add(new SaveMeta
                {
                    slotIndex = i,
                    title = $"Corrupted Slot {i:D2}",
                    savedAtIso = "",
                    sceneCount = 0
                });
            }
        }
        return list;
    }

    public static bool SlotExists(int slotIndex)
        => File.Exists(GetSlotPath(slotIndex));

    public static void DeleteSlot(int slotIndex)
    {
        string path = GetSlotPath(slotIndex);
        if (File.Exists(path))
        {
            File.Delete(path);
            Debug.Log($"🗑 Deleted slot {slotIndex}");
        }
    }
}
