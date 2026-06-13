using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

#region Runtime Meta
/// <summary>
/// 各配置オブジェクトに付与するメタ情報
/// </summary>
public class PlacedMeta : MonoBehaviour
{
    public string prefabId;
    public bool isAnomaly;
}
#endregion

public class EditModeManager : MonoBehaviour
{
    [Header("基本設定")]
    public Camera mainCamera;
    [Tooltip("ヒット対象レイヤー（地面や設置可能サーフェス）")]
    [SerializeField] private LayerMask placeableMask = ~0;
    [SerializeField] private float maxRayDistance = 10f;
    [SerializeField] private float rotateSpeed = 90f;
    [SerializeField] private float beamLength = 10f;

    [Header("プレハブ群")]
    public GameObject[] anomalyPrefabs;
    public GameObject[] structurePrefabs;

    [Header("プレビュー/演出")]
    public LineRenderer beamLine;
    public Material previewMaterial;

    [Header("UI")]
    public Button saveButton;
    public Button exitButton;
    public Button quitButton;
    public TMP_Text placedCountText;
    public TMP_Text selectedPrefabText;


    [Header("保存設定")]
    [SerializeField] private int currentSceneId = 1;
    [SerializeField] private SharedString sharedString;



    // 内部状態
    private enum Category { Anomaly, Structure }
    private Category currentCategory = Category.Anomaly;
    private int currentIndex = 0;
    private float rotationY = 0f;
    private GameObject previewInstance;
    private string previewPrefabId; // 再生成判定用
    private bool isUIActive = false;

    #region DTO (Save Data)
    [Serializable]
    public class SnapshotItem
    {
        public string prefabId;
        public Vector3 position;
        public Vector3 rotation; // Euler
        public bool isAnomaly;
    }

    [Serializable]
    public class SceneSnapshot
    {
        public int sceneId;
        public bool anomalyHouse;
        public List<SnapshotItem> items = new List<SnapshotItem>();
    }

    [Serializable]
    public class AllScenesData
    {
        public List<SceneSnapshot> scenes = new List<SceneSnapshot>();
    }

    private AllScenesData allScenesData = new AllScenesData();
    #endregion

    #region Unity Lifecycle
    private void Start()
    {
        // UIイベント登録
        if (saveButton != null) saveButton.onClick.AddListener(OnSaveButtonClicked);
        if (exitButton != null) exitButton.onClick.AddListener(OnExitEditMode);
        if (quitButton != null) quitButton.onClick.AddListener(OnQuitButtonClicked);


        // LineRenderer初期化
        if (beamLine != null)
        {
            if (beamLine.positionCount < 2) beamLine.positionCount = 2;
        }

        // 既存保存の読み込み（存在時）
        LoadAllScenesIfExists();
    }

    private void Update()
    {
        DrawBeam();

        // UIアクティブ中はゲーム入力を無効化
        if (!isUIActive)
        {
            HandleCategorySwitch();
            HandleScrollInput();
            HandleRotationInput();

            // プレビュー＆配置
            if (Input.GetKey(KeyCode.LeftShift))
            {
                ShowPreview();
                if (Input.GetMouseButtonDown(0))
                {
                    PlaceCurrentPrefab();
                }
            }
            else
            {
                HidePreview();
            }
        }

        // --- ショートカットキー ---
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            OnSaveButtonClicked();
        }
        if (Input.GetKeyDown(KeyCode.L))   // 編集終了
        {
            OnExitEditMode();
        }
        if (Input.GetKeyDown(KeyCode.O))   // 終了
        {
            OnQuitButtonClicked();
        }

#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.F1))
        {
            ToggleUIMode();
        }
#else
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ToggleUIMode();
        }
