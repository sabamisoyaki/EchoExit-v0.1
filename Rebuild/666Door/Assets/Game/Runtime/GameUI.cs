using Door666.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Door666.Runtime
{
    /// <summary>
    /// The root of Interface.prefab, placed in every screen scene. It finds its screens among its children, switches
    /// between them and wires every UIActionButton. The look and wording live in the prefabs under Assets/Prefabs/UI.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas))]
    public sealed class GameUI : MonoBehaviour
    {
        [Tooltip("暗転に使う全画面の画像。色は画像の色（RGB）を使い、濃さはコードで変える。")]
        [SerializeField] private Image fade;

        private SceneController game;
        private PlayHud hud;
        private EditorHud editorHud;
        private TitleScreen title;
        private PauseScreen pause;
        private SettingsScreen settings;
        private EndingScreen ending;
        private ErrorScreen error;
        private EditorMenu editorMenu;
        private UIScreen[] screens;
        private bool settingsFromPause;
        private bool editorMenuReady;
        private string editorMessage = "";

        public bool TextHasFocus => EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null
            && EventSystem.current.currentSelectedGameObject.GetComponent<TMP_InputField>() != null;
        public bool PointerOverUI => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        public string EditorSceneIdText => editorMenu.StageIdText;

        public void Initialize(SceneController owner)
        {
            game = owner;
            hud = Find<PlayHud>("HUD");
            editorHud = Find<EditorHud>("EditorHud");
            title = Find<TitleScreen>("Title");
            pause = Find<PauseScreen>("Pause");
            settings = Find<SettingsScreen>("Settings");
            ending = Find<EndingScreen>("Ending");
            error = Find<ErrorScreen>("Error");
            editorMenu = Find<EditorMenu>("EditorMenu");
            screens = new UIScreen[] { hud, editorHud, title, pause, settings, ending, error, editorMenu };
            // Screens left visible while editing the prefab are hidden until the scene asks for them.
            foreach (var screen in screens) screen.SetShown(false);
            hud.Clear();
            settings.Initialize(owner);
            foreach (var button in GetComponentsInChildren<UIActionButton>(true))
            {
                var target = button;
                target.Button.onClick.AddListener(() => Perform(target.action));
            }
            if (fade != null) fade.raycastTarget = false;
            Fade(0);
            if (EventSystem.current == null)
            {
                var events = new GameObject("UI Input", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform, false);
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
        }

        public void ShowTitle() => ShowMenu(title);

        public void ShowPlay()
        {
            Switch(null, true, false);
            Deselect();
        }

        public void ShowPause() => ShowMenu(pause);

        public void ShowSettings(bool fromPause)
        {
            settingsFromPause = fromPause;
            settings.Refresh();
            ShowMenu(settings);
        }

        public void ShowEnding(RunEndReason reason, string capturedBy)
        {
            ending.Present(reason, capturedBy);
            ShowMenu(ending);
        }

        public void ShowError(string message)
        {
            error.Present(message);
            ShowMenu(error);
        }

        /// <summary>Stage editing while walking: the crosshair, what will be placed, the latest message and the controls.</summary>
        public void ShowEditorHud(EditorStatus state)
        {
            hud.HideRun();
            hud.Prompt("");
            editorHud.Present(state);
            editorHud.SetMessage(editorMessage);
            Switch(null, true, true);
            Fade(0);
            Deselect();
        }

        /// <summary>The Tab menu: choose what to place, switch stages, save, or leave.</summary>
        public void ShowEditorMenu(EditorStatus state)
        {
            if (!editorMenuReady)
            {
                editorMenu.Initialize(game.World.Catalog, id => { var editor = StageEditor; if (editor != null) editor.Choose(id); });
                editorMenuReady = true;
            }
            editorMenu.Present(state);
            editorMenu.SetMessage(editorMessage);
            Switch(editorMenu, false, false);
            Fade(0);
        }

        public void SetHUD(int count, int required, double seconds) => hud.SetRun(count, required, seconds);
        /// <summary>The line under the crosshair: the door action while exploring, placement details while editing.</summary>
        public void Prompt(string text) => hud.Prompt(text);
        public void DoorPrompt(DoorTarget door, string key) => hud.DoorPrompt(door, key);
        public void DoorResult(bool correct, int count, int required, float seconds) => hud.DoorResult(correct, count, required, seconds);
        public void Recognized(bool threat, bool editing = false) => hud.Recognized(threat, editing);
        public void CaughtWhileEditing(string anomalyName) => hud.CaughtWhileEditing(anomalyName);
        public void Subtitle(string text) { if (game.SubtitlesEnabled) hud.Subtitle(text); }

        public void EditorMessage(string message)
        {
            editorMessage = message;
            editorHud.SetMessage(message);
            editorMenu.SetMessage(message);
        }

        public void Fade(float opacity)
        {
            if (fade == null) return;
            var color = fade.color;
            color.a = Mathf.Clamp01(opacity);
            fade.color = color;
        }

        private void Perform(UIAction action)
        {
            if (game.Input.IsRebinding) return;
            GameLog.Detail("画面", "ボタン: " + action);
            var editor = StageEditor;
            switch (action)
            {
                case UIAction.StartRun: game.StartRun(); break;
                case UIAction.StartEditor: game.StartEditor(); break;
                case UIAction.OpenSettings: ShowSettings(game.Screen == GameScreen.Paused); break;
                case UIAction.CloseSettings:
                    PlayerPrefs.Save();
                    if (settingsFromPause) ShowPause(); else ShowTitle();
                    break;
                case UIAction.Resume: game.Resume(); break;
                case UIAction.BackToTitle: game.ShowTitle(); break;
                case UIAction.Quit: game.Quit(); break;
                case UIAction.ToggleSubtitles:
                    game.SubtitlesEnabled = !game.SubtitlesEnabled;
                    PlayerPrefs.SetInt(GameConstants.SubtitlePreference, game.SubtitlesEnabled ? 1 : 0);
                    GameLog.Info("画面", "音の字幕を" + (game.SubtitlesEnabled ? "オン" : "オフ") + "にしました。");
                    settings.Refresh();
                    break;
                case UIAction.ResetBindings:
                    game.Input.ResetBindings();
                    settings.Refresh();
                    break;
                case UIAction.EditorLoad:
                    if (editor == null) break;
                    if (int.TryParse(EditorSceneIdText, out int value)) editor.Load(value);
                    else EditorMessage(editorMenu.InvalidStageIdMessage);
                    break;
                case UIAction.EditorNew: if (editor != null) editor.New(); break;
                case UIAction.EditorSave: if (editor != null) editor.Save(EditorSceneIdText); break;
                case UIAction.EditorNormal: if (editor != null) editor.SetCategory(false); break;
                case UIAction.EditorAnomaly: if (editor != null) editor.SetCategory(true); break;
                case UIAction.EditorCloseMenu: if (editor != null) editor.SetMenuOpen(false); break;
            }
        }

        private PlacementEditor StageEditor => game is EditModeSceneController edit ? edit.StageEditor : null;

        private void ShowMenu(UIScreen menu)
        {
            Switch(menu, false, false);
            Fade(0);
            menu.SelectFirst();
        }

        private void Switch(UIScreen menu, bool hudShown, bool editorHudShown)
        {
            foreach (var screen in screens)
                screen.SetShown(screen == menu || (screen == hud && hudShown) || (screen == editorHud && editorHudShown));
        }

        // A hidden input field must not keep the keyboard, or walking keys would be typed into it.
        private static void Deselect()
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        private T Find<T>(string prefab) where T : UIScreen
        {
            var screen = GetComponentInChildren<T>(true);
            if (screen == null)
                throw new MissingReferenceException(name + " に画面「" + prefab + "」（" + typeof(T).Name + "）がありません。Assets/Prefabs/UI/"
                    + prefab + ".prefab を子として置いてください。");
            return screen;
        }
    }
}
