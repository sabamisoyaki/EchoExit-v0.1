using System;
using System.Collections;
using System.Collections.Generic;
using Door666.Core;
using UnityEngine;

namespace Door666.Runtime
{
    public enum GameScreen { Title, Playing, Paused, Judging, Editing, Ending, Error }

    public sealed class SessionCoordinator : MonoBehaviour
    {
        public GameScreen Screen { get; private set; }
        public GameSettings Settings { get; private set; }
        public PlayerInputReader Input { get; private set; }
        public FirstPersonRig Player { get; private set; }
        public GameUI UI { get; private set; }
        public WorldBuilder World { get; private set; }
        public PlacementEditor StageEditor { get; private set; }
        public StageRepository Repository { get; private set; }
        public AnomalyCatalog Definitions { get; private set; }
        public RunSession Session { get; private set; }
        public bool SubtitlesEnabled { get; set; }
        public IReadOnlyList<AnomalyActor> Actors => actors;
        private readonly List<AnomalyActor> actors = new List<AnomalyActor>();
        private readonly System.Random random = new System.Random();
        private StageData currentStage;
        private AudioSource ambience;
        private AudioClip ambienceClip;
        private Coroutine transition;

        private void Awake()
        {
            Settings = Resources.Load<GameSettings>(GameConstants.SettingsResource);
            if (Settings == null) Settings = ScriptableObject.CreateInstance<GameSettings>();
            Definitions = AnomalyCatalog.FromJson(Resources.Load<TextAsset>(GameConstants.DefinitionsResource).text);
            Input = new PlayerInputReader();
            Player = new GameObject("Player", typeof(CharacterController)).AddComponent<FirstPersonRig>();
            Player.Initialize();
            UI = new GameObject("Interface", typeof(RectTransform)).AddComponent<GameUI>();
            UI.Initialize(this);
            World = new WorldBuilder(Definitions);
            StageEditor = new PlacementEditor(this);
            Repository = new StageRepository(Application.persistentDataPath);
            SubtitlesEnabled = PlayerPrefs.GetInt(GameConstants.SubtitlePreference, 1) != 0;
            var loaded = Repository.LoadOrCreate(Resources.Load<TextAsset>(GameConstants.DefaultStagesResource).text);
            foreach (string warning in loaded.Warnings) Debug.LogWarning(warning);
            CreateAmbience();
            ShowTitle();
            if (!loaded.Success)
            {
                Screen = GameScreen.Error;
                UI.ShowError(loaded.Error + "\n既存ファイルは変更していません。\n" + Repository.SavePath);
            }
        }

        private void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, .1f);
            if (Input == null || Input.IsRebinding) return;
            if (Screen == GameScreen.Editing) { StageEditor.Tick(); return; }
            if (Input.Pause.WasPressedThisFrame())
            {
                if (Screen == GameScreen.Playing) Pause();
                else if (Screen == GameScreen.Paused) Resume();
                return;
            }
            if (Screen != GameScreen.Playing) return;
            if (!Application.isFocused && !Application.isBatchMode) { Pause(); return; }
            Player.Tick(Input, Settings.playerSpeed, dt);
            World.Tick(dt);
            Session.Tick(Time.deltaTime);
            if (Session.EndReason != RunEndReason.None) { FinishRun(); return; }