#endif

        UpdateUI();
    }

    private void OnDestroy()
    {
        // ハンドラ解除（メモリリーク予防）
        if (exitButton != null) exitButton.onClick.RemoveListener(OnExitEditMode);
        if (saveButton != null) saveButton.onClick.RemoveListener(OnSaveButtonClicked);
    }
    #endregion

    #region Input Handlers
    private void HandleCategorySwitch()
    {
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            currentCategory = currentCategory == Category.Anomaly ? Category.Structure : Category.Anomaly;
            currentIndex = 0;
            RecreatePreviewIfNeeded(force: true);
            Debug.Log($"カテゴリ切替: {currentCategory}");
        }
    }

    private void HandleScrollInput()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) < float.Epsilon) return;

        var arr = GetCurrentPrefabArray();
        int max = (arr == null) ? 0 : arr.Length;
        if (max == 0) return;

        currentIndex = (currentIndex + (scroll > 0 ? 1 : -1) + max) % max;
        RecreatePreviewIfNeeded(force: true);

        var pf = GetCurrentPrefab();
        if (pf != null) Debug.Log($"選択中: {pf.name}");
    }

    private void HandleRotationInput()
    {
        if (Input.GetKey(KeyCode.Q)) rotationY -= rotateSpeed * Time.deltaTime;
        if (Input.GetKey(KeyCode.E)) rotationY += rotateSpeed * Time.deltaTime;
    }
    #endregion

    #region Preview / Placement
    private GameObject[] GetCurrentPrefabArray()
    {
        return currentCategory == Category.Anomaly ? anomalyPrefabs : structurePrefabs;
    }

    private GameObject GetCurrentPrefab()
    {
        var arr = GetCurrentPrefabArray();
        if (arr == null || arr.Length == 0 || currentIndex < 0 || currentIndex >= arr.Length) return null;
        return arr[currentIndex];
    }

    private void DrawBeam()
    {
        if (beamLine == null || mainCamera == null) return;

        Vector3 origin = mainCamera.transform.position;
        Vector3 direction = mainCamera.transform.forward;

        beamLine.SetPosition(0, origin);
        beamLine.SetPosition(1, origin + direction * beamLength);
    }

    private void ShowPreview()
    {
        var prefab = GetCurrentPrefab();
        if (mainCamera == null || prefab == null) { HidePreview(); return; }

        if (previewInstance == null || previewPrefabId != prefab.name)
        {
            RecreatePreviewIfNeeded(force: true);
        }

        if (TryGetPlacement(prefab, out Vector3 pos, out Quaternion rot))
        {
            previewInstance.transform.SetPositionAndRotation(pos, rot);
        }
    }

    private void HidePreview()
    {
        if (previewInstance != null)
        {
            Destroy(previewInstance);
            previewInstance = null;
            previewPrefabId = null;
        }
    }

    private void RecreatePreviewIfNeeded(bool force)
    {
        var prefab = GetCurrentPrefab();
        string id = prefab ? prefab.name : null;

        if (!force && previewInstance != null && previewPrefabId == id) return;

        HidePreview();
        if (prefab == null) return;

        previewInstance = Instantiate(prefab);
        previewPrefabId = id;

        // プレビュー設定
        SetPreviewMaterial(previewInstance);
        DisableForPreview(previewInstance);
    }

    private bool TryGetPlacement(GameObject prefab, out Vector3 pos, out Quaternion rot)
    {
        pos = default;
        rot = default;

        if (mainCamera == null || prefab == null) return false;

        Ray ray = new Ray(mainCamera.transform.position, mainCamera.transform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, placeableMask))
            return false;

        rot = Quaternion.Euler(0f, mainCamera.transform.eulerAngles.y + rotationY, 0f);

        // 回転後Boundsで底面補正
        GameObject temp = Instantiate(prefab);
        try
        {
            temp.transform.rotation = rot;
            DisableForPreview(temp); // 余計な動作防止

            Bounds bounds = GetCombinedRendererOrColliderBounds(temp);
            float yOffset = bounds.extents.y;
            pos = hit.point + Vector3.up * yOffset;
        }
        finally
        {
            Destroy(temp);
        }

        return true;
    }

    private void PlaceCurrentPrefab()
    {
        var prefab = GetCurrentPrefab();
        if (prefab == null) return;

        if (!TryGetPlacement(prefab, out Vector3 pos, out Quaternion rot)) return;

        var go = Instantiate(prefab, pos, rot);
        var meta = go.GetComponent<PlacedMeta>();
        if (meta == null) meta = go.AddComponent<PlacedMeta>();

        meta.prefabId = prefab.name;
        meta.isAnomaly = (currentCategory == Category.Anomaly);

        Debug.Log($"設置: {meta.prefabId} at {pos} (Anomaly={meta.isAnomaly})");
    }

    private void SetPreviewMaterial(GameObject obj)
    {
        if (previewMaterial == null || obj == null) return;

        foreach (var r in obj.GetComponentsInChildren<Renderer>())
        {
            // プレビューは短命インスタンスなので material 差替えでOK（共有マテリアルは触らない）
            var mats = r.materials;
            for (int i = 0; i < mats.Length; i++) mats[i] = previewMaterial;
            r.materials = mats;
        }
    }

    private void DisableForPreview(GameObject obj)
    {
        if (obj == null) return;

        // MonoBehaviour停止（自分は除外）
        foreach (var behaviour in obj.GetComponentsInChildren<MonoBehaviour>())
        {
            if (behaviour != this) behaviour.enabled = false;
        }

        // Collider無効
        foreach (var c in obj.GetComponentsInChildren<Collider>())
        {
            c.enabled = false;
        }

        // Rigidbody無効化
        foreach (var rb in obj.GetComponentsInChildren<Rigidbody>())
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    private Bounds GetCombinedRendererOrColliderBounds(GameObject obj)
    {
        var renderers = obj.GetComponentsInChildren<Renderer>();
        if (renderers != null && renderers.Length > 0)
        {
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return b;
        }

        var colliders = obj.GetComponentsInChildren<Collider>();
        if (colliders != null && colliders.Length > 0)
        {
            Bounds b = colliders[0].bounds;
            for (int i = 1; i < colliders.Length; i++) b.Encapsulate(colliders[i].bounds);
            return b;
        }

        return new Bounds(obj.transform.position, Vector3.zero);
    }
    #endregion

    #region UI & Mode
    private void ToggleUIMode()
    {
        isUIActive = !isUIActive;

        Cursor.lockState = isUIActive ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = isUIActive;

        // 必要に応じて Canvas の有効化/無効化もここで
    }

    private void UpdateUI()
    {
        // 配置数は PlacedMeta から都度集計（実シーンと不一致が起きない）
        int placedCount = 0;
        var metas = FindObjectsByType<PlacedMeta>(FindObjectsSortMode.None);
        if (metas != null) placedCount = metas.Count(m => m.gameObject.activeInHierarchy);

        if (placedCountText != null)
            placedCountText.text = $"設置済み: {placedCount}個";

        var pf = GetCurrentPrefab();
        if (selectedPrefabText != null)
            selectedPrefabText.text = $"選択中: {(pf != null ? pf.name : "なし")}";
    }

    private void OnExitEditMode()
    {
        Debug.Log("編集モード終了");
        // 必要ならシーン遷移やUI無効化など
    }

    private void OnSaveButtonClicked()
    {
        Debug.Log("配置確定ボタンが押されました");
        SaveCurrentScene();
        SceneManager.LoadScene("MainScene");
    }
    #endregion
    private void OnQuitButtonClicked()
    {
        Debug.Log("Quit"); // Unity のコンソールに出力
        SceneManager.LoadScene("tittle"); // ゲーム本編のシーン名に変更
    }


    #region Save / Load
    // 保存先のパスを Editor / Build で分岐
    private string SavePath
    {
        get
        {
            string fileName = string.IsNullOrEmpty(sharedString?.value) ? "anomalies.json" : sharedString.value;
            return SavePathProvider.GetSaveFilePath(fileName, "anomalies.json");
        }
    }




    private void LoadAllScenesIfExists()
    {
        try
        {
            if (!File.Exists(SavePath)) return;
            var json = File.ReadAllText(SavePath);
            if (string.IsNullOrWhiteSpace(json)) return;

            var loaded = JsonUtility.FromJson<AllScenesData>(json);
            if (loaded != null && loaded.scenes != null)
            {
                allScenesData = loaded;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"ロード失敗: {e.Message}");
            allScenesData = new AllScenesData(); // フォールバック
        }
    }

    public void SaveCurrentScene()
    {
        var metas = FindObjectsByType<PlacedMeta>(FindObjectsSortMode.None);
        var items = new List<SnapshotItem>();
        bool anomaly = false;

        foreach (var m in metas)
        {
            if (!m.gameObject.activeInHierarchy) continue;

            items.Add(new SnapshotItem
            {
                prefabId = string.IsNullOrEmpty(m.prefabId) ? m.gameObject.name.Replace("(Clone)", "") : m.prefabId,
                position = m.transform.position,
                rotation = m.transform.eulerAngles,
                isAnomaly = m.isAnomaly
            });

            anomaly |= m.isAnomaly;
        }

        // --- 🔽 ここでIDの重複をチェックし、必要なら振り直す ---
        int sceneIdToUse = currentSceneId;
        bool alreadyExists = allScenesData.scenes.Any(s => s.sceneId == sceneIdToUse);

        if (alreadyExists)
        {
            // 使用されていないIDを探す（1〜9999の中から）
            var usedIds = allScenesData.scenes.Select(s => s.sceneId).ToHashSet();
            for (int i = 1; i < 10000; i++)
            {
                if (!usedIds.Contains(i))
                {
                    sceneIdToUse = i;
                    break;
                }
            }
            Debug.Log($"SceneID {currentSceneId} は使用中のため、新しい ID {sceneIdToUse} に変更しました。");
        }

        // スナップショット作成
        var snapshot = new SceneSnapshot
        {
            sceneId = sceneIdToUse,
            anomalyHouse = anomaly,
            items = items
        };

        allScenesData.scenes.Add(snapshot);

        // JSON 書き出し
        try
        {
            var json = JsonUtility.ToJson(allScenesData, true);
            File.WriteAllText(SavePath, json);
            Debug.Log($"保存完了: {SavePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"保存失敗: {e.Message}");
        }
    }


    #endregion

}
