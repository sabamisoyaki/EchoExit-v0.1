using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AbnormalityPresenceDetector : MonoBehaviour
{
    [Header("Detection Source")]
    [SerializeField] private string anomalyTag = "Anomary";
    [SerializeField] private bool useTagOnly = false;
    [SerializeField] private string resourcesFolder = "Prefabs/Abnormalities";
    [SerializeField] private Transform scanRoot;
    [SerializeField] private bool includeInactive = true;
    // 名前フォールバックは「プレハブ名が Abnormalities フォルダにあるか」でしか判定できず、
    // 同じプレハブを通常アイテムとして配置したケース（ChairPrefab / DollPrefab など）を
    // 異変として誤検知する。既定ではタグとマーカーのみを信頼する。
    [SerializeField] private bool useMarkerOnly = true;

    [Header("Auto Scan")]
    [SerializeField] private bool scanOnStart = true;
    [SerializeField] private float scanIntervalSeconds = 0f;

    public bool HasAbnormality { get; private set; }

    private readonly HashSet<string> abnormalityPrefabNames = new HashSet<string>();
    private readonly List<GameObject> detectedObjects = new List<GameObject>();
    private bool warnedMissingTag = false;

    public IReadOnlyList<GameObject> DetectedObjects => detectedObjects;

    public void SetScanRoot(Transform root)
    {
        scanRoot = root;
    }

    /// <summary>
    /// プレハブ名による推測検知を使うかどうか。
    /// 生成側がインスタンスへ明示的にタグ／マーカーを付与している場合は false にする。
    /// </summary>
    public void SetNameFallbackEnabled(bool enabled)
    {
        useMarkerOnly = !enabled;
    }

    private void Awake()
    {
        CacheAbnormalityPrefabNames();
    }

    private void Start()
    {
        if (scanOnStart)
        {
            ScanNow();
        }

        if (scanIntervalSeconds > 0f)
        {
            InvokeRepeating(nameof(ScanNow), scanIntervalSeconds, scanIntervalSeconds);
        }
    }

    public bool ScanNow()
    {
        if (abnormalityPrefabNames.Count == 0)
        {
            CacheAbnormalityPrefabNames();
        }

        detectedObjects.Clear();

        foreach (var tr in EnumerateTargets())
        {
            if (tr == null) continue;

            var go = tr.gameObject;
            if (!includeInactive && !go.activeInHierarchy) continue;

            if (HasAnomalyTag(go))
            {
                detectedObjects.Add(go);
                continue;
            }

            if (useTagOnly)
            {
                continue;
            }

            if (go.GetComponent<AbnormalityInstanceMarker>() != null)
            {
                detectedObjects.Add(go);
                continue;
            }

            if (useMarkerOnly)
            {
                continue;
            }

            var normalizedName = NormalizeObjectName(go.name);
            if (abnormalityPrefabNames.Contains(normalizedName))
            {
                detectedObjects.Add(go);
            }
        }

        HasAbnormality = detectedObjects.Count > 0;
        return HasAbnormality;
    }

    private void CacheAbnormalityPrefabNames()
    {
        abnormalityPrefabNames.Clear();

        var prefabs = Resources.LoadAll<GameObject>(resourcesFolder);
        foreach (var prefab in prefabs)
        {
            if (prefab == null) continue;
            abnormalityPrefabNames.Add(prefab.name);
        }

        if (abnormalityPrefabNames.Count == 0)
        {
            Debug.LogWarning($"AbnormalityPresenceDetector: no prefabs found in Resources/{resourcesFolder}");
        }
    }

    private IEnumerable<Transform> EnumerateTargets()
    {
        if (scanRoot != null)
        {
            foreach (var tr in EnumerateWithChildren(scanRoot))
            {
                yield return tr;
            }
            yield break;
        }

        var scene = gameObject.scene.IsValid() ? gameObject.scene : SceneManager.GetActiveScene();
        var roots = scene.GetRootGameObjects();
        foreach (var root in roots)
        {
            if (root == null) continue;
            foreach (var tr in EnumerateWithChildren(root.transform))
            {
                yield return tr;
            }
        }
    }

    private static IEnumerable<Transform> EnumerateWithChildren(Transform root)
    {
        var stack = new Stack<Transform>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            yield return current;

            for (int i = 0; i < current.childCount; i++)
            {
                stack.Push(current.GetChild(i));
            }
        }
    }

    private static string NormalizeObjectName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName)) return string.Empty;

        const string cloneSuffix = "(Clone)";
        if (objectName.EndsWith(cloneSuffix))
        {
            return objectName.Substring(0, objectName.Length - cloneSuffix.Length).Trim();
        }

        return objectName.Trim();
    }

    private bool HasAnomalyTag(GameObject go)
    {
        if (string.IsNullOrEmpty(anomalyTag))
        {
            return false;
        }

        try
        {
            return go.CompareTag(anomalyTag);
        }
        catch (UnityException)
        {
            if (!warnedMissingTag)
            {
                warnedMissingTag = true;
                Debug.LogWarning($"AbnormalityPresenceDetector: tag '{anomalyTag}' is not defined in Tags and Layers.");
            }
            return false;
        }
    }
}
