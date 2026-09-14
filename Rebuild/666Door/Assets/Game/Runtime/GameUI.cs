using System;
using System.Collections.Generic;
using Door666.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace Door666.Runtime
{
    public sealed class GameUI : MonoBehaviour
    {
        // Screen fractions covered by the stage editor's palette (left) and status bar (bottom).
        public const float EditorPaletteWidth = .25f;
        public const float EditorStatusHeight = .11f;

        private SceneController game;
        private TMP_FontAsset font;
        private RectTransform root;
        private GameObject menu;
        private GameObject hud;
        private TMP_Text timer;
        private TMP_Text streak;
        private TMP_Text prompt;
        private TMP_Text banner;
        private TMP_Text subtitle;
        private TMP_Text editorStatus;
        private TMP_InputField stageId;
        private UnityEngine.UI.Image fade;
        private float bannerUntil;
        private float subtitleUntil;
        private readonly Color ink = new Color(.86f, .83f, .72f);
        private readonly Color muted = new Color(.59f, .61f, .53f);
        private readonly Color accent = new Color(.73f, .48f, .30f);

        public bool TextHasFocus => EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null
            && EventSystem.current.currentSelectedGameObject.GetComponent<TMP_InputField>() != null;
        public bool PointerOverUI => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        public string EditorSceneIdText => stageId != null ? stageId.text : string.Empty;

        public void Initialize(SceneController owner)
        {
            game = owner;
            font = Resources.Load<TMP_FontAsset>(GameConstants.FontResource);
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = .5f;
            gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            root = GetComponent<RectTransform>();
            if (EventSystem.current == null)
            {
                var events = new GameObject("UI Input", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform, false);
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            hud = Panel(root, "HUD", Color.clear, Vector2.zero, Vector2.one);
            streak = Label(hud.transform, "連続正解  0 / 6", 22, new Vector2(.035f, .91f), new Vector2(.30f, .97f));
            timer = Label(hud.transform, "03:00", 30, new Vector2(.79f, .90f), new Vector2(.965f, .97f), TextAlignmentOptions.Right);
            Label(hud.transform, "・", 22, new Vector2(.48f, .47f), new Vector2(.52f, .53f), TextAlignmentOptions.Center);
            prompt = Label(hud.transform, "", 22, new Vector2(.28f, .39f), new Vector2(.72f, .45f), TextAlignmentOptions.Center);
            banner = Label(hud.transform, "", 27, new Vector2(.20f, .78f), new Vector2(.80f, .87f), TextAlignmentOptions.Center);
            subtitle = Label(hud.transform, "", 21, new Vector2(.16f, .08f), new Vector2(.84f, .15f), TextAlignmentOptions.Center);
            var shade = Panel(root, "Transition", Color.clear, Vector2.zero, Vector2.one);
            fade = shade.GetComponent<UnityEngine.UI.Image>();
            fade.raycastTarget = false;
        }

        private void Update()
        {
            if (Time.unscaledTime > bannerUntil) banner.text = "";
            if (Time.unscaledTime > subtitleUntil) subtitle.text = "";
        }

        public void ShowTitle()
        {
            BeginMenu(false);
            var panel = Panel(menu.transform, "Title", new Color(.025f, .033f, .027f, .94f), Vector2.zero, new Vector2(.48f, 1));
            Label(panel.transform, "記憶より、あなたの違和感を信じろ。", 20, new Vector2(.09f, .79f), new Vector2(.93f, .86f)).color = muted;
            Label(panel.transform, "666号扉", 76, new Vector2(.08f, .63f), new Vector2(.95f, .78f));
            Label(panel.transform, "異変がなければ、前へ。\n異変があれば、引き返せ。\n六度、続けて正しく選べ。", 24, new Vector2(.10f, .42f), new Vector2(.93f, .62f));
            Button(panel.transform, "扉を開ける", .30f, () => game.StartRun());
            Button(panel.transform, "部屋を編集する", .22f, () => game.StartEditor());
            Button(panel.transform, "設定", .14f, () => ShowSettings(false));
            Button(panel.transform, "終了", .06f, () => game.Quit());
            Label(menu.transform, "WASD  移動     マウス  視点\nE  扉を操作     左クリック  叩く     Esc  一時停止", 18, new Vector2(.55f, .045f), new Vector2(.96f, .14f)).color = muted;
            SelectFirst();
        }

        public void ShowPlay()
        {
            ClearMenu();
            hud.SetActive(true);
        }

        public void ShowPause()
        {
            BeginMenu(true);
            var panel = Panel(menu.transform, "Pause", new Color(.035f, .043f, .034f, .97f), new Vector2(.30f, .18f), new Vector2(.70f, .82f));
            Label(panel.transform, "しばしの静寂", 40, new Vector2(.10f, .73f), new Vector2(.90f, .91f), TextAlignmentOptions.Center);
            Button(panel.transform, "部屋へ戻る", .52f, () => game.Resume());
            Button(panel.transform, "設定", .35f, () => ShowSettings(true));
            Button(panel.transform, "探索を終えてタイトルへ", .18f, () => game.ShowTitle());
            SelectFirst();
        }

        public void ShowSettings(bool fromPause)
        {
            BeginMenu(true);
            var panel = Panel(menu.transform, "Settings", new Color(.025f, .033f, .027f, .99f), new Vector2(.12f, .06f), new Vector2(.88f, .94f));
            Label(panel.transform, "設定", 38, new Vector2(.06f, .86f), new Vector2(.94f, .96f));
            Slider(panel.transform, "マウス感度", .73f, .025f, .20f, game.Player.MouseSensitivity, value =>
            {
                game.Player.MouseSensitivity = value;
                PlayerPrefs.SetFloat(GameConstants.SensitivityPreference, value);
            });
            Slider(panel.transform, "ゲームパッド感度", .60f, 45f, 230f, game.Player.GamepadSensitivity, value =>
            {
                game.Player.GamepadSensitivity = value;
                PlayerPrefs.SetFloat(GameConstants.GamepadSensitivityPreference, value);
            });
            Button(panel.transform, "音の字幕  :  " + (game.SubtitlesEnabled ? "表示" : "非表示"), .49f, () =>
            {
                game.SubtitlesEnabled = !game.SubtitlesEnabled;
                PlayerPrefs.SetInt(GameConstants.SubtitlePreference, game.SubtitlesEnabled ? 1 : 0);
                ShowSettings(fromPause);
            });
            Label(panel.transform, "キー設定  ／  選択後にキーを押す・Escで取消", 18, new Vector2(.10f, .40f), new Vector2(.94f, .46f)).color = muted;
            var bindings = new[] { ("前進", game.Input.Move, 1), ("後退", game.Input.Move, 2), ("左", game.Input.Move, 3), ("右", game.Input.Move, 4), ("操作", game.Input.Interact, 0), ("叩く", game.Input.Hit, 0) };
            for (int i = 0; i < bindings.Length; i++)
            {
                var binding = bindings[i];
                float x = .10f + (i % 3) * .27f;
                float y = .31f - (i / 3) * .095f;
                var button = ButtonAt(panel.transform, binding.Item1 + "  " + binding.Item2.GetBindingDisplayString(binding.Item3),
                    new Vector2(x, y), new Vector2(x + .25f, y + .073f), null);
                button.onClick.AddListener(() =>
                {
                    if (game.Input.IsRebinding) return;
                    button.GetComponentInChildren<TMP_Text>().text = "キーを押してください";
                    game.Input.Rebind(binding.Item2, binding.Item3, () => ShowSettings(fromPause));
                });
            }
            ButtonAt(panel.transform, "キー設定を初期化", new Vector2(.10f, .09f), new Vector2(.48f, .16f), () =>
            {
                if (game.Input.IsRebinding) return;
                game.Input.ResetBindings();
                ShowSettings(fromPause);
            });
            ButtonAt(panel.transform, "戻る", new Vector2(.53f, .09f), new Vector2(.90f, .16f), () =>
            {
                if (game.Input.IsRebinding) return;
                PlayerPrefs.Save();
                if (fromPause) ShowPause(); else ShowTitle();
            });
            SelectFirst();
        }

        public void ShowEnding(string title, string body)
        {
            BeginMenu(true);
            Label(menu.transform, title, 62, new Vector2(.15f, .60f), new Vector2(.85f, .77f), TextAlignmentOptions.Center);
            Label(menu.transform, body, 25, new Vector2(.20f, .42f), new Vector2(.80f, .60f), TextAlignmentOptions.Center);
            ButtonAt(menu.transform, "もう一度、扉を開ける", new Vector2(.33f, .27f), new Vector2(.67f, .35f), () => game.StartRun());
            ButtonAt(menu.transform, "タイトルへ", new Vector2(.33f, .16f), new Vector2(.67f, .24f), () => game.ShowTitle());
            SelectFirst();
        }

        public void ShowError(string message)
        {
            BeginMenu(true);
            Label(menu.transform, "部屋を開けませんでした", 42, new Vector2(.1f, .65f), new Vector2(.9f, .8f), TextAlignmentOptions.Center);
            Label(menu.transform, message, 22, new Vector2(.12f, .25f), new Vector2(.88f, .62f), TextAlignmentOptions.Center);
            ButtonAt(menu.transform, "タイトルへ", new Vector2(.35f, .1f), new Vector2(.65f, .19f), () => game.ShowTitle());
        }

        public void ShowEditor(PlacementEditor editor, int currentId, bool anomalyCategory)
        {
            BeginMenu(false);
            var panel = Panel(menu.transform, "Stage editing", new Color(.025f, .033f, .027f, .97f), Vector2.zero, new Vector2(EditorPaletteWidth, 1));
            Label(panel.transform, "部屋を編集", 34, new Vector2(.08f, .89f), new Vector2(.92f, .97f));
            Label(panel.transform, "ステージ番号", 18, new Vector2(.08f, .83f), new Vector2(.9f, .89f)).color = muted;
            stageId = Field(panel.transform, currentId.ToString(), new Vector2(.10f, .77f), new Vector2(.44f, .83f));
            ButtonAt(panel.transform, "読込", new Vector2(.48f, .77f), new Vector2(.70f, .83f), () =>
            {
                if (int.TryParse(stageId.text, out int value)) editor.Load(value);
                else EditorMessage("1以上の番号を入力してください。");
            });
            ButtonAt(panel.transform, "新規", new Vector2(.72f, .77f), new Vector2(.94f, .83f), () => editor.New());
            ButtonAt(panel.transform, anomalyCategory ? "通常" : "● 通常", new Vector2(.08f, .68f), new Vector2(.49f, .74f), () => editor.SetCategory(false));
            ButtonAt(panel.transform, anomalyCategory ? "● 異変" : "異変", new Vector2(.51f, .68f), new Vector2(.92f, .74f), () => editor.SetCategory(true));
            int index = 0;
            foreach (var id in game.World.Catalog.PrefabIds)
            {
                string key = id;
                float y = .60f - index++ * .062f;
                ButtonAt(panel.transform, game.World.Catalog.DisplayName(key), new Vector2(.08f, y), new Vector2(.92f, y + .052f), () => editor.Choose(key));
            }
            ButtonAt(panel.transform, "回転 [R]", new Vector2(.08f, .13f), new Vector2(.49f, .19f), () => editor.Rotate());
            ButtonAt(panel.transform, "削除 [Del]", new Vector2(.51f, .13f), new Vector2(.92f, .19f), () => editor.Delete());
            ButtonAt(panel.transform, "保存 [F5]", new Vector2(.08f, .065f), new Vector2(.92f, .12f), () => editor.Save(stageId.text));
            ButtonAt(panel.transform, "タイトルへ（未保存は破棄）", new Vector2(.08f, .008f), new Vector2(.92f, .06f), () => game.ShowTitle());
            // An opaque bar keeps messages readable over the map and stops clicks under it from placing objects.
            var status = Panel(menu.transform, "Editor status", new Color(.025f, .033f, .027f, .94f), new Vector2(EditorPaletteWidth, 0), new Vector2(1, EditorStatusHeight));
            editorStatus = Label(status.transform, "名前を選んで床をクリックすると配置できます。\n配置物をクリックして選択。右クリックで配置を解除。", 19, new Vector2(.025f, .06f), new Vector2(.975f, .94f));
            editorStatus.enableAutoSizing = true;
            editorStatus.fontSizeMin = 12;
            editorStatus.fontSizeMax = 19;
        }

        public void EditorMessage(string message) { if (editorStatus != null) editorStatus.text = message; }
        public void SetHUD(int count, int required, double seconds, string interaction)
        {
            streak.text = "連続正解  " + count + " / " + required + "\n" + new string('●', count) + new string('○', Mathf.Max(0, required - count));
            int time = Mathf.CeilToInt((float)seconds);
            timer.text = (time / 60).ToString("00") + ":" + (time % 60).ToString("00");
            timer.color = time <= 30 ? new Color(.92f, .49f, .36f) : ink;
            prompt.text = interaction;
        }
        public void Banner(string text, float seconds = 2.3f) { banner.text = text; bannerUntil = Time.unscaledTime + seconds; }
        public void Subtitle(string text) { if (game.SubtitlesEnabled) { subtitle.text = "［" + text + "］"; subtitleUntil = Time.unscaledTime + 2.4f; } }
        public void Fade(float opacity) { fade.color = new Color(.014f, .018f, .012f, Mathf.Clamp01(opacity)); fade.transform.SetAsLastSibling(); }

        private void BeginMenu(bool dim)
        {
            ClearMenu();
            hud.SetActive(false);
            Fade(0);
            menu = Panel(root, "Menu", dim ? new Color(.012f, .018f, .012f, .90f) : Color.clear, Vector2.zero, Vector2.one);
        }
        private void ClearMenu() { if (menu != null) { menu.SetActive(false); Destroy(menu); } }
        private void SelectFirst()
        {
            var button = menu.GetComponentInChildren<UnityEngine.UI.Button>();
            if (button != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(button.gameObject);
        }
        private GameObject Panel(Transform parent, string name, Color color, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
            go.transform.SetParent(parent, false);
            SetRect(go.GetComponent<RectTransform>(), min, max);
            var image = go.GetComponent<UnityEngine.UI.Image>();
            image.color = color;
            image.raycastTarget = color.a > .1f;
            return go;
        }
        private TMP_Text Label(Transform parent, string text, float size, Vector2 min, Vector2 max, TextAlignmentOptions alignment = TextAlignmentOptions.Left)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            SetRect(go.GetComponent<RectTransform>(), min, max);
            var label = go.GetComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = text;
            label.fontSize = size;
            label.color = ink;
            label.alignment = alignment;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.raycastTarget = false;
            return label;
        }
        private void Button(Transform parent, string label, float y, Action action) => ButtonAt(parent, label, new Vector2(.10f, y), new Vector2(.90f, y + .067f), action);
        private UnityEngine.UI.Button ButtonAt(Transform parent, string label, Vector2 min, Vector2 max, Action action)
        {
            var go = Panel(parent, label, new Color(.12f, .145f, .107f), min, max);
            var button = go.AddComponent<UnityEngine.UI.Button>();
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.55f, 1.50f, 1.2f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = accent;
            button.colors = colors;
            if (action != null) button.onClick.AddListener(() => action());
            // Button captions stay on one line and shrink instead of wrapping out of narrow buttons.
            var caption = Label(go.transform, label, 21, new Vector2(.05f, .04f), new Vector2(.95f, .96f), TextAlignmentOptions.Center);
            caption.textWrappingMode = TextWrappingModes.NoWrap;
            caption.enableAutoSizing = true;
            caption.fontSizeMin = 12;
            caption.fontSizeMax = 21;
            return button;
        }
        private void Slider(Transform parent, string label, float y, float min, float max, float value, Action<float> changed)
        {
            Label(parent, label, 22, new Vector2(.10f, y + .04f), new Vector2(.80f, y + .105f));
            var track = Panel(parent, label, new Color(.19f, .22f, .17f), new Vector2(.12f, y), new Vector2(.88f, y + .025f));
            var slider = track.AddComponent<UnityEngine.UI.Slider>();
            var handle = Panel(track.transform, "Handle", ink, Vector2.zero, Vector2.one).GetComponent<RectTransform>();
            handle.sizeDelta = new Vector2(22, 18);
            slider.handleRect = handle;
            slider.targetGraphic = handle.GetComponent<UnityEngine.UI.Image>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = value;
            slider.onValueChanged.AddListener(v => changed(v));
        }
        private TMP_InputField Field(Transform parent, string value, Vector2 min, Vector2 max)
        {
            var go = Panel(parent, "StageId", new Color(.15f, .18f, .13f), min, max);
            var field = go.AddComponent<TMP_InputField>();
            var label = Label(go.transform, "", 24, new Vector2(.1f, .08f), new Vector2(.9f, .92f));
            field.textViewport = label.rectTransform;
            field.textComponent = (TextMeshProUGUI)label;
            field.contentType = TMP_InputField.ContentType.IntegerNumber;
            field.text = value;
            return field;
        }
        private static void SetRect(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
        }
    }
}
