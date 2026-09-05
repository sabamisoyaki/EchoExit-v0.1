using UnityEngine;
using Extensions;

public class ObjectLoader : MonoBehaviour
{
    [System.Serializable]
    public class MapObject
    {
        public string type;
        public float[] position;
    }

    [System.Serializable]
    public class MapObjectList
    {
        public MapObject[] objects;
    }

    [TextArea(3, 10)]
    [SerializeField] private string jsonOverride;
    [SerializeField] private bool loadOnStart = false;
    [SerializeField] private string prefabResourcesFolder = "Prefabs";

    void Start()
    {
        if (!loadOnStart) return;

        LoadFromJson(jsonOverride);
    }

    public void LoadFromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            Debug.LogWarning("[ObjectLoader] JSON is empty.");
            return;
        }

        MapObjectList mapData = JsonUtility.FromJson<MapObjectList>(json);
        if (mapData?.objects == null)
        {
            Debug.LogWarning("[ObjectLoader] JSON does not contain an objects array.");
            return;
        }

        foreach (MapObject obj in mapData.objects)
        {
            if (!TryGetPosition(obj, out Vector3 pos))
            {
                Debug.LogWarning("[ObjectLoader] Skipped object with invalid position data.");
                continue;
            }

            string prefabName = string.IsNullOrWhiteSpace(obj.type) ? string.Empty : obj.type.FirstCharToUpper();
            if (string.IsNullOrEmpty(prefabName))
            {
                Debug.LogWarning("[ObjectLoader] Skipped object with empty type.");
                continue;
            }

            GameObject prefab = Resources.Load<GameObject>($"{prefabResourcesFolder}/{prefabName}");
            if (prefab != null)
            {
                Instantiate(prefab, pos, Quaternion.identity);
                Debug.Log($"[ObjectLoader] Placed: {prefabName} at {pos}");
            }
            else
            {
                Debug.LogWarning($"[ObjectLoader] Prefab not found: {prefabName} at {pos}");
            }
        }
    }

    private static bool TryGetPosition(MapObject obj, out Vector3 pos)
    {
        pos = Vector3.zero;
        if (obj?.position == null || obj.position.Length < 3) return false;

        pos = new Vector3(obj.position[0], obj.position[1], obj.position[2]);
        return true;
    }
}
