using Door666.Runtime;
using UnityEngine;

namespace Door666.Tests
{
    internal static class SceneTestUtility
    {
        public const int MaxLoadFrames = 600;

        /// <summary>Finds the screen scene's controller and reports whether it has loaded the field and entered its screen.
        /// Tests poll this in their own loop: after EnterPlayMode the runner does not step nested enumerators.</summary>
        public static bool IsReady<T>(out T controller) where T : SceneController
        {
            controller = Object.FindFirstObjectByType<T>();
            return controller != null && controller.IsReady;
        }
    }
}
