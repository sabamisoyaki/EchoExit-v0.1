using System.Collections;
using System.Collections.Generic;
using Door666.Core;
using UnityEngine;

namespace Door666.Runtime
{
    /// <summary>Game.unity: one run of rounds. Rounds rebuild only the field's placements; the scene is never reloaded mid-run.</summary>
    public sealed class GameSceneController : SceneController
    {
        public RunSession Session { get; private set; }
        public IReadOnlyList<AnomalyActor> Actors => actors;

        private readonly List<AnomalyActor> actors = new List<AnomalyActor>();
        private readonly System.Random random = new System.Random();
        private StageData currentStage;
        private Coroutine transition;

        protected override void Enter() => StartRun();

        private void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, .1f);
            if (Session == null || Input.IsRebinding) return;
            if (Input.Pause.WasPressedThisFrame())
            {
                if (Screen == GameScreen.Playing) Pause();
                else if (Screen == GameScreen.Paused) Resume();
                return;
            }
            if (Screen != GameScreen.Playing) return;
            if (!Application.isFocused && !Application.isBatchMode) { Pause(); return; }
            Player.Tick(Input, Settings.playerSpeed, dt);
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

        /// <summary>Starts a fresh run in place; retrying from the ending does not reload the scene.</summary>
        public override void StartRun()
        {
            StopTransition();
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
                ShowError("読み込めるステージがありません。部屋の編集からステージを作成してください。");
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

        public void Pause()
        {
            if (Screen != GameScreen.Playing) return;
            Screen = GameScreen.Paused;
            SuspendActors(true);
            SetCursor(true);
            UI.ShowPause();
        }

        public override void Resume()
        {
            if (Screen != GameScreen.Paused) return;
            Screen = GameScreen.Playing;
            SuspendActors(false);
            SetCursor(false);
            UI.ShowPlay();
        }

        private void StopTransition() { if (transition != null) { StopCoroutine(transition); transition = null; } }
        private void SuspendActors(bool suspend) { foreach (var actor in actors) if (actor != null) actor.Suspend(suspend); }
        private void OnApplicationFocus(bool focus) { if (!focus && Screen == GameScreen.Playing && !Application.isBatchMode) Pause(); }
    }
}
