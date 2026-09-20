using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Door666.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Door666.Runtime
{
    public enum GameScreen { Title, Playing, Paused, Judging, Editing, Ending, Error }

    /// <summary>
    /// Entry point of one screen scene (Title / Game / EditMode). Services are created per scene and nothing is carried
    /// across a scene change: settings and stages are reloaded, and run state lives only in the Game scene.
    /// </summary>
    public abstract class SceneController : MonoBehaviour
    {
        public GameScreen Screen
        {
            get => screen.GetValueOrDefault();
            protected set
            {
                if (screen == value) return;
                GameLog.Info("画面", (screen.HasValue ? screen + " → " : "") + value);
                screen = value;
            }
        }
        /// <summary>True once the field scene is loaded and the screen has been entered.</summary>
        public bool IsReady { get; private set; }
        public GameSettings Settings { get; private set; }
        public PlayerInputReader Input { get; private set; }
        public FirstPersonRig Player { get; private set; }
        public GameUI UI { get; private set; }
        public WorldBuilder World { get; private set; }
        public StageRepository Repository { get; private set; }
        public AnomalyCatalog Definitions { get; private set; }
        public bool SubtitlesEnabled { get; set; }

        [Tooltip("The first-person rig placed in this scene from Assets/Prefabs/Player.prefab.")]
        [SerializeField] private FirstPersonRig player;
        [Tooltip("The interface placed in this scene from Assets/Prefabs/UI/Interface.prefab.")]
        [SerializeField] private GameUI ui;

        private GameScreen? screen;
        private StageLoadResult loadedStages;
        private AudioClip ambienceClip;
        private int startupWarnings;
        private int startupErrors;

        protected virtual void Awake()
        {
            if (player == null)
                throw new System.InvalidOperationException(gameObject.scene.name + " にプレイヤーが配置されていません。メニュー「666号扉 → プロジェクトを初期化」を実行してください。");
            if (ui == null)
                throw new System.InvalidOperationException(gameObject.scene.name + " に UI（Interface.prefab）が配置されていません。メニュー「666号扉 → プロジェクトを初期化」を実行してください。");
            // Every warning and error until the screen is entered counts toward the start-up summary.
            Application.logMessageReceived += CountStartupProblem;
            Settings = Resources.Load<GameSettings>(GameConstants.SettingsResource);
            GameLog.Verbose = Settings != null && Settings.verboseLogging;
            if (Settings == null)
            {
                GameLog.Warning("起動", "Resources/" + GameConstants.SettingsResource + ".asset がないため、調整値は初期値で動かします。");
                Settings = ScriptableObject.CreateInstance<GameSettings>();
            }
            Definitions = AnomalyCatalog.FromJson(Resources.Load<TextAsset>(GameConstants.DefinitionsResource).text);
            Input = new PlayerInputReader();
            Player = player;
            Player.Initialize();
            UI = ui;
            UI.Initialize(this);
            Repository = new StageRepository(Application.persistentDataPath);
            SubtitlesEnabled = PlayerPrefs.GetInt(GameConstants.SubtitlePreference, 1) != 0;
            loadedStages = Repository.LoadOrCreate(Resources.Load<TextAsset>(GameConstants.DefaultStagesResource).text);
            foreach (string warning in loadedStages.Warnings) GameLog.Warning("起動", warning);
            var sounds = SoundLibrary.Load();
            CreateAmbience(sounds);
            GameLog.Info("起動", gameObject.scene.name + " を起動: 異変定義 " + Definitions.Definitions.Count + " 種、ステージ " + Repository.Data.scenes.Count + " 件"
                + (loadedStages.CreatedDefaults ? "（保存ファイルがないため初期データから作成）" : "") + "、保存先 " + Repository.SavePath
                + "、" + DescribeSounds(sounds) + (GameLog.Verbose ? "、詳細ログ オン" : ""));
        }

        private IEnumerator Start()
        {
            yield return FieldRoot.EnsureLoaded();
            var field = FindFirstObjectByType<FieldRoot>();
            if (field == null)
                ShowError("部屋のシーン「" + GameConstants.FieldScene + "」を読み込めませんでした。");
            else
            {
                ReportFieldProblems(field);
                World = new WorldBuilder(field, Definitions);
                ReportDefinitionProblems();
                if (loadedStages.Success) Enter();
                else ShowError(loadedStages.Error + "\n既存ファイルは変更していません。\n" + Repository.SavePath);
            }
            IsReady = true;
            Application.logMessageReceived -= CountStartupProblem;
            string summary = gameObject.scene.name + " の起動完了（画面: " + Screen + "）: ";
            if (startupWarnings == 0 && startupErrors == 0) GameLog.Info("起動", summary + "警告・エラーなし");
            else GameLog.Warning("起動", summary + "起動中に警告 " + startupWarnings + " 件・エラー " + startupErrors + " 件が出ています。上のログを確認してください。");
        }

        /// <summary>Called once the shared field is available.</summary>
        protected abstract void Enter();

        public virtual void ShowTitle() => LoadScreen(GameConstants.TitleScene);
        public virtual void StartRun() => LoadScreen(GameConstants.GameScene);
        public virtual void StartEditor() => LoadScreen(GameConstants.EditModeScene);
        public virtual void Resume() { }

        public void Quit()
        {
            GameLog.Info("画面", "ゲームを終了します。");
            PlayerPrefs.Save();
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        protected void ShowError(string message)
        {
            GameLog.Error("画面", "エラー画面を表示: " + message.Replace("\n", " / "), this);
            Screen = GameScreen.Error;
            SetCursor(true);
            UI.ShowError(message);
        }

        // A single-mode load also unloads the additive field, so the next screen starts from a clean room.
        private void LoadScreen(string scene)
        {
            GameLog.Info("画面", "シーン移動: " + gameObject.scene.name + " → " + scene);
            SceneManager.LoadScene(scene, LoadSceneMode.Single);
        }

        protected static void SetCursor(bool free)
        {
            Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = free;
        }

        protected virtual void OnDestroy()
        {
            Application.logMessageReceived -= CountStartupProblem;
            if (Input != null) Input.Dispose();
            if (World != null) World.Dispose();
            if (ambienceClip != null) Destroy(ambienceClip);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void CountStartupProblem(string message, string stackTrace, LogType type)
        {
            if (type == LogType.Warning) startupWarnings++;
            else if (type != LogType.Log) startupErrors++;
        }

        private static void ReportFieldProblems(FieldRoot field)
        {
            if (field.Placements == null) GameLog.Warning("起動", "FieldRoot の Placements が空です。配置物が部屋の外に置かれます。", field);
            if (field.ForwardDoor == null || field.BackDoor == null) GameLog.Warning("起動", "FieldRoot の扉（ForwardDoor / BackDoor）が割り当てられていません。", field);
            if (field.Navigation == null) GameLog.Warning("起動", "FieldRoot の Navigation が空です。追跡型の異変が動けません。", field);
        }

        /// <summary>Catches typos in AnomalyDefinitions.json: a prefabId with no visual, or a sound the library does not know.</summary>
        private void ReportDefinitionProblems()
        {
            var knownSounds = new HashSet<string>(SoundLibrary.KnownSounds.Select(sound => sound.Key));
            foreach (var definition in Definitions.Definitions)
            {
                string name = definition.anomalyId + " " + definition.displayName;
                if (!World.Catalog.IsKnown(definition.prefabId))
                    GameLog.Warning("起動", name + ": prefabId「" + definition.prefabId + "」の見た目が ObjectCatalog にありません。置いても表示されません。");
                foreach (string sound in new[] { definition.clue?.sound, definition.recognitionEffect?.sound })
                    if (!string.IsNullOrEmpty(sound) && !knownSounds.Contains(sound))
                        GameLog.Warning("起動", name + ": 音「" + sound + "」が SoundLibrary.KnownSounds にありません。既定の音で鳴ります。");
            }
        }

        private static string DescribeSounds(SoundLibrary sounds)
        {
            if (sounds == null) return "音はすべてコードの音（SoundLibrary.asset なし）";
            int assigned = SoundLibrary.KnownSounds.Count(known => { var sound = sounds.Find(known.Key); return sound != null && sound.clip != null; });
            return "効果音の割り当て " + assigned + "/" + SoundLibrary.KnownSounds.Length + "（残りはコードの音）、環境音 "
                + (sounds.ambience != null ? sounds.ambience.name : "コードの音");
        }

        private void CreateAmbience(SoundLibrary sounds)
        {
            var ambience = gameObject.AddComponent<AudioSource>();
            ambience.clip = sounds != null && sounds.ambience != null ? sounds.ambience : CreateBallastHum();
            ambience.loop = true;
            ambience.volume = sounds != null ? sounds.ambienceVolume : .4f;
            ambience.Play();
        }

        private AudioClip CreateBallastHum()
        {
            const int rate = 22050;
            var samples = new float[rate * 2];
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / rate;
                samples[i] = (Mathf.Sin(2 * Mathf.PI * 50 * t) + Mathf.Sin(2 * Mathf.PI * 100 * t) * .25f) * .045f;
            }
            ambienceClip = AudioClip.Create("Fluorescent ballast", samples.Length, 1, rate, false);
            ambienceClip.SetData(samples, 0);
            return ambienceClip;
        }
    }
}
