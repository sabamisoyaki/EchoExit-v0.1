using Door666.Core;
using UnityEngine;

namespace Door666.Runtime
{
    /// <summary>Title.unity: the empty first room as a backdrop behind the title menu.</summary>
    public sealed class TitleSceneController : SceneController
    {
        protected override void Enter()
        {
            Screen = GameScreen.Title;
            World.Build(new StageData { sceneId = 1 }, false, 0);
            Player.Teleport(new Vector3(1.8f, .05f, -4.5f), -8f);
            SetCursor(true);
            UI.ShowTitle();
        }
    }
}
