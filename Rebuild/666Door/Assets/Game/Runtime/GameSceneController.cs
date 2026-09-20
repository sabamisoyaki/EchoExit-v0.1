using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Door666.Core;
using UnityEngine;

namespace Door666.Runtime
{
    /// <summary>Game.unity: one run of rounds. Rounds rebuild only the field's placements; the scene is never reloaded mid-run.</summary>
    public sealed class GameSceneController : SceneController
    {
        public RunSession Session { get; private set; }
        public IReadOnlyList<AnomalyActor> Actors => anomalies.Actors;

        private AnomalyActorSet anomalies;
        private readonly System.Random random = new System.Random();
        private StageData currentStage;
        private Coroutine transition;

        protected override void Awake()
        {
            base.Awake();
            anomalies = new AnomalyActorSet(this);
            anomalies.Recognized += OnRecognition;
            anomalies.Caught += OnCaught;
        }

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
            if (!Application.isFocused && !Application.isBatchMode) { GameLog.Info("画面", "ウィンドウのフォーカスがないため一時停止します。"); Pause(); return; }
            Player.Tick(Input, Settings.playerSpeed, dt);
            Session.Tick(Time.deltaTime);
            if (Session.EndReason != RunEndReason.None) { FinishRun(); return; }

            // One occlusion-aware gaze ray per frame; every ritual shares its result.
            var view = Player.View.transform;
            Physics.Raycast(view.position, view.forward, out var hit, Settings.interactionDistance, ~0, QueryTriggerInteraction.Ignore);
            var target = hit.collider == null ? null : hit.collider.GetComponentInParent<StageObject>();
            var door = hit.collider == null ? null : hit.collider.GetComponentInParent<DoorTarget>();
            UI.SetHUD(Session.Streak, Session.RequiredStreak, Session.RemainingSeconds);
            UI.DoorPrompt(door, door != null ? Input.Binding(Input.Interact) : "");
            // Door input takes precedence, immediately closes the round, and freezes pursuit.
            if (door != null && Input.Interact.WasPressedThisFrame()) { ChooseDoor(door.IsForward); return; }
            bool strike = Input.Hit.WasPressedThisFrame();
            if (strike) Player.Strike(hit);
            // A capture ends the run and suspends every actor, so the remaining actors skip this frame.
            anomalies.Tick(target, strike, dt);
        }

        /// <summary>Starts a fresh run in place; retrying from the ending does not reload the scene.</summary>
        public override void StartRun()
        {
            StopTransition();
            Session = new RunSession(Settings.roundSeconds, Settings.requiredCorrectAnswers);
            Session.StartRun();
            GameLog.Info("ラウンド", "ラン開始: " + Session.RequiredStreak + " 連続正解で脱出、各ラウンドの制限時間 " + Session.RoundSeconds + " 秒");
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
            GameLog.Info("ラウンド", "抽選 " + roll.ToString("F3") + (desired ? " < " : " ≥ ") + Settings.anomalyProbability
                + " → 異変" + (desired ? "あり" : "なし") + "を希望 → ステージ " + selected.Stage.sceneId + "（異変" + (selected.IncludeAnomalies ? "あり" : "なし") + "）"
                + (selected.ReuseCurrent ? "。候補がないため同じ部屋を使います" : selected.UsedFallback ? "。希望に合う部屋がないため反対側で代用" : ""));
            LoadRound(selected.Stage, selected.IncludeAnomalies);
        }

        public void LoadRound(StageData stage, bool includeAnomalies)
        {
            anomalies.Clear();
            currentStage = stage;
            World.Build(stage, includeAnomalies, Settings.maximumAnomalies);
            Player.Teleport(WorldBuilder.SpawnPosition);
            foreach (var placed in World.PlacedObjects) anomalies.Attach(placed);
            Session.BeginRound(stage.sceneId, World.PlacedAnomalyCount);
            LogRoundStart(stage, includeAnomalies);
            Screen = GameScreen.Playing;
            SetCursor(false);
            UI.ShowPlay();
            UI.Fade(0);
            UI.SetHUD(Session.Streak, Session.RequiredStreak, Session.RemainingSeconds);
            UI.Prompt("");
        }

