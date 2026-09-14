using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Door666.Core
{
    [Serializable, JsonObject(MemberSerialization.OptIn)]
    public struct Float3
    {
        [JsonProperty] public float x;
        [JsonProperty] public float y;
        [JsonProperty] public float z;

        public Float3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static double Distance(Float3 a, Float3 b)
        {
            double x = a.x - b.x, y = a.y - b.y, z = a.z - b.z;
            return Math.Sqrt(x * x + y * y + z * z);
        }
        public bool IsFinite => !float.IsNaN(x) && !float.IsInfinity(x)
            && !float.IsNaN(y) && !float.IsInfinity(y) && !float.IsNaN(z) && !float.IsInfinity(z);
    }

    [Serializable, JsonObject(MemberSerialization.OptIn)]
    public sealed class StageItem
    {
        [JsonProperty] public string prefabId = string.Empty;
        [JsonProperty] public Float3 position;
        [JsonProperty] public Float3 rotation;
        [JsonProperty] public bool isAnomaly;
        [JsonExtensionData] public IDictionary<string, JToken> AdditionalData;

        public StageItem Clone() => new StageItem
        {
            prefabId = prefabId, position = position, rotation = rotation,
            isAnomaly = isAnomaly, AdditionalData = StageFile.CloneExtra(AdditionalData)
        };
    }

    [Serializable, JsonObject(MemberSerialization.OptIn)]
    public sealed class StageData
    {
        [JsonProperty] public int sceneId;
        [JsonProperty] public bool anomalyHouse;
        [JsonProperty] public List<StageItem> items = new List<StageItem>();
        [JsonExtensionData] public IDictionary<string, JToken> AdditionalData;

        // The legacy house flag is kept for compatibility; it never decides the answer.
        public bool HasAnomalies => items != null && items.Any(item => item != null && item.isAnomaly);
        public StageData Clone() => new StageData
        {
            sceneId = sceneId, anomalyHouse = anomalyHouse,
            items = items == null ? new List<StageItem>() : items.Select(item => item?.Clone()).ToList(),
            AdditionalData = StageFile.CloneExtra(AdditionalData)
        };
    }

    [Serializable, JsonObject(MemberSerialization.OptIn)]
    public sealed class StageFile
    {
        [JsonProperty] public List<StageData> scenes = new List<StageData>();
        [JsonExtensionData] public IDictionary<string, JToken> AdditionalData;

        public StageFile Clone() => new StageFile
        {
            scenes = scenes == null ? new List<StageData>() : scenes.Select(stage => stage?.Clone()).ToList(),
            AdditionalData = CloneExtra(AdditionalData)
        };

        internal static IDictionary<string, JToken> CloneExtra(IDictionary<string, JToken> data)
        {
            return data?.ToDictionary(pair => pair.Key, pair => pair.Value?.DeepClone(), StringComparer.Ordinal);
        }
    }
}
