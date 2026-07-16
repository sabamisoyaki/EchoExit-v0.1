// Assets/Scripts/GameManager.cs
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

public class GameManager : MonoBehaviour
{
    // ==========================
    // インスペクタ設定
    // ==========================
    [Header("プレハブ設定")]
    public GameObject goalPrefab;
    public GameObject forwardTriggerPrefab;
    public GameObject backTriggerPrefab;
    public Transform forwardTriggerPoint;
    public Transform backTriggerPoint;

    [Header("通常オブジェクト")]
    public GameObject[] normalPrefabs;
    public int normalCount = 3;
    public bool deterministicNormal = true; // SceneIDシードで決定的配置

    [Header("ローカルJSON読込")]
    [SerializeField] private SharedString sharedString; // ← SharedStringをInspectorにセット


    public enum GoalMode { ShowGoalOnly, LoadScene, PlayMovie }
    [Header("ゴール時の挙動（到達直後）")]
    public GoalMode goalMode = GoalMode.ShowGoalOnly;
    public string nextSceneName;   // GoalMode=LoadScene 用
    public VideoClip goalMovie;    // GoalMode=PlayMovie 用
    public int goalThreshold = 8; // 連続正解数
    public TMP_Text correctCountText; // ← UI 参照を追加
    public float anomalySpawnChance = 0.5f; // 異常検知の閾値（未使用）





    public enum SceneIdSource { BuildIndex, Manual, RandomFromJson }
    [Header("初期SceneIDソース")]
    public SceneIdSource sceneIdSource = SceneIdSource.BuildIndex;
    public int manualSceneId = -1; // Manualのとき使用

    [Header("SceneID -> Unityシーン名 マッピング")]
    public List<SceneBinding> sceneBindings = new List<SceneBinding>(); // 空なら buildIndex を使用
    [System.Serializable] public class SceneBinding { public int sceneId; public string sceneName; }

    [Header("ワールド生成ルート")]
    public Transform worldRoot; // 生成物の親。未設定なら自動生成
    [Header("異変検知")]
    [SerializeField] private AbnormalityPresenceDetector abnormalityDetector;
    [SerializeField] private string anomalyTagName = "Anomary";
    [Header("Debug Log")]
    [SerializeField] private bool verboseLogs = false;
    [SerializeField] private bool spawnTraceLogs = false;

    // ==========================
    // ランタイム状態
    // ==========================
    private bool hasAnomaly;
    private static int correctCount = 0;
    private bool inputLocked = true;
    private bool goalShown = false;
    private bool goalTransitionStarted = false;
    private bool isLoaded = false;

    private int currentSceneId;
    private static int? pendingSceneId = null; // ★ 追加：次ラウンド用のIDを持ち回り
    private readonly List<GameObject> spawned = new();
    private bool warnedMissingAnomalyTag = false;


    // ==========================
    // DTO（新スキーマ）
    // ==========================
    [System.Serializable] public class SceneFileDto { public SceneBlock[] scenes; }
    [System.Serializable] public class SceneBlock { public int sceneId; public bool anomalyHouse; public ItemDto[] items; }
    [System.Serializable] public class ItemDto { public string prefabId; public Vec3 position; public Vec3 rotation; public bool isAnomaly; }
    [System.Serializable] public class Vec3 { public float x, y, z; public Vector3 ToVector3() => new(x, y, z); public Quaternion ToQuaternion() => Quaternion.Euler(x, y, z); }

    // 旧形式互換（不要なら削除可）
    [System.Serializable] public class AnomalyData { public string prefabName; public Vec3 position; public Vec3 rotation; }
    [System.Serializable] public class AnomalyListWrapper { public AnomalyData[] list; }

    // キャッシュ
    private SceneFileDto cachedFile;                      // JSON全体
    private Dictionary<int, List<SceneBlock>> idToBlocks; // sceneId→blocks

