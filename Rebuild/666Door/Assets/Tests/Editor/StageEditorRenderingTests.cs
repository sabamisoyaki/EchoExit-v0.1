using System.Collections;
using System.Collections.Generic;
using Door666.Runtime;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Door666.Tests
{
    public sealed class StageEditorRenderingTests
    {
        [UnityTest]
        public IEnumerator StageEditorNeverRendersMissingMaterials()
        {
            EditorSceneManager.OpenScene(GameConstants.ScenePath(GameConstants.EditModeScene), OpenSceneMode.Single);
            yield return new EnterPlayMode();
            EditModeSceneController editor = null;
            for (int frame = 0; frame < SceneTestUtility.MaxLoadFrames && !SceneTestUtility.IsReady(out editor); frame++) yield return null;
            Assert.That(editor != null && editor.IsReady, Is.True, "EditModeSceneController did not become ready.");
            var problems = new List<string>();

            Collect(problems, "open");
            foreach (bool anomaly in new[] { false, true })
            {
                editor.StageEditor.SetCategory(anomaly);
                foreach (string id in editor.World.Catalog.PrefabIds)
                {
                    editor.StageEditor.Choose(id);
                    yield return null;
                    Collect(problems, (anomaly ? "anomaly " : "ordinary ") + id);
                }
            }
            foreach (var stage in editor.Repository.Data.scenes)
            {
                if (stage == null) continue;
                editor.StageEditor.Load(stage.sceneId);
                yield return null;
                Collect(problems, "stage " + stage.sceneId);
            }
            editor.StageEditor.New();
            yield return null;
            Collect(problems, "new stage");

            // Leaving and re-entering must not keep renderers that point at the previous session's guides.
            editor.ShowTitle();
            TitleSceneController title = null;
            for (int frame = 0; frame < SceneTestUtility.MaxLoadFrames && !SceneTestUtility.IsReady(out title); frame++) yield return null;
            Assert.That(title != null && title.IsReady, Is.True, "TitleSceneController did not become ready.");
            Collect(problems, "title after editor");
            title.StartRun();
            GameSceneController game = null;
            for (int frame = 0; frame < SceneTestUtility.MaxLoadFrames && !SceneTestUtility.IsReady(out game); frame++) yield return null;
            Assert.That(game != null && game.IsReady, Is.True, "GameSceneController did not become ready.");
            game.StartEditor();
            for (int frame = 0; frame < SceneTestUtility.MaxLoadFrames && !SceneTestUtility.IsReady(out editor); frame++) yield return null;
            Assert.That(editor != null && editor.IsReady, Is.True, "EditModeSceneController did not become ready.");
            Collect(problems, "editor after run");
            yield return new ExitPlayMode();

            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        private static void Collect(List<string> problems, string step)
        {
            foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var materials = renderer.sharedMaterials;
                if (materials.Length == 0) problems.Add(step + ": " + PathOf(renderer.transform) + " has no material slots");
                for (int i = 0; i < materials.Length; i++)
                {
                    var material = materials[i];
                    string issue = material == null ? "missing or destroyed material"
                        : material.shader == null ? "material without shader"
                        : !material.shader.isSupported || material.shader.name == "Hidden/InternalErrorShader" ? "unsupported shader " + material.shader.name
                        : null;
                    if (issue != null) problems.Add(step + ": " + PathOf(renderer.transform) + " [" + i + "] " + issue);
                }
            }
        }

        private static string PathOf(Transform transform)
        {
            string path = transform.name;
            for (var parent = transform.parent; parent != null; parent = parent.parent) path = parent.name + "/" + path;
            return path;
        }
    }
}
