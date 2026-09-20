using System.Collections;
using System.IO;
using System.Linq;
using Door666.Core;
using Door666.Runtime;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Door666.Tests
{
    public sealed class RuntimeIntegrationTests
    {
        [UnityTest]
        public IEnumerator TitleRoundLoopPauseTimeoutCaptureAndEditorWorkTogether()
        {
            EditorSceneManager.OpenScene(GameConstants.ScenePath(GameConstants.TitleScene), OpenSceneMode.Single);
            yield return new EnterPlayMode();
            TitleSceneController title = null;
            for (int frame = 0; frame < SceneTestUtility.MaxLoadFrames && !SceneTestUtility.IsReady(out title); frame++) yield return null;
            Assert.That(title != null && title.IsReady, Is.True, "TitleSceneController did not become ready.");
            Assert.That(title.Screen, Is.EqualTo(GameScreen.Title));
            Assert.That(SceneManager.GetSceneByName(GameConstants.FieldScene).isLoaded, Is.True);
            // Playing with the field already open beside the screen scene (as the editor shows it) must not load a second room.
            Assert.That(Object.FindObjectsByType<FieldRoot>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
            Capture(title, "01-title.png");

            title.StartRun();
            GameSceneController game = null;
            for (int frame = 0; frame < SceneTestUtility.MaxLoadFrames && !SceneTestUtility.IsReady(out game); frame++) yield return null;
            Assert.That(game != null && game.IsReady, Is.True, "GameSceneController did not become ready.");
            Assert.That(game.Screen, Is.EqualTo(GameScreen.Playing));
            Assert.That(Object.FindObjectsByType<FirstPersonRig>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
            Assert.That(Object.FindObjectsByType<FieldRoot>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
            Assert.That(game.Session.PlacedAnomalyCount, Is.EqualTo(game.World.PlacedAnomalyCount));
            Assert.That(game.World.PlacedObjects.Count(x => x.IsAnomaly), Is.EqualTo(game.World.PlacedAnomalyCount));
            var path = new NavMeshPath();
            Assert.That(NavMesh.SamplePosition(WorldBuilder.SpawnPosition, out var start, 2, NavMesh.AllAreas), Is.True);
            Assert.That(NavMesh.SamplePosition(WorldBuilder.ForwardDoorPosition - Vector3.forward, out var end, 2, NavMesh.AllAreas), Is.True);
            Assert.That(NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path), Is.True);
            Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete));
            Capture(game, "02-exploration.png");

            // The hand shows only while it strikes, and never blocks the gaze ray.
            var hand = game.Player.Hand;
            Assert.That(hand, Is.Not.Null, "Player.prefab has no striking hand.");
            Assert.That(hand.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(hand.GetComponentsInChildren<Renderer>(), Is.Empty, "The hand must be hidden before striking.");
            var view = game.Player.View.transform;
            Assert.That(Physics.Raycast(view.position, view.forward, out var wall, 30, ~0, QueryTriggerInteraction.Ignore), Is.True);
            game.Player.Strike(wall);
            Assert.That(hand.IsStriking, Is.True);
            Assert.That(hand.GetComponentsInChildren<Renderer>(), Is.Not.Empty);
            bool kicked = false;
            for (int frame = 0; frame < 300 && hand.IsStriking && !kicked; frame++)
            {
                yield return null;
                kicked = hand.ViewKick > 0;
            }
            Assert.That(kicked, Is.True, "Striking a surface kicks the view.");
            Assert.That(view.InverseTransformPoint(hand.transform.position).z, Is.LessThanOrEqualTo(wall.distance), "The fist must stop in front of the surface.");
            Capture(game, "02-strike.png");
            for (int frame = 0; frame < 300 && hand.IsStriking; frame++) yield return null;
            Assert.That(hand.IsStriking, Is.False);
            Assert.That(hand.GetComponentsInChildren<Renderer>(), Is.Empty, "The hand must hide after the strike.");

            double pausedTime = game.Session.RemainingSeconds;
            game.Pause();
            yield return new WaitForSecondsRealtime(.1f);
            Assert.That(game.Session.RemainingSeconds, Is.EqualTo(pausedTime));
            game.Resume();
            game.Settings.feedbackSeconds = .04f;
            for (int index = 0; index < 6; index++)
            {
                game.ChooseDoor(!game.Session.HasAnomaly);
                Assert.That(game.Session.Streak, Is.EqualTo(index + 1));
                game.ChooseDoor(game.Session.HasAnomaly); // A second input cannot change the decision.
                yield return new WaitForSecondsRealtime(.1f);
                yield return null;
            }
            Assert.That(game.Session.EndReason, Is.EqualTo(RunEndReason.Escaped));
            Assert.That(game.Screen, Is.EqualTo(GameScreen.Ending));
            Capture(game, "03-escaped.png");

            // Retrying from the ending restarts the run inside the same Game scene.
            game.StartRun();
            Assert.That(game.Session.Streak, Is.Zero);
            game.Session.Tick(181);
            yield return null;
            Assert.That(game.Screen, Is.EqualTo(GameScreen.Ending));
            Assert.That(game.Session.EndReason, Is.EqualTo(RunEndReason.TimeExpired));

            game.StartRun();
            // End the random round, then construct the fixed pursuit fixture through the same runtime.
            game.Session.Decide(DoorChoice.Forward);
            var chaseStage = StageRepository.Parse(Resources.Load<TextAsset>(GameConstants.DefaultStagesResource).text).Data.scenes.Single(s => s.sceneId == 2);
            game.LoadRound(chaseStage, true);
            yield return null;
            var threat = game.Actors.Single(a => a.IsThreat);
            Vector3 position = threat.transform.position;
            game.Player.Teleport(new Vector3(position.x, .05f, position.z - .65f));
            threat.Tick(new RitualPerception { Distance = .65f, Gazing = true, Hit = true }, 1f / 60);
            Assert.That(threat.IsRecognized, Is.True);
            Assert.That(game.Session.EndReason, Is.EqualTo(RunEndReason.None));
            for (int frame = 0; frame < 65; frame++) threat.Tick(new RitualPerception { Distance = .65f }, 1f / 60);
            Assert.That(game.Session.EndReason, Is.EqualTo(RunEndReason.None), "Recognition grace must prevent early capture.");
            for (int frame = 0; frame < 60 && game.Screen == GameScreen.Playing; frame++) threat.Tick(new RitualPerception { Distance = .65f }, 1f / 60);
            Assert.That(game.Session.EndReason, Is.EqualTo(RunEndReason.Caught));
            Assert.That(game.Session.CapturedBy, Is.EqualTo("叩き起こし"));

            game.StartEditor();
            EditModeSceneController editor = null;
            for (int frame = 0; frame < SceneTestUtility.MaxLoadFrames && !SceneTestUtility.IsReady(out editor); frame++) yield return null;
            Assert.That(editor != null && editor.IsReady, Is.True, "EditModeSceneController did not become ready.");
            Assert.That(editor.Screen, Is.EqualTo(GameScreen.Editing));
            Assert.That(editor.StageEditor.IsOpen, Is.True);
            Assert.That(editor.Player.View.orthographic, Is.False);
            // The run's actors went with the Game scene; the first stage places no anomalies of its own.
            Assert.That(Object.FindObjectsByType<AnomalyActor>(FindObjectsSortMode.None), Is.Empty);
            Capture(editor, "04-stage-editor.png");
            editor.StageEditor.SetMenuOpen(true);
            yield return null;
            Capture(editor, "05-stage-editor-menu.png");

            editor.ShowTitle();
            for (int frame = 0; frame < SceneTestUtility.MaxLoadFrames && !SceneTestUtility.IsReady(out title); frame++) yield return null;
            Assert.That(title != null && title.IsReady, Is.True, "TitleSceneController did not become ready.");
            Assert.That(Object.FindObjectsByType<SceneController>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
            yield return new ExitPlayMode();
        }

        private static void Capture(SceneController screen, string name)
        {
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Artifacts/Screenshots"));
            Directory.CreateDirectory(directory);
            var camera = screen.Player.View;
            var canvas = screen.UI.GetComponent<Canvas>();
            var originalMode = canvas.renderMode;
            var originalCamera = canvas.worldCamera;
            var originalTarget = camera.targetTexture;
            var target = new RenderTexture(1600, 900, 24);
            var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = .3f;
                Canvas.ForceUpdateCanvases();
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
                image.Apply();
                File.WriteAllBytes(Path.Combine(directory, name), image.EncodeToPNG());
            }
            finally
            {
                canvas.renderMode = originalMode;
                canvas.worldCamera = originalCamera;
                camera.targetTexture = originalTarget;
                RenderTexture.active = previous;
                Object.Destroy(image);
                Object.Destroy(target);
            }
        }
    }
}
