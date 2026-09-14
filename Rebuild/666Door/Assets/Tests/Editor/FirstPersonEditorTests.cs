using System.Collections;
using System.Linq;
using Door666.Core;
using Door666.Runtime;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Door666.Tests
{
    public sealed class FirstPersonEditorTests
    {
        [UnityTest]
        public IEnumerator PlacedAnomaliesAreLiveAndACaptureOnlyResetsTheThreat()
        {
            EditorSceneManager.OpenScene(GameConstants.ScenePath(GameConstants.EditModeScene), OpenSceneMode.Single);
            yield return new EnterPlayMode();
            EditModeSceneController scene = null;
            for (int frame = 0; frame < SceneTestUtility.MaxLoadFrames && !SceneTestUtility.IsReady(out scene); frame++) yield return null;
            Assert.That(scene != null && scene.IsReady, Is.True, "EditModeSceneController did not become ready.");
            var editor = scene.StageEditor;
            Assert.That(editor.IsOpen, Is.True);
            Assert.That(editor.MenuOpen, Is.False);
            Assert.That(scene.Player.View.orthographic, Is.False);

            // A shrinking box placed beside the player recognizes the approach without leaving the editor.
            editor.New();
            editor.SetCategory(true);
            editor.Choose("anomaryShirinkBox");
            Assert.That(editor.TryPlace(WorldBuilder.SpawnPosition + new Vector3(0, 0, 1.5f), out string problem), Is.True, problem);
            Assert.That(editor.HasUnsavedChanges, Is.True);
            var box = editor.Actors.Single();
            for (int frame = 0; frame < 30 && !box.IsRecognized; frame++) yield return null;
            Assert.That(box.IsRecognized, Is.True, "Placed anomalies must run their rituals while editing.");
            Assert.That(editor.TryPlace(scene.Player.transform.position, out problem), Is.False, "Placing inside the player must be refused.");

            // Menus suspend the live anomalies.
            editor.SetMenuOpen(true);
            Assert.That(editor.MenuOpen, Is.True);
            editor.SetMenuOpen(false);

            // A pursuer that catches the player is returned to its authored pose, unrecognized, and editing continues.
            editor.Load(2);
            yield return null;
            var bear = editor.Actors.Single(actor => actor.IsThreat);
            Vector3 origin = bear.transform.position;
            scene.Player.Teleport(new Vector3(origin.x, .05f, origin.z - .65f));
            bear.Tick(new RitualPerception { Distance = .65f, Gazing = true, Hit = true }, 1f / 60);
            Assert.That(bear.IsRecognized, Is.True);
            for (int frame = 0; frame < 130; frame++) bear.Tick(new RitualPerception { Distance = .65f }, 1f / 60);
            yield return null;
            yield return null;
            var respawned = editor.Actors.Single(actor => actor.IsThreat);
            Assert.That(respawned, Is.Not.SameAs(bear), "The captured pursuer must be recreated.");
            Assert.That(respawned.IsRecognized, Is.False);
            Assert.That(Vector3.Distance(respawned.transform.position, origin), Is.LessThan(.01f));
            Assert.That(scene.Screen, Is.EqualTo(GameScreen.Editing));

            // Rotating recreates the placement with its new pose; deleting removes it from the draft and the field.
            scene.Player.Teleport(WorldBuilder.SpawnPosition);
            editor.SetCategory(false);
            editor.Choose("ChairPrefab");
            Assert.That(editor.TryPlace(new Vector3(0, 0, 2.5f), out problem), Is.True, problem);
            editor.Rotate(scene.World.PlacedObjects.Last());
            var chair = scene.World.PlacedObjects.Last();
            Assert.That(chair.PrefabId, Is.EqualTo("ChairPrefab"));
            Assert.That(chair.Data.rotation.y, Is.EqualTo(45).Within(.01f));
            Assert.That(chair.transform.rotation.eulerAngles.y, Is.EqualTo(45).Within(.1f));
            int count = scene.World.PlacedObjects.Count;
            editor.Delete(chair);
            Assert.That(scene.World.PlacedObjects.Count, Is.EqualTo(count - 1));
            Assert.That(scene.World.PlacedObjects.Any(placed => placed.PrefabId == "ChairPrefab" && Mathf.Approximately(placed.Data.rotation.y, 45)), Is.False);
            yield return new ExitPlayMode();
        }
    }
}