            // One occlusion-aware gaze ray per frame; every ritual shares its result.
            var view = Player.View.transform;
            Physics.Raycast(view.position, view.forward, out var hit, Settings.interactionDistance, ~0, QueryTriggerInteraction.Ignore);
            var target = hit.collider == null ? null : hit.collider.GetComponentInParent<StageObject>();
            var door = hit.collider == null ? null : hit.collider.GetComponentInParent<DoorTarget>();
            string prompt = door != null ? "[" + Input.Binding(Input.Interact) + "]  " + door.InteractionLabel : "";
            UI.SetHUD(Session.Streak, Session.RequiredStreak, Session.RemainingSeconds, prompt);
            // Door input takes precedence, immediately closes the round, and freezes pursuit.
            if (door != null && Input.Interact.WasPressedThisFrame()) { ChooseDoor(door.IsForward); return; }
            bool tapped = Input.Hit.WasPressedThisFrame();
            foreach (var actor in actors)
            {
                if (actor == null) continue;
                bool gazing = target != null && actor.gameObject == target.gameObject;
                actor.Tick(actor.GetPerception(Player.transform.position, Player.Speed, gazing, tapped && gazing), dt);
                if (Screen != GameScreen.Playing) break;
            }
        }

        public void StartRun()
        {
            StopTransition();
            StageEditor.Close();
            Player.View.orthographic = false;
            Session = new RunSession(Settings.roundSeconds, Settings.requiredCorrectAnswers);
            Session.StartRun();
            currentStage = null;
            BuildNextRound();
        }

        private void BuildNextRound()
        {
            double roll = random.NextDouble();
            // The core owns the fixed threshold; settings may tune the effective roll for playtests.
            bool desired = roll < Settings.anomalyProbability;
            var selected = RoundSelector.Select(Repository.Data.scenes, currentStage == null ? (int?)null : currentStage.sceneId,
                currentStage, desired ? 0 : .9, random.Next());
            if (selected.Stage == null)
            {
                Screen = GameScreen.Error;
                SetCursor(true);
                UI.ShowError("読み込めるステージがありません。部屋の編集からステージを作成してください。");
                return;
            }
            LoadRound(selected.Stage, selected.IncludeAnomalies);
        }

        public void LoadRound(StageData stage, bool includeAnomalies)
        {
            SuspendActors(true);
            actors.Clear();
            currentStage = stage;
            World.Build(stage, includeAnomalies, Settings.maximumAnomalies);
            Player.Teleport(WorldBuilder.SpawnPosition);
            foreach (var placed in World.PlacedObjects)
            {
                var definition = Definitions.FindByPrefab(placed.PrefabId);
                if (!placed.IsAnomaly || definition == null) continue;
                var actor = placed.gameObject.AddComponent<AnomalyActor>();
                actor.Initialize(definition, true, placed.VisualRoot, Player.transform, Player.View, Settings.playerSpeed);
                actor.Recognized += OnRecognition;
                actor.Caught += OnCaught;
                actor.Subtitle += UI.Subtitle;
                actors.Add(actor);
            }
            Session.BeginRound(stage.sceneId, World.PlacedAnomalyCount);
            Screen = GameScreen.Playing;
            SetCursor(false);
            UI.ShowPlay();
            UI.Fade(0);
            UI.SetHUD(Session.Streak, Session.RequiredStreak, Session.RemainingSeconds, "");
        }

        public void ChooseDoor(bool forward)
        {
            if (Screen != GameScreen.Playing) return;
            var result = Session.Decide(forward ? DoorChoice.Forward : DoorChoice.Backward);
            if (!result.Accepted) return;
            Screen = GameScreen.Judging;
            SuspendActors(true);
            transition = StartCoroutine(ShowDecision(result));
        }

        private IEnumerator ShowDecision(RoundDecision result)
        {
            UI.Banner(result.IsCorrect ? "扉の先へ。  " + result.Streak + " / " + Session.RequiredStreak : "ここへ、戻された。\n連続正解  0 / " + Session.RequiredStreak, Settings.feedbackSeconds + .3f);
            SpatialAudio.Emit(Player.transform, Player.View.transform.position, result.IsCorrect ? "door" : "reverse", .35f);
            float elapsed = 0;
            while (elapsed < Settings.feedbackSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                UI.Fade(Mathf.SmoothStep(0, 1, elapsed / Settings.feedbackSeconds) * .94f);
                yield return null;
            }
            transition = null;
            if (Session.EndReason == RunEndReason.Escaped) FinishRun(); else BuildNextRound();
        }

        private void OnRecognition(AnomalyActor actor)
        {
            if (Screen != GameScreen.Playing) return;
            UI.Banner(actor.IsThreat ? "異変を認識した。  出口へ逃げろ。" : "異変を認識した。");
        }
        private void OnCaught(AnomalyActor actor)
        {
            if (Screen == GameScreen.Playing && Session.Catch(actor.Definition.displayName, actor.Definition.endingId)) FinishRun();
        }
        private void FinishRun()
        {
            Screen = GameScreen.Ending;
            SuspendActors(true);
            SetCursor(true);
            if (Session.EndReason == RunEndReason.Escaped) UI.ShowEnding("扉の向こうへ", "六度の判断が、あなたを外へ連れ出した。\nそれでも、背後の扉を見てはいけない。");
            else if (Session.EndReason == RunEndReason.Caught) UI.ShowEnding("もう、戻れない", "「" + Session.CapturedBy + "」に捕まった。\nこの部屋には、あなたの気配が残る。");
            else UI.ShowEnding("時間が、尽きた", "扉の音は、もう聞こえない。\nこの部屋での探索は終わった。");
        }

        public void Pause() { if (Screen != GameScreen.Playing) return; Screen = GameScreen.Paused; SuspendActors(true); SetCursor(true); UI.ShowPause(); }
        public void Resume() { if (Screen != GameScreen.Paused) return; Screen = GameScreen.Playing; SuspendActors(false); SetCursor(false); UI.ShowPlay(); }
        public void ShowTitle()
        {
            StopTransition();
            StageEditor.Close();
            SuspendActors(true);
            actors.Clear();
            Session = null;
            currentStage = null;
            World.Build(new StageData { sceneId = 1 }, false, 0);
            Player.View.orthographic = false;
            Player.Teleport(new Vector3(1.8f, .05f, -4.5f), -8f);
            Screen = GameScreen.Title;
            SetCursor(true);
            UI.ShowTitle();
        }
        public void StartEditor()
        {
            StopTransition();
            SuspendActors(true);
            actors.Clear();
            Session = null;
            Screen = GameScreen.Editing;
            SetCursor(true);
            StageEditor.Open();
        }
        public void Quit()
        {
            PlayerPrefs.Save();
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
        private void StopTransition() { if (transition != null) { StopCoroutine(transition); transition = null; } }
        private void SuspendActors(bool suspend) { foreach (var actor in actors) if (actor != null) actor.Suspend(suspend); }
        private static void SetCursor(bool free) { Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Locked; Cursor.visible = free; }
        private void OnApplicationFocus(bool focus) { if (!focus && Screen == GameScreen.Playing && !Application.isBatchMode) Pause(); }

        private void CreateAmbience()
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
            ambience = gameObject.AddComponent<AudioSource>();
            ambience.clip = ambienceClip;
            ambience.loop = true;
            ambience.volume = .4f;
            ambience.Play();
        }
        private void OnDestroy()
        {
            Input?.Dispose();
            StageEditor?.Close();
            World?.Dispose();
            if (ambienceClip != null) Destroy(ambienceClip);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
