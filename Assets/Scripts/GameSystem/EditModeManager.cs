using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

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
    [SerializeField] private TMP_Dropdown sceneDropdown;
    [SerializeField] private TMP_Text sceneStatusText;


    [Header("保存設定")]
    [SerializeField] private int currentSceneId = 1;
    [SerializeField] private SharedString sharedString;
    [SerializeField, Min(1)] private int maxAnomaliesPerScene = 3;



    // 内部状態
    private enum Category { Anomaly, Structure }
    private Category currentCategory = Category.Anomaly;
    private int currentIndex = 0;
    private float rotationY = 0f;
    private GameObject previewInstance;
    private string previewPrefabId; // 再生成判定用
    private bool isUIActive = false;
    private bool hasUnsavedChanges = false;
    private bool suppressSceneDropdownCallback = false;
    private int selectedSceneDropdownIndex = 0;
    private int newSceneId = 1;
    private readonly List<int> sceneDropdownIds = new List<int>();
    private readonly List<SceneItemData> unresolvedItems = new List<SceneItemData>();
    private SceneDataFile allScenesData = new SceneDataFile();
    private Transform editPlayer;
    private static readonly string[] MvpAnomalyPrefabIds =
    {
        "changeColorBox",
        "anomaryShirinkBox",
        "DollPrefab",
        "bears",
        "ChairPrefab",
        "wall",
        "footstepEcho"
    };

    #region Unity Lifecycle
    private void Start()
    {
        EnsureMvpAnomalyPalette();

        // UIイベント登録
        if (saveButton != null) saveButton.onClick.AddListener(OnSaveButtonClicked);
        if (exitButton != null) exitButton.onClick.AddListener(OnExitEditMode);
        if (quitButton != null && quitButton != exitButton) quitButton.onClick.AddListener(OnQuitButtonClicked);


        // LineRenderer初期化
        if (beamLine != null)
        {
            if (beamLine.positionCount < 2) beamLine.positionCount = 2;
        }

        // 既存保存の読み込み（存在時）
        LoadAllScenesIfExists();
        var playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            editPlayer = playerObject.transform;
            if (playerObject.GetComponent<PlayerInteractionController>() == null)
            {
                playerObject.AddComponent<PlayerInteractionController>();
            }
        }
        EnsureSceneSelectorUi();
        if (sceneDropdown != null)
        {
            sceneDropdown.onValueChanged.AddListener(OnSceneDropdownChanged);
        }
        RefreshSceneDropdown(currentSceneId);
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
        // UI操作中や入力欄フォーカス中は保存・遷移を発火させない。
        if (!isUIActive && !IsTextInputFocused() && !IsSceneDropdownInteractionActive())
        {
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
        if (quitButton != null && quitButton != exitButton) quitButton.onClick.RemoveListener(OnQuitButtonClicked);
        if (sceneDropdown != null) sceneDropdown.onValueChanged.RemoveListener(OnSceneDropdownChanged);
    }
    #endregion

    private void EnsureMvpAnomalyPalette()
    {
        var merged = new List<GameObject>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var prefab in anomalyPrefabs ?? Array.Empty<GameObject>())
        {
            if (prefab != null && names.Add(prefab.name)) merged.Add(prefab);
        }

        foreach (string prefabId in MvpAnomalyPrefabIds)
        {
            GameObject prefab = PrefabResolver.Load(prefabId);
            if (prefab != null && names.Add(prefab.name)) merged.Add(prefab);
        }

        anomalyPrefabs = merged.ToArray();
    }

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

        if (currentCategory == Category.Anomaly)
        {
            var placedAnomalies = FindObjectsByType<PlacedMeta>(FindObjectsSortMode.None)
                .Where(meta => meta != null && meta.gameObject.activeInHierarchy && meta.isAnomaly)
                .ToList();
            int anomalyCount = placedAnomalies.Count;
            if (anomalyCount >= maxAnomaliesPerScene)
            {
                SetSceneStatus($"異変は最大{maxAnomaliesPerScene}個まで配置できます。", true);
                return;
            }

            bool placingAggressive = AnomalyRuntimeFactory.GetProfile(prefab.name).Aggressive;
            bool alreadyHasAggressive = placedAnomalies.Any(meta =>
                AnomalyRuntimeFactory.GetProfile(meta.prefabId).Aggressive);
            if (placingAggressive && alreadyHasAggressive)
            {
                SetSceneStatus("追跡型の異変は1ステージにつき1個まで配置できます。", true);
                return;
            }
        }

        if (!TryGetPlacement(prefab, out Vector3 pos, out Quaternion rot)) return;

        var go = Instantiate(prefab, pos, rot);
        var meta = go.GetComponent<PlacedMeta>();
        if (meta == null) meta = go.AddComponent<PlacedMeta>();

        meta.prefabId = prefab.name;
        meta.isAnomaly = (currentCategory == Category.Anomaly);
        AnomalyRuntimeFactory.Configure(
            go,
            meta.prefabId,
            meta.isAnomaly,
            gameManager: null,
            player: editPlayer,
            threatsEnabled: false);
        hasUnsavedChanges = true;

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

        if (isUIActive)
        {
            HidePreview();
        }

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

    private bool IsTextInputFocused()
    {
        var selected = EventSystem.current?.currentSelectedGameObject;
        var inputField = selected != null ? selected.GetComponentInParent<TMP_InputField>() : null;
        return inputField != null && inputField.isFocused;
    }

    private bool IsSceneDropdownInteractionActive()
    {
        if (sceneDropdown == null) return false;
        if (sceneDropdown.IsExpanded) return true;

        var selected = EventSystem.current?.currentSelectedGameObject;
        return selected != null && selected.GetComponentInParent<TMP_Dropdown>() == sceneDropdown;
    }

    private void EnsureSceneSelectorUi()
    {
        if (sceneDropdown == null)
        {
            sceneDropdown = FindObjectsByType<TMP_Dropdown>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(dropdown => dropdown.name == "SceneIdDropdown");
        }

        if (sceneStatusText == null)
        {
            sceneStatusText = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(text => text.name == "SceneStatusText");
        }

        if (sceneDropdown == null || sceneStatusText == null)
        {
            Debug.LogWarning("EditModeManager: Scene選択UIの参照が不足しています。");
        }
    }

    private void RefreshSceneDropdown(int preferredSceneId, bool loadSelection = true)
    {
        if (sceneDropdown == null) return;

        sceneDropdownIds.Clear();
        sceneDropdownIds.AddRange(allScenesData.GetSceneIds());
        newSceneId = allScenesData.GetNextAvailableSceneId();

        var options = sceneDropdownIds
            .Select(sceneId => new TMP_Dropdown.OptionData($"Scene {sceneId}"))
            .ToList();
        options.Add(new TMP_Dropdown.OptionData($"新規 Scene {newSceneId}"));

        int selectedIndex = sceneDropdownIds.IndexOf(preferredSceneId);
        if (selectedIndex < 0)
        {
            selectedIndex = sceneDropdownIds.Count > 0 ? 0 : sceneDropdownIds.Count;
        }

        suppressSceneDropdownCallback = true;
        sceneDropdown.ClearOptions();
        sceneDropdown.AddOptions(options);
        sceneDropdown.SetValueWithoutNotify(selectedIndex);
        sceneDropdown.RefreshShownValue();
        suppressSceneDropdownCallback = false;
        selectedSceneDropdownIndex = selectedIndex;

        if (loadSelection)
        {
            ApplySceneDropdownSelection(selectedIndex);
        }
    }

    private void OnSceneDropdownChanged(int index)
    {
        if (suppressSceneDropdownCallback) return;

        if (hasUnsavedChanges)
        {
            suppressSceneDropdownCallback = true;
            sceneDropdown.SetValueWithoutNotify(selectedSceneDropdownIndex);
            sceneDropdown.RefreshShownValue();
            suppressSceneDropdownCallback = false;
            SetSceneStatus("未保存の変更があります。保存してからSceneを切り替えてください。", true);
            return;
        }

        ApplySceneDropdownSelection(index);
    }

    private void ApplySceneDropdownSelection(int index)
    {
        if (index < 0 || index > sceneDropdownIds.Count) return;

        selectedSceneDropdownIndex = index;
        if (index == sceneDropdownIds.Count)
        {
            PrepareNewScene(newSceneId);
            return;
        }

        LoadSceneForEditing(sceneDropdownIds[index]);
    }

    private void PrepareNewScene(int sceneId)
    {
        ClearPlacedObjects();
        unresolvedItems.Clear();
        currentSceneId = sceneId;
        hasUnsavedChanges = false;
        SetSceneStatus($"新規 Scene {sceneId} を編集中", false);
    }

    private void LoadSceneForEditing(int sceneId)
    {
        var scene = allScenesData.GetMergedScene(sceneId);
        if (scene == null)
        {
            PrepareNewScene(sceneId);
            return;
        }

        ClearPlacedObjects();
        unresolvedItems.Clear();
        int loadedCount = 0;

        foreach (var item in scene.items ?? new List<SceneItemData>())
        {
            if (item == null) continue;

            if (!PrefabResolver.TryLoad(item.prefabId, out var prefab, out _))
            {
                unresolvedItems.Add(item.Clone());
                continue;
            }

            var instance = Instantiate(prefab, item.position, Quaternion.Euler(item.rotation));
            var meta = instance.GetComponent<PlacedMeta>();
            if (meta == null) meta = instance.AddComponent<PlacedMeta>();
            meta.prefabId = item.prefabId;
            meta.isAnomaly = item.isAnomaly;
            AnomalyRuntimeFactory.Configure(
                instance,
                item.prefabId,
                item.isAnomaly,
                gameManager: null,
                player: editPlayer,
                threatsEnabled: false);
            loadedCount++;
        }

        currentSceneId = sceneId;
        hasUnsavedChanges = false;
        string unresolvedMessage = unresolvedItems.Count > 0
            ? $" / 未解決Prefab {unresolvedItems.Count}件はデータを保持"
            : string.Empty;
        SetSceneStatus($"Scene {sceneId} を読込: {loadedCount}件{unresolvedMessage}", unresolvedItems.Count > 0);
    }

    private void ClearPlacedObjects()
    {
        HidePreview();
        var metas = FindObjectsByType<PlacedMeta>(FindObjectsSortMode.None);
        foreach (var meta in metas)
        {
            if (meta != null)
            {
                Destroy(meta.gameObject);
            }
        }
    }

    private void SetSceneStatus(string message, bool warning)
    {
        if (sceneStatusText != null)
        {
            sceneStatusText.text = message;
            sceneStatusText.color = warning ? new Color(1f, 0.65f, 0.2f) : Color.white;
        }

        if (warning) Debug.LogWarning(message);
        else Debug.Log(message);
    }

    private void OnExitEditMode()
    {
        if (hasUnsavedChanges)
        {
            SetSceneStatus("未保存の変更があります。保存してから編集を終了してください。", true);
            return;
        }

        SceneManager.LoadScene("MainScene");
    }

    private void OnSaveButtonClicked()
    {
        Debug.Log("配置確定ボタンが押されました");
        if (TrySaveCurrentScene())
        {
            SceneManager.LoadScene("MainScene");
        }
    }
    #endregion
    private void OnQuitButtonClicked()
    {
        if (hasUnsavedChanges)
        {
            SetSceneStatus("未保存の変更があります。保存してからタイトルへ戻ってください。", true);
            return;
        }

        Debug.Log("Quit"); // Unity のコンソールに出力
        SceneManager.LoadScene("tittle"); // ゲーム本編のシーン名に変更
    }


    #region Save / Load
    // 保存先のパスを Editor / Build で分岐
    private string SavePath
    {
        get
        {
            string fileName = string.IsNullOrEmpty(sharedString?.value) ? SavePathProvider.DefaultFileName : sharedString.value;
            return SavePathProvider.GetSaveFilePath(fileName);
        }
    }




    private void LoadAllScenesIfExists()
    {
        try
        {
            SavePathProvider.EnsureSaveFileWithDefaultData(
                string.IsNullOrEmpty(sharedString?.value) ? SavePathProvider.DefaultFileName : sharedString.value);

            if (!File.Exists(SavePath))
            {
                allScenesData = new SceneDataFile();
                return;
            }

            var json = File.ReadAllText(SavePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                allScenesData = new SceneDataFile();
                return;
            }

            var loaded = JsonUtility.FromJson<SceneDataFile>(json);
            if (loaded != null && loaded.scenes != null)
            {
                allScenesData = loaded;
                allScenesData.Normalize();
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"ロード失敗: {e.Message}");
            allScenesData = new SceneDataFile(); // フォールバック
        }
    }

    public void SaveCurrentScene()
    {
        TrySaveCurrentScene();
    }

    private bool TrySaveCurrentScene()
    {
        var metas = FindObjectsByType<PlacedMeta>(FindObjectsSortMode.None);
        var items = unresolvedItems.Select(item => item.Clone()).ToList();

        foreach (var m in metas)
        {
            if (!m.gameObject.activeInHierarchy) continue;

            items.Add(new SceneItemData
            {
                prefabId = string.IsNullOrEmpty(m.prefabId) ? m.gameObject.name.Replace("(Clone)", "") : m.prefabId,
                position = m.transform.position,
                rotation = m.transform.eulerAngles,
                isAnomaly = m.isAnomaly
            });
        }

        var snapshot = new SceneDataEntry
        {
            sceneId = currentSceneId,
            anomalyHouse = items.Any(item => item != null && item.isAnomaly),
            items = items
        };

        var validation = StageValidator.Validate(snapshot, maxAnomaliesPerScene);
        if (!validation.IsValid)
        {
            string errorMessage = string.Join(" / ", validation.Errors);
            SetSceneStatus($"検証失敗: {errorMessage}", true);
            return false;
        }

        string warningMessage = validation.Warnings.Count > 0
            ? $" / 警告: {string.Join(" / ", validation.Warnings)}"
            : string.Empty;

        try
        {
            // 書込失敗時に編集中データを壊さないよう、コピーへUpsertして成功後に採用する。
            var workingCopy = JsonUtility.FromJson<SceneDataFile>(JsonUtility.ToJson(allScenesData));
            workingCopy ??= new SceneDataFile();
            workingCopy.Upsert(snapshot);

            var json = JsonUtility.ToJson(workingCopy, true);
            File.WriteAllText(SavePath, json);
            allScenesData = workingCopy;
            hasUnsavedChanges = false;
            unresolvedItems.Clear();
            unresolvedItems.AddRange(items
                .Where(item => item != null && PrefabResolver.Load(item.prefabId) == null)
                .Select(item => item.Clone()));
            RefreshSceneDropdown(currentSceneId, loadSelection: false);
            SetSceneStatus($"Scene {currentSceneId} を上書き保存しました{warningMessage}", validation.Warnings.Count > 0);
            Debug.Log($"保存完了: {SavePath}");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"保存失敗: {e.Message}");
            SetSceneStatus($"保存失敗: {e.Message}", true);
            return false;
        }
    }


    #endregion

}