    // ==========================
    // Lifecycle
    // ==========================
    private void Start()
    {


        if (!worldRoot)
        {
            var go = new GameObject("WorldRoot");
            worldRoot = go.transform;
        }

        if (abnormalityDetector == null)
        {
            abnormalityDetector = FindFirstObjectByType<AbnormalityPresenceDetector>();
        }

        if (abnormalityDetector == null)
        {
            abnormalityDetector = gameObject.AddComponent<AbnormalityPresenceDetector>();
            LogVerbose("AbnormalityPresenceDetector was missing and has been added to GameManager.");
        }

        abnormalityDetector.SetScanRoot(worldRoot);

        // JSONは必要なら読む
        if (cachedFile == null) LoadSceneDataOnce();

        // 次ラウンド用の抽選結果があれば優先し、sceneId の選択にも反映する。
        if (pendingSpawnAnomaly.HasValue)
        {
            spawnAnomalyThisRound = pendingSpawnAnomaly.Value;
            pendingSpawnAnomaly = null;
            LogVerbose($"🎛 spawnAnomalyThisRound (PENDING): {spawnAnomalyThisRound}");
        }
        else
        {
            spawnAnomalyThisRound = (Random.value < anomalySpawnChance);
            LogVerbose($"🎛 spawnAnomalyThisRound (RANDOM): {spawnAnomalyThisRound} (chance={anomalySpawnChance * 100f:F0}%)");
        }

        // ★ pendingSceneId があれば最優先で採用
        if (pendingSceneId.HasValue)
        {
            currentSceneId = pendingSceneId.Value;
            pendingSceneId = null; // 消費
            LogVerbose($"🎯 Using PENDING sceneId: {currentSceneId}");
        }
        else
        {
            if (sceneIdSource == SceneIdSource.Manual && manualSceneId >= 0)
            {
                currentSceneId = manualSceneId;
                LogVerbose($"🎯 Using MANUAL sceneId: {currentSceneId}");
            }
            else if (sceneIdSource == SceneIdSource.RandomFromJson)
            {
                currentSceneId = PickSceneIdByAnomaly(spawnAnomalyThisRound, -1);
                LogVerbose($"🎲 Using RANDOM-FROM-JSON sceneId: {currentSceneId}");
            }
            else
            {
                currentSceneId = SceneManager.GetActiveScene().buildIndex;
                LogVerbose($"🎬 Using BUILD-INDEX sceneId: {currentSceneId}");
            }
        }




        LogVerbose($"📁 persistentDataPath: {Application.persistentDataPath}");
        StartCoroutine(Boot());
        UpdateCorrectCountUI();
    }
    private bool spawnAnomalyThisRound = true;           // このラウンドで異変を出すか
    private static bool? pendingSpawnAnomaly = null;     // 次ラウンド用（リロード持ち回り）



    private IEnumerator Boot()
    {
        inputLocked = true; isLoaded = false; goalShown = false; goalTransitionStarted = false;

        if (cachedFile == null) LoadSceneDataOnce();

        BuildWorldForSceneId(currentSceneId);
        SetupEnvironment(currentSceneId);

        isLoaded = true; inputLocked = false;
        yield break;
    }
    // ==========================
    // データ読み込み（1回だけ）
    // ==========================
    private string SavePath
    {
        get
        {
            string fileName = string.IsNullOrEmpty(sharedString?.value)
                ? "anomalies.json"
                : sharedString.value;
            return SavePathProvider.GetSaveFilePath(fileName, "anomalies.json");
        }
    }
    // sceneIdの分類キャッシュ
    private List<int> anomalySceneIds = new(); // isAnomaly=true を含む sceneId
    private List<int> normalSceneIds = new(); // isAnomaly=true を一切含まない sceneId



