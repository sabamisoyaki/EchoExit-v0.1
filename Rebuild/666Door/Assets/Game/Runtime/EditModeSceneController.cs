namespace Door666.Runtime
{
    /// <summary>EditMode.unity: edits stage placements inside the shared field.</summary>
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
            SetCursor(true);
            StageEditor.Open();
        }

        private void Update()
        {
            if (Screen == GameScreen.Editing && !Input.IsRebinding) StageEditor.Tick();
        }

        protected override void OnDestroy()
        {
            if (StageEditor != null) StageEditor.Close();
            base.OnDestroy();
        }
    }
}