        private void LogRoundStart(StageData stage, bool includeAnomalies)
        {
            var names = World.PlacedObjects.Where(placed => placed != null && placed.IsAnomaly).Select(placed => World.Catalog.DisplayName(placed.PrefabId)).ToList();
            int authored = stage.items == null ? 0 : stage.items.Count(item => item != null && item.isAnomaly);
            string contents = names.Count > 0 ? "異変 " + names.Count + " 個（" + string.Join("、", names) + "）"
                : "異変なし" + (!includeAnomalies && authored > 0 ? "（異変なしの回なので、ステージの異変 " + authored + " 個は置いていません）" : "");
            GameLog.Info("ラウンド", "ラウンド " + Session.RoundsPlayed + " 開始: ステージ " + stage.sceneId + "、配置物 " + World.PlacedObjects.Count + " 個、"
                + contents + " → 正解は" + (Session.HasAnomaly ? "後ろ" : "前") + "の扉");
            if (includeAnomalies && names.Count == 0)
                GameLog.Warning("ラウンド", "異変ありの回ですが、ステージ " + stage.sceneId + " に置けた異変が 0 個です。正解は前の扉になります。");
        }

        public void ChooseDoor(bool forward)
        {
            if (Screen != GameScreen.Playing) return;
            var result = Session.Decide(forward ? DoorChoice.Forward : DoorChoice.Backward);
            if (!result.Accepted) return;
            GameLog.Info("ラウンド", "扉: " + (forward ? "前" : "後ろ") + "を選択 → " + (result.IsCorrect ? "正解" : "不正解")
                + "（異変 " + Session.PlacedAnomalyCount + " 個、認識 " + anomalies.Actors.Count(actor => actor != null && actor.IsRecognized) + " 個）。連続正解 "
                + result.Streak + "/" + Session.RequiredStreak + "、残り " + Session.RemainingSeconds.ToString("F1") + " 秒");
            Screen = GameScreen.Judging;
            SuspendActors(true);
            transition = StartCoroutine(ShowDecision(result));
        }

        private IEnumerator ShowDecision(RoundDecision result)
        {
            UI.DoorResult(result.IsCorrect, result.Streak, Session.RequiredStreak, Settings.feedbackSeconds + .3f);
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
            UI.Recognized(actor.IsThreat);
        }

        private void OnCaught(AnomalyActor actor)
        {
            if (Screen == GameScreen.Playing && Session.Catch(actor.Definition.displayName, actor.Definition.endingId)) FinishRun();
        }

        private void FinishRun()
        {
            string reason = Session.EndReason == RunEndReason.Escaped ? "脱出"
                : Session.EndReason == RunEndReason.Caught ? "捕獲（" + Session.CapturedBy + (string.IsNullOrEmpty(Session.EndingId) ? "" : "、エンディング " + Session.EndingId) + "）"
                : Session.EndReason == RunEndReason.TimeExpired ? "時間切れ" : Session.EndReason.ToString();
            GameLog.Info("ラウンド", "ラン終了: " + reason + "。" + Session.RoundsPlayed + " ラウンド目、連続正解 " + Session.Streak + "/" + Session.RequiredStreak);
            Screen = GameScreen.Ending;
            SuspendActors(true);
            SetCursor(true);
            UI.ShowEnding(Session.EndReason, Session.CapturedBy);
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
        private void SuspendActors(bool suspend) => anomalies.Suspend(suspend);
        private void OnApplicationFocus(bool focus)
        {
            if (focus || Screen != GameScreen.Playing || Application.isBatchMode) return;
            GameLog.Info("画面", "ウィンドウのフォーカスが外れたため一時停止します。");
            Pause();
        }
    }
}
