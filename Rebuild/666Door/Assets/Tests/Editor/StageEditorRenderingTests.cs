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
            EditorSceneManager.OpenScene(GameConstants.ScenePath, OpenSceneMode.Single);
            yield return new EnterPlayMode();
            yield return null;
            var game = Object.FindFirstObjectByType<SessionCoordinator>();
            var problems = new List<string>();

            game.StartEditor();
            yield return null;
            Collect(problems, "open");
            foreach (bool anomaly in new[] { false, true })
            {
                game.StageEditor.SetCategory(anomaly);
                foreach (string id in game.World.Catalog.PrefabIds)
                {
                    game.StageEditor.Choose(id);
                    yield return null;
                    Collect(problems, (anomaly ? "anomaly " : "ordinary ") + id);
                }
            }
            foreach (var stage in game.Repository.Data.scenes)
            {
                if (stage == null) continue;
                game.StageEditor.Load(stage.sceneId);
                yield return null;
                Collect(problems, "stage " + stage.sceneId);
            }
            game.StageEditor.New();
            yield return null;
            Collect(problems, "new stage");

            // Leaving and re-entering must not keep renderers that point at the previous session's guides.
            game.ShowTitle();
            yield return null;
            Collect(problems, "title after editor");
            game.StartRun();
            yield return null;
            game.StartEditor();
            yield return null;
            Collect(problems, "editor after run");
            game.ShowTitle();
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