    private void LoadSceneDataOnce()
    {
        if (!File.Exists(SavePath))
        {
            Debug.LogError($"❌ データ未検出: {SavePath}");
            cachedFile = new SceneFileDto { scenes = new SceneBlock[0] };
            idToBlocks = new();
            return;
        }

        LogVerbose($"📄 読み込み: {SavePath}");
        string raw = File.ReadAllText(SavePath, Encoding.UTF8);

        try
        {
            var t = raw.Trim();
            if (t.StartsWith("{") && t.Contains("\"scenes\""))
            {
                cachedFile = JsonUtility.FromJson<SceneFileDto>(t);
            }
            else
            {
                Debug.LogWarning("⚠ 未対応のJSON形式 → 空データ扱い");
                cachedFile = new SceneFileDto { scenes = new SceneBlock[0] };
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("JSONパース失敗: " + e.Message);
            cachedFile = new SceneFileDto { scenes = new SceneBlock[0] };
        }

        idToBlocks = new Dictionary<int, List<SceneBlock>>();
        foreach (var b in cachedFile.scenes ?? System.Array.Empty<SceneBlock>())
        {
            if (!idToBlocks.TryGetValue(b.sceneId, out var list))
            {
                list = new List<SceneBlock>();
                idToBlocks[b.sceneId] = list;
            }
            list.Add(b);
        }

        var ids = idToBlocks.Keys.ToList(); ids.Sort();
        LogVerbose($"🗺️ SceneID一覧: [{string.Join(", ", ids)}]");


        // ★ sceneIdを「異変あり/異変なし」に分類
        anomalySceneIds.Clear();
        normalSceneIds.Clear();

        foreach (var kv in idToBlocks)
        {
            int sid = kv.Key;
            bool hasAnyAnomaly = false;

            foreach (var b in kv.Value)
            {
                foreach (var it in b.items ?? System.Array.Empty<ItemDto>())
                {
                    if (it != null && it.isAnomaly)
                    {
                        hasAnyAnomaly = true;
                        break;
                    }
                }
                if (hasAnyAnomaly) break;
            }

            if (hasAnyAnomaly) anomalySceneIds.Add(sid);
            else normalSceneIds.Add(sid);
        }

        anomalySceneIds.Sort();
        normalSceneIds.Sort();

        LogVerbose($"🧪 Anomaly SceneIDs: [{string.Join(", ", anomalySceneIds)}]");
        LogVerbose($"🧼 Normal  SceneIDs: [{string.Join(", ", normalSceneIds)}]");

    }

    private int PickSceneIdByAnomaly(bool wantAnomaly, int excludeSceneId)
    {
        var pool = wantAnomaly ? anomalySceneIds : normalSceneIds;

        // excludeを外す（候補が複数ある時だけ）
        List<int> candidates = pool;
        if (excludeSceneId >= 0 && pool.Count > 1 && pool.Contains(excludeSceneId))
        {
            candidates = pool.Where(id => id != excludeSceneId).ToList();
        }

        if (candidates.Count > 0)
        {
            int pick = candidates[Random.Range(0, candidates.Count)];
            LogVerbose($"🎯 PickSceneIdByAnomaly wantAnomaly={wantAnomaly} -> {pick}");
            return pick;
        }

        // フォールバック：逆側に候補があるなら逆側から取る
        var alt = wantAnomaly ? normalSceneIds : anomalySceneIds;
        if (alt.Count > 0)
        {
            int pick = alt[Random.Range(0, alt.Count)];
            Debug.LogWarning($"⚠ 候補不足：wantAnomaly={wantAnomaly} の候補が0。逆側から {pick} を使用");
            return pick;
        }

        // 最終フォールバック：何もない
        Debug.LogError("🚨 JSONに sceneId 候補が存在しません（両方0）");
        return SceneManager.GetActiveScene().buildIndex;
    }





    // ==========================
    // ワールド構築/破棄
    // ==========================
    private void ClearWorld()
    {
        foreach (var go in spawned) if (go) Destroy(go);
        spawned.Clear();
    }

    private void BuildWorldForSceneId(int sceneId)
    {
        ClearWorld();

        bool matchedAny = false;
        bool spawnedAnyAnomaly = false;
        int placed = 0;

        if (idToBlocks != null && idToBlocks.TryGetValue(sceneId, out var blocks))
        {
            matchedAny = true;

            LogVerbose($"🎲 sceneId={sceneId} spawnAnomalyThisRound={(spawnAnomalyThisRound ? "ON" : "OFF")}");

            foreach (var b in blocks)
            {
                foreach (var it in b.items ?? System.Array.Empty<ItemDto>())
                {
                    if (it == null) continue;

                    // ★ このラウンドは異変OFFなら、異変アイテムは一切置かない
                    // DEBUG: 何を出そうとしているか
                    LogSpawn($"[SPAWN?] roundAnomaly={spawnAnomalyThisRound} sceneId={sceneId} prefabId={it.prefabId} isAnomaly={it.isAnomaly}");

                    if (it.isAnomaly && !spawnAnomalyThisRound)
                    {
                        LogSpawn($"[SKIP ] prefabId={it.prefabId} (isAnomaly=true but round OFF)");
                        continue;
                    }

                    if (!SpawnItem(it))
                    {
                        continue;
                    }

                    LogSpawn($"[SPAWN] prefabId={it.prefabId} isAnomaly={it.isAnomaly}");

                    placed++;

                    if (it.isAnomaly) spawnedAnyAnomaly = true;
                }
            }
        }
        else
        {
            Debug.LogWarning($"⚠ sceneId={sceneId} に対応するJSONブロックがありません");
        }

        // まずは生成結果ベースで仮設定し、最後に検知器で確定する。
        hasAnomaly = spawnAnomalyThisRound && spawnedAnyAnomaly;

        if (abnormalityDetector != null)
        {
            hasAnomaly = abnormalityDetector.ScanNow();
        }

        LogVerbose($"🧩 Build sceneId={sceneId} matched={matchedAny} placed={placed} hasAnomaly(thisRound)={hasAnomaly}");
    }





    private void SetupEnvironment(int sceneId)
    {
        if (correctCount >= goalThreshold)
        {
            ShowGoal();
        }

        if (forwardTriggerPrefab && forwardTriggerPoint)
        {
            var t = Instantiate(forwardTriggerPrefab, forwardTriggerPoint.position, forwardTriggerPoint.rotation, worldRoot);
            spawned.Add(t);
        }
        if (backTriggerPrefab && backTriggerPoint)
        {
            var t = Instantiate(backTriggerPrefab, backTriggerPoint.position, backTriggerPoint.rotation, worldRoot);
            spawned.Add(t);
        }

        if (normalPrefabs != null && normalPrefabs.Length > 0 && normalCount > 0)
        {
            var saved = Random.state;
            if (deterministicNormal) Random.InitState(sceneId);

            for (int i = 0; i < normalCount; i++)
            {
                int idx = Random.Range(0, normalPrefabs.Length);
                float x = Random.Range(-2f, 2f);
                float z = Random.Range(1f, 4f);
                var o = Instantiate(normalPrefabs[idx], new Vector3(x, 0.5f, z), Quaternion.identity, worldRoot);
                spawned.Add(o);
            }

            if (deterministicNormal) Random.state = saved;
        }
    }

    // ==========================
    // 入力（前進/後退）
    // ==========================
    public void PlayerChose(bool goForward)
    {
        if (inputLocked || !isLoaded) { LogVerbose("⌛ 入力不可"); return; }
        if (goalShown) { LogVerbose("🏁 ゴール状態"); return; }

        inputLocked = true; // ← 早めにロックして連打による多重呼び出しを防止

        // 判定直前に実シーンを再スキャンして、hasAnomaly を最新化。
        if (abnormalityDetector != null)
        {
            hasAnomaly = abnormalityDetector.ScanNow();
        }

        bool isCorrect = (!hasAnomaly && goForward) || (hasAnomaly && !goForward);

        if (isCorrect)
        {
            correctCount++;
            LogVerbose($"✅ 正解！現在の正解数: {correctCount}");
        }
        else
        {
            correctCount = 0;
            LogVerbose("❌ 不正解！正解数リセット");
        }

        UpdateCorrectCountUI();

        // ここでまずゴール判定。到達時は専用シーンへ遷移（この時点では再ロードしない）
        if (correctCount >= goalThreshold)
        {
            HandleGoalReached();
            return;
        }

        // 次ラウンドの異変有無と一致する sceneId を選び、リロード先へ持ち回る。
        bool wantAnomalyNext = (Random.value < anomalySpawnChance);
        int nextId = PickSceneIdByAnomaly(wantAnomalyNext, currentSceneId);
        pendingSpawnAnomaly = wantAnomalyNext;
        LogVerbose(wantAnomalyNext
            ? "🧪 Next round: anomaly ON"
            : "🧼 Next round: anomaly OFF");

        pendingSceneId = nextId;

        var curName = SceneManager.GetActiveScene().name;
        LogVerbose($"➡ 次ラウンド sceneId={nextId} spawnAnomaly={pendingSpawnAnomaly} → \"{curName}\" を再ロード");
        SceneManager.LoadScene(curName, LoadSceneMode.Single);

    }

    private void SafeLoadSceneByName(int sceneId, string sceneName)
    {
        try
        {
            LogVerbose($"➡ 正解：SceneID {sceneId} → UnityScene \"{sceneName}\" をロード（名前指定）");
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"❌ シーン名ロード失敗: id={sceneId}, name='{sceneName}', msg={ex.Message}");
            // 最終フォールバック：現行シーン
            try
            {
                var cur = SceneManager.GetActiveScene().name;
                Debug.LogWarning($"↩ フォールバックとして現行シーンを再ロード: {cur}");
                SceneManager.LoadScene(cur, LoadSceneMode.Single);
            }
            catch (System.Exception exFallback)
            {
                Debug.LogError($"🚨 フォールバックも失敗: {exFallback.Message}");
            }
        }
    }
    private void UpdateCorrectCountUI()
    {
        if (correctCountText != null)
        {
            correctCountText.text = $"Now {correctCount}/{goalThreshold}";
        }
    }


