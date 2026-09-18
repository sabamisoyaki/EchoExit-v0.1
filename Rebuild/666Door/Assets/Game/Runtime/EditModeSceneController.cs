using UnityEngine;

namespace Door666.Runtime
{
    /// <summary>EditMode.unity: walk the field in first person, place items, and try the placed anomalies' rituals.</summary>
    public sealed class EditModeSceneController : SceneController
    {
        public PlacementEditor StageEditor { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            StageEditor = new PlacementEditor(this);
        }

        protected override void Enter()
        {
            Screen = GameScreen.Editing;
            StageEditor.Open();
        }

        private void Update()
        {
            if (Screen == GameScreen.Editing && !Input.IsRebinding) StageEditor.Tick(Time.deltaTime);
        }

        // Free the cursor when the window loses focus, as the Game scene pauses.
        private void OnApplicationFocus(bool focus)
        {
            if (!focus && Screen == GameScreen.Editing && !Application.isBatchMode) StageEditor.SetMenuOpen(true);
        }

        protected override void OnDestroy()
        {
            if (StageEditor != null) StageEditor.Close();
            base.OnDestroy();
        }
    }
}
