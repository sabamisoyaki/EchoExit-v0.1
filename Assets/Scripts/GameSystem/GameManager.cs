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
    public int goalThreshold = 6; // 連続正解数
    public TMP_Text correctCountText; // ← UI 参照を追加
    [Range(0f, 1f)] public float anomalySpawnChance = 0.666f; // 各ラウンドで異変を出す確率

    [Header("MVPラウンド設定")]
    [SerializeField, Min(1)] private int maxAnomaliesPerRound = 3;
    [SerializeField, Min(10f)] private float roundTimeLimitSeconds = 180f;
    [SerializeField] private string gameOverSceneName = "endTitle";

    [Header("正誤フィードバック")]
    [SerializeField] private RoundFeedbackPresenter roundFeedbackPresenter;
    [SerializeField] private Behaviour playerMovementBehaviour;





    public enum SceneIdSource { BuildIndex, Manual, RandomFromJson }
    [Header("初期SceneIDソース")]
    public SceneIdSource sceneIdSource = SceneIdSource.RandomFromJson;
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
    private bool roundEnding = false;
    private float roundTimeRemaining;
    private Transform playerTransform;

    public bool IsRoundActive => isLoaded && !inputLocked && !goalShown && !roundEnding;

    private int currentSceneId;
    private static int? pendingSceneId = null; // ★ 追加：次ラウンド用のIDを持ち回り
    private readonly List<GameObject> spawned = new();
    private bool warnedMissingAnomalyTag = false;


    // キャッシュ
    private SceneDataFile cachedFile;                          // JSON全体
    private Dictionary<int, List<SceneDataEntry>> idToBlocks; // sceneId→blocks

    // ==========================
    // Lifecycle
    // ==========================
    private void Start()
    {
        // pending が無い＝ラウンド持ち回りではない（タイトルからの新規プレイ等）。
        // static な correctCount が前回プレイの値を持ち越さないようリセットする。
        if (!pendingSceneId.HasValue && !pendingSpawnAnomaly.HasValue)
        {
            correctCount = 0;
            GameSessionState.Reset();
        }

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

        if (roundFeedbackPresenter == null)
        {
            roundFeedbackPresenter = GetComponent<RoundFeedbackPresenter>();
            if (roundFeedbackPresenter == null)
            {
                roundFeedbackPresenter = gameObject.AddComponent<RoundFeedbackPresenter>();
            }
        }

        if (playerMovementBehaviour == null)
        {
            playerMovementBehaviour = FindFirstObjectByType<PlayerMovement>();
        }

        var playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            playerTransform = playerObject.transform;
            var firstPersonController = playerObject.GetComponent<MvpFirstPersonController>();
            if (firstPersonController == null)
            {
                firstPersonController = playerObject.AddComponent<MvpFirstPersonController>();
            }
            playerMovementBehaviour = firstPersonController;

            if (playerObject.GetComponent<PlayerInteractionController>() == null)
            {
                playerObject.AddComponent<PlayerInteractionController>();
            }
        }

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
                var selection = PickSceneByAnomaly(spawnAnomalyThisRound, -1);
                if (selection.IsValid)
                {
                    currentSceneId = selection.SceneId;
                    spawnAnomalyThisRound = selection.HasAnomaly;
                    LogVerbose($"🎲 Using RANDOM-FROM-JSON sceneId: {currentSceneId}");
                }
                else
                {
                    currentSceneId = SceneManager.GetActiveScene().buildIndex;
                    spawnAnomalyThisRound = false;
                }
            }
            else
            {
                currentSceneId = SceneManager.GetActiveScene().buildIndex;
                LogVerbose($"🎬 Using BUILD-INDEX sceneId: {currentSceneId}");

                // buildIndex は EditMode の保存 ID（1始まりの連番）と一致しないことが多い。
                // JSON に該当ブロックが無ければ抽選にフォールバックしてラウンドを成立させる。
                if (idToBlocks != null && idToBlocks.Count > 0 && !idToBlocks.ContainsKey(currentSceneId))
                {
                    var selection = PickSceneByAnomaly(spawnAnomalyThisRound, -1);
                    if (selection.IsValid)
                    {
                        Debug.LogWarning($"⚠ buildIndex={currentSceneId} は JSON に存在しないため sceneId={selection.SceneId} にフォールバックします");
                        currentSceneId = selection.SceneId;
                        spawnAnomalyThisRound = selection.HasAnomaly;
                    }
                }
            }
        }




        LogVerbose($"📁 persistentDataPath: {Application.persistentDataPath}");
        StartCoroutine(Boot());
        UpdateCorrectCountUI();
    }

    private void Update()
    {
        if (!IsRoundActive || roundTimeLimitSeconds <= 0f) return;

        roundTimeRemaining = Mathf.Max(0f, roundTimeRemaining - Time.deltaTime);
        roundFeedbackPresenter?.SetRoundTimer(roundTimeRemaining);
        if (roundTimeRemaining <= 0f)
        {
            StartCoroutine(EndRun(GameEndingKind.TimeExpired, "時間切れ", "制限時間を超えた"));
        }
    }
    private bool spawnAnomalyThisRound = true;           // このラウンドで異変を出すか
    private static bool? pendingSpawnAnomaly = null;     // 次ラウンド用（リロード持ち回り）



    private IEnumerator Boot()
    {
        inputLocked = true; isLoaded = false; goalShown = false; goalTransitionStarted = false; roundEnding = false;

        if (cachedFile == null) LoadSceneDataOnce();

        BuildWorldForSceneId(currentSceneId);
        SetupEnvironment(currentSceneId);

        roundTimeRemaining = roundTimeLimitSeconds;
        roundFeedbackPresenter?.SetRoundTimer(roundTimeRemaining);
        isLoaded = true; inputLocked = false;
        yield break;
    }
    // ==========================
    // データ読み込み（1回だけ）
    // ==========================
    private string SaveFileName => string.IsNullOrEmpty(sharedString?.value)
        ? SavePathProvider.DefaultFileName
        : sharedString.value;

    private string SavePath => SavePathProvider.GetSaveFilePath(SaveFileName);
    // sceneIdの分類キャッシュ
    private List<int> anomalySceneIds = new(); // isAnomaly=true を含む sceneId
    private List<int> normalSceneIds = new(); // isAnomaly=true を一切含まない sceneId



    private void LoadSceneDataOnce()
    {
        // 初回起動などでファイルが無い/空の場合はデフォルトデータを展開する
        SavePathProvider.EnsureSaveFileWithDefaultData(SaveFileName);

        if (!File.Exists(SavePath))
        {
            Debug.LogError($"❌ データ未検出: {SavePath}");
            cachedFile = new SceneDataFile();
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
                cachedFile = JsonUtility.FromJson<SceneDataFile>(t);
            }
            else
            {
                Debug.LogWarning("⚠ 未対応のJSON形式 → 空データ扱い");
                cachedFile = new SceneDataFile();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("JSONパース失敗: " + e.Message);
            cachedFile = new SceneDataFile();
        }

        cachedFile ??= new SceneDataFile();
        cachedFile.Normalize();

        idToBlocks = new Dictionary<int, List<SceneDataEntry>>();
        foreach (var b in cachedFile.scenes)
        {
            if (b == null) continue;
            if (!idToBlocks.TryGetValue(b.sceneId, out var list))
            {
                list = new List<SceneDataEntry>();
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
                foreach (var it in b.items ?? new List<SceneItemData>())
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

    private RoundSelection PickSceneByAnomaly(bool wantAnomaly, int excludeSceneId)
    {
        var selection = RoundSelectionUtility.Pick(
            anomalySceneIds,
            normalSceneIds,
            wantAnomaly,
            excludeSceneId,
            count => Random.Range(0, count));

        if (!selection.IsValid)
        {
            Debug.LogError("🚨 JSONに sceneId 候補が存在しません（両方0）");
            return selection;
        }

        if (selection.UsedFallback)
        {
            Debug.LogWarning(
                $"⚠ 候補不足：wantAnomaly={wantAnomaly} の候補が0。" +
                $"逆側から sceneId={selection.SceneId} (hasAnomaly={selection.HasAnomaly}) を使用");
        }
        else
        {
            LogVerbose($"🎯 PickSceneByAnomaly wantAnomaly={wantAnomaly} -> {selection.SceneId}");
        }

        return selection;
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
        int spawnedAnomalyCount = 0;
        int placed = 0;

        if (idToBlocks != null && idToBlocks.TryGetValue(sceneId, out var blocks))
        {
            matchedAny = true;

            LogVerbose($"🎲 sceneId={sceneId} spawnAnomalyThisRound={(spawnAnomalyThisRound ? "ON" : "OFF")}");

            foreach (var b in blocks)
            {
                foreach (var it in b.items ?? new List<SceneItemData>())
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

                    if (it.isAnomaly && spawnedAnomalyCount >= maxAnomaliesPerRound)
                    {
                        Debug.LogWarning(
                            $"sceneId={sceneId}: 異変上限{maxAnomaliesPerRound}個を超えたため '{it.prefabId}' をスキップします");
                        continue;
                    }

                    if (!SpawnItem(it))
                    {
                        continue;
                    }

                    LogSpawn($"[SPAWN] prefabId={it.prefabId} isAnomaly={it.isAnomaly}");

                    placed++;

                    if (it.isAnomaly)
                    {
                        spawnedAnyAnomaly = true;
                        spawnedAnomalyCount++;
                    }
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
            ConfigureDecisionDoor(t);
            spawned.Add(t);
        }
        if (backTriggerPrefab && backTriggerPoint)
        {
            var t = Instantiate(backTriggerPrefab, backTriggerPoint.position, backTriggerPoint.rotation, worldRoot);
            ConfigureDecisionDoor(t);
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
        StartCoroutine(ResolveChoiceAfterFeedback(isCorrect));
    }

    public void NotifyAnomalyRecognized(AnomalyRitualController ritual)
    {
        if (ritual == null || roundEnding) return;

        string message = ritual.IsAggressive
            ? $"{ritual.DisplayName}を認識した\n出口へ逃げろ"
            : $"{ritual.DisplayName}を認識した";
        StartCoroutine(roundFeedbackPresenter != null
            ? roundFeedbackPresenter.ShowAnnouncement(message, ritual.IsAggressive)
            : EmptyRoutine());
    }

    public void PlayerCaught(string anomalyName)
    {
        if (roundEnding) return;
        StartCoroutine(EndRun(
            GameEndingKind.Caught,
            "異変に捕まった",
            string.IsNullOrWhiteSpace(anomalyName) ? "正体不明の異変" : anomalyName));
    }

    private IEnumerator EndRun(GameEndingKind ending, string message, string detail)
    {
        if (roundEnding) yield break;

        roundEnding = true;
        inputLocked = true;
        correctCount = 0;
        pendingSceneId = null;
        pendingSpawnAnomaly = null;
        UpdateCorrectCountUI();
        GameSessionState.SetEnding(ending, detail);

        if (playerMovementBehaviour != null)
        {
            playerMovementBehaviour.enabled = false;
        }

        if (roundFeedbackPresenter != null)
        {
            yield return roundFeedbackPresenter.ShowAnnouncement(message, isDanger: true, duration: 1.5f);
        }

        SafeLoadSceneByName(currentSceneId, gameOverSceneName);
    }

    private static IEnumerator EmptyRoutine()
    {
        yield break;
    }

    private IEnumerator ResolveChoiceAfterFeedback(bool isCorrect)
    {
        bool restoreMovement = playerMovementBehaviour != null && playerMovementBehaviour.enabled;
        if (restoreMovement)
        {
            playerMovementBehaviour.enabled = false;
        }

        if (roundFeedbackPresenter != null)
        {
            yield return roundFeedbackPresenter.ShowResult(isCorrect, correctCount, goalThreshold);
        }
        else
        {
            Debug.LogWarning("GameManager: RoundFeedbackPresenter が未設定のため表示をスキップします。");
        }

        if (restoreMovement && playerMovementBehaviour != null)
        {
            playerMovementBehaviour.enabled = true;
        }

        // 最終正解のフィードバックを見せた後でゴール処理へ進む。
        if (correctCount >= goalThreshold)
        {
            HandleGoalReached();
            yield break;
        }

        // 次ラウンドの実際の分類と一致する sceneId / 異変フラグを持ち回る。
        bool wantAnomalyNext = (Random.value < anomalySpawnChance);
        var selection = PickSceneByAnomaly(wantAnomalyNext, currentSceneId);
        if (!selection.IsValid)
        {
            bool currentHasAnomaly = anomalySceneIds.Contains(currentSceneId);
            selection = new RoundSelection(currentSceneId, currentHasAnomaly, usedFallback: true, isValid: true);
            Debug.LogWarning($"⚠ 次ラウンド候補がないため現在の sceneId={currentSceneId} を再利用します");
        }

        pendingSpawnAnomaly = selection.HasAnomaly;
        LogVerbose(selection.HasAnomaly
            ? "🧪 Next round: anomaly ON"
            : "🧼 Next round: anomaly OFF");

        pendingSceneId = selection.SceneId;

        var curName = SceneManager.GetActiveScene().name;
        LogVerbose($"➡ 次ラウンド sceneId={selection.SceneId} spawnAnomaly={pendingSpawnAnomaly} → \"{curName}\" を再ロード");
        SceneManager.LoadScene(curName, LoadSceneMode.Single);
    }

    private void SafeLoadSceneByName(int sceneId, string sceneName)
    {
        // LoadScene は存在しないシーン名でも例外を投げない（エラーログのみ）ため、
        // 事前にロード可否を確認してフォールバックする。
        if (Application.CanStreamedLevelBeLoaded(sceneName))
        {
            LogVerbose($"➡ SceneID {sceneId} → UnityScene \"{sceneName}\" をロード（名前指定）");
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
            return;
        }

        Debug.LogError($"❌ シーン '{sceneName}' はビルド設定に存在しません (sceneId={sceneId})");

        if (sceneName != "endTitle" && Application.CanStreamedLevelBeLoaded("endTitle"))
        {
            Debug.LogWarning("↩ フォールバックとして endTitle をロード");
            SceneManager.LoadScene("endTitle", LoadSceneMode.Single);
            return;
        }

        var cur = SceneManager.GetActiveScene().name;
        Debug.LogWarning($"↩ フォールバックとして現行シーンを再ロード: {cur}");
        SceneManager.LoadScene(cur, LoadSceneMode.Single);
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
        ConfigureDecisionDoor(goal);
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
        GameSessionState.SetEnding(GameEndingKind.Escaped, "6回の判断に成功した");
        UpdateCorrectCountUI();
        LogVerbose($"🎉 ゴール完了：{sceneName}シーンへ遷移");
        SafeLoadSceneByName(currentSceneId, sceneName);
    }




    // ==========================
    // ユーティリティ
    // ==========================
    private bool SpawnItem(SceneItemData item)
    {
        if (item == null || string.IsNullOrEmpty(item.prefabId)) return false;

        if (!PrefabResolver.TryLoad(item.prefabId, out var prefab, out _))
        {
            Debug.LogWarning($"Prefab not found: {item.prefabId}");
            return false;
        }

        var pos = item.position;
        var rot = Quaternion.Euler(item.rotation);

        var go = Instantiate(prefab, pos, rot, worldRoot);
        ApplyAnomalyTag(go, item.isAnomaly);
        AnomalyRuntimeFactory.Configure(
            go,
            item.prefabId,
            item.isAnomaly,
            this,
            playerTransform,
            threatsEnabled: true);
        spawned.Add(go);
        return true;
    }

    private void LogVerbose(string message)
    {
        if (verboseLogs) Debug.Log(message);
    }

    private void LogSpawn(string message)
    {
        if (spawnTraceLogs) Debug.Log(message);
    }

    private static void ConfigureDecisionDoor(GameObject door)
    {
        if (door == null) return;

        foreach (var colliderComponent in door.GetComponentsInChildren<Collider>(true))
        {
            colliderComponent.isTrigger = false;
        }
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