    // ==========================
    // ゴール
    // ==========================
    private void HandleGoalReached()
    {
        inputLocked = true;

        switch (goalMode)
        {
            case GoalMode.ShowGoalOnly:
                if (!ShowGoal())
                {
                    Debug.LogError("GameManager: goalPrefab is missing. Falling back to endTitle.");
                    CompleteGoal();
                }
                break;

            case GoalMode.LoadScene:
                CompleteGoal(string.IsNullOrWhiteSpace(nextSceneName) ? "endTitle" : nextSceneName);
                break;

            case GoalMode.PlayMovie:
                if (goalMovie == null)
                {
                    Debug.LogError("GameManager: goalMovie is missing. Falling back to endTitle.");
                    CompleteGoal();
                    break;
                }

                PlayGoalMovie();
                break;
        }
    }

    private bool ShowGoal()
    {
        if (goalShown) return true;
        if (goalPrefab == null) return false;

        var goal = Instantiate(goalPrefab, new Vector3(0, 0.5f, 6), Quaternion.identity, worldRoot);
        spawned.Add(goal);
        goalShown = true;
        inputLocked = true;
        LogVerbose("🏁 ゴール出現（達成済）");
        return true;
    }

    private void PlayGoalMovie()
    {
        var player = GetComponent<VideoPlayer>();
        if (player == null)
        {
            player = gameObject.AddComponent<VideoPlayer>();
        }

        player.playOnAwake = false;
        player.isLooping = false;
        player.clip = goalMovie;
        player.renderMode = VideoRenderMode.CameraNearPlane;
        player.targetCamera = Camera.main;
        player.loopPointReached -= OnGoalMovieFinished;
        player.loopPointReached += OnGoalMovieFinished;
        player.errorReceived -= OnGoalMovieError;
        player.errorReceived += OnGoalMovieError;
        player.Play();
    }

