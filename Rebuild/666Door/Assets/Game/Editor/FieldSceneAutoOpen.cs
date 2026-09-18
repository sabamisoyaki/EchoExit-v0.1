using System.IO;
using Door666.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Door666.Editor
{
    /// <summary>Opening a screen scene also opens the shared field additively, so the room is visible while editing.
    /// At runtime each screen scene loads the field itself; this only affects the Unity editor.</summary>
    [InitializeOnLoad]
    internal static class FieldSceneAutoOpen
    {
        static FieldSceneAutoOpen()
        {
            EditorSceneManager.sceneOpened += OnSceneOpened;
        }

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            if (mode != OpenSceneMode.Single) return;
            if (scene.name != GameConstants.TitleScene && scene.name != GameConstants.GameScene && scene.name != GameConstants.EditModeScene) return;
            // Opening another scene inside the callback is re-entrant; defer to the next editor update.
            EditorApplication.delayCall += () =>
            {
                string path = GameConstants.ScenePath(GameConstants.FieldScene);
                if (EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer) return;
                if (!File.Exists(path) || SceneManager.GetSceneByPath(path).isLoaded) return;
                EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            };
        }
    }
}
