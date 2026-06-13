using UnityEngine;
using System.Collections.Generic;
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

    void Start()
    {
        // ダミーJSONデータ
        string json = @"
        {
            ""objects"": [
                { ""type"": ""trap"", ""position"": [1, 0, 2] },
                { ""type"": ""wall"", ""position"": [3, 0, 1] },
                { ""type"": ""goal"", ""position"": [5, 0, 0] }
            ]
        }";

        // JSONデコード
        MapObjectList mapData = JsonUtility.FromJson<MapObjectList>(json);

        // 配置
        foreach (MapObject obj in mapData.objects)
        {
            Vector3 pos = new Vector3(obj.position[0], obj.position[1], obj.position[2]);
            string prefabName = obj.type.FirstCharToUpper();
            GameObject prefab = Resources.Load<GameObject>($"Prefabs/{prefabName}");
            if (prefab != null)
            {
                Instantiate(prefab, pos, Quaternion.identity);
                Debug.Log($"[ObjectLoader] 配置完了: {prefabName} at {pos}");
            }
            else
            {
                Debug.LogWarning($"[ObjectLoader] プレハブが見つかりません: {prefabName} at {pos}");
            }
        }

    }
}