    private void OnGoalMovieFinished(VideoPlayer player)
    {
        player.loopPointReached -= OnGoalMovieFinished;
        player.errorReceived -= OnGoalMovieError;
        CompleteGoal();
    }

    private void OnGoalMovieError(VideoPlayer player, string message)
    {
        player.loopPointReached -= OnGoalMovieFinished;
        player.errorReceived -= OnGoalMovieError;
        Debug.LogError($"GameManager: goal movie playback failed: {message}");
        CompleteGoal();
    }

    public void CompleteGoal()
    {
        CompleteGoal("endTitle");
    }

    private void CompleteGoal(string sceneName)
    {
        if (goalTransitionStarted) return;

        goalTransitionStarted = true;
        inputLocked = true;
        correctCount = 0;
        pendingSceneId = null;
        pendingSpawnAnomaly = null;
        UpdateCorrectCountUI();
        LogVerbose($"🎉 ゴール完了：{sceneName}シーンへ遷移");
        SafeLoadSceneByName(currentSceneId, sceneName);
    }




    // ==========================
    // ユーティリティ
    // ==========================
    private bool SpawnItem(ItemDto item)
    {
        if (item == null || string.IsNullOrEmpty(item.prefabId)) return false;

        string path = ResolvePrefabPath(item.prefabId);
        var prefab = Resources.Load<GameObject>(path);
        if (!prefab) { Debug.LogWarning($"Prefab not found: {path}"); return false; }

        var pos = (item.position != null) ? item.position.ToVector3() : Vector3.zero;
        var rot = (item.rotation != null) ? item.rotation.ToQuaternion() : Quaternion.identity;

        var go = Instantiate(prefab, pos, rot, worldRoot);
        ApplyAnomalyTag(go, item.isAnomaly);
        spawned.Add(go);
        return true;
    }

    private string ResolvePrefabPath(string prefabId)
    {
        switch (prefabId)
        {
            case "anomaryShirinkBox": return "Prefabs/Abnormalities/anomaryShirinkBox";
            case "bears": return "Prefabs/Abnormalities/bears";
            case "changeColorBox": return "Prefabs/Abnormalities/changeColorBox";
            default: return $"Prefabs/Abnormalities/{prefabId}";
        }
    }

    private void LogVerbose(string message)
    {
        if (verboseLogs) Debug.Log(message);
    }

    private void LogSpawn(string message)
    {
        if (spawnTraceLogs) Debug.Log(message);
    }

    private void ApplyAnomalyTag(GameObject go, bool isAnomaly)
    {
        if (go == null)
        {
            return;
        }

        try
        {
            go.tag = isAnomaly ? anomalyTagName : "Untagged";
        }
        catch (UnityException)
        {
            if (isAnomaly && !warnedMissingAnomalyTag)
            {
                warnedMissingAnomalyTag = true;
                Debug.LogWarning($"GameManager: tag '{anomalyTagName}' is not defined in Tags and Layers.");
            }
        }
    }

}
