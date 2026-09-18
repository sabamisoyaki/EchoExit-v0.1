using System.Collections;
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
        public GameScreen Screen { get; protected set; }
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

        private StageLoadResult loadedStages;
        private AudioClip ambienceClip;

        protected virtual void Awake()
        {
            if (player == null)
                throw new System.InvalidOperationException(gameObject.scene.name + " にプレイヤーが配置されていません。メニュー「666号扉 → プロジェクトを初期化」を実行してください。");
            if (ui == null)
                throw new System.InvalidOperationException(gameObject.scene.name + " に UI（Interface.prefab）が配置されていません。メニュー「666号扉 → プロジェクトを初期化」を実行してください。");
            Settings =Resources.Load<GameSettings>(GameConstants.SettingsResource);
            if (Settings == null) Settings = ScriptableObject.CreateInstance<GameSettings>();
            Definitions = AnomalyCatalog.FromJson(Resources.Load<TextAsset>(GameConstants.DefinitionsResource).text);
            Input = new PlayerInputReader();
            Player = player;
            Player.Initialize();
            UI = ui;
            UI.Initialize(this);
            Repository = new StageRepository(Application.persistentDataPath);
            SubtitlesEnabled = PlayerPrefs.GetInt(GameConstants.SubtitlePreference, 1) != 0;
            loadedStages = Repository.LoadOrCreate(Resources.Load<TextAsset>(GameConstants.DefaultStagesResource).text);
            foreach (string warning in loadedStages.Warnings) Debug.LogWarning(warning);
            CreateAmbience();
        }

        private IEnumerator Start()
        {
            yield return FieldRoot.EnsureLoaded();
            var field = FindFirstObjectByType<FieldRoot>();
            if (field == null)
                ShowError("部屋のシーン「" + GameConstants.FieldScene + "」を読み込めませんでした。");
            else
            {
                World = new WorldBuilder(field, Definitions);
                if (loadedStages.Success) Enter();
                else ShowError(loadedStages.Error + "\n既存ファイルは変更していません。\n" + Repository.SavePath);
            }
            IsReady = true;
        }

        /// <summary>Called once the shared field is available.</summary>
        protected abstract void Enter();

        public virtual void ShowTitle() => LoadScreen(GameConstants.TitleScene);
        public virtual void StartRun() => LoadScreen(GameConstants.GameScene);
        public virtual void StartEditor() => LoadScreen(GameConstants.EditModeScene);
        public virtual void Resume() { }

        public void Quit()
        {
            PlayerPrefs.Save();
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        protected void ShowError(string message)
        {
            Screen = GameScreen.Error;
            SetCursor(true);
            UI.ShowError(message);
        }

        // A single-mode load also unloads the additive field, so the next screen starts from a clean room.
        private static void LoadScreen(string scene) => SceneManager.LoadScene(scene, LoadSceneMode.Single);

        protected static void SetCursor(bool free)
        {
            Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = free;
        }

        protected virtual void OnDestroy()
        {
            if (Input != null) Input.Dispose();
            if (World != null) World.Dispose();
            if (ambienceClip != null) Destroy(ambienceClip);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void CreateAmbience()
        {
            var sounds = SoundLibrary.Load();
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
