namespace Door666.Runtime
{
    public static class GameConstants
    {
        // Screen scenes replace each other; each one loads the shared field scene additively.
        public const string TitleScene = "Title";
        public const string GameScene = "Game";
        public const string EditModeScene = "EditMode";
        public const string FieldScene = "Field";
        public static readonly string[] BuildScenes = { TitleScene, GameScene, EditModeScene, FieldScene };
        public static string ScenePath(string scene) => "Assets/Scenes/" + scene + ".unity";

        public const string InputResource = "PlayerControls";
        public const string DefinitionsResource = "AnomalyDefinitions";
        public const string DefaultStagesResource = "DefaultAnomalies";
        public const string SettingsResource = "GameSettings";
        public const string SoundsResource = "SoundLibrary";
        public const string FontResource = "Fonts/Japanese";
        public const string CatalogMaterialResource = "Materials/Catalog";
        public const string SensitivityPreference = "Door666.MouseSensitivity";
        public const string GamepadSensitivityPreference = "Door666.GamepadSensitivity";
        public const string BindingPreference = "Door666.Bindings";
        public const string SubtitlePreference = "Door666.Subtitles";
    }
}
