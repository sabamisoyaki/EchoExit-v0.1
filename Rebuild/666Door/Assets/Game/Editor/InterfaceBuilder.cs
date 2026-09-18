using System;
using Door666.Runtime;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Door666.Editor
{
    /// <summary>
    /// Generates the interface prefabs under Assets/Prefabs/UI: one prefab per screen, nested in Interface.prefab.
    /// Only missing prefabs are created; existing ones are left for hand editing.
    /// </summary>
    public static class InterfaceBuilder
    {
        public const string Folder = "Assets/Prefabs/UI";
        public const string InterfacePath = Folder + "/Interface.prefab";
        private const string FontPath = "Assets/Resources/Fonts/Japanese.asset";
        private const string EditorControls = "Shift＋クリック  置く     ホイール  向きを5°ずつ（Ctrl で 45°・0.25m 刻み）     R  45°回転     Delete  削除     左クリック  叩く     F5  保存     Tab  メニュー";

        private static readonly Color Ink = new Color(.86f, .83f, .72f);
        private static readonly Color Muted = new Color(.59f, .61f, .53f);
        private static readonly Color Accent = new Color(.73f, .48f, .30f);
        private static readonly Color Face = new Color(.12f, .145f, .107f);
        private static readonly Color Dim = new Color(.012f, .018f, .012f, .90f);

        // Drawing order: the HUDs at the back, the menus above them.
        private static readonly (string Name, Action<GameObject> Build)[] Screens =
        {
            ("HUD", BuildHud), ("EditorHud", BuildEditorHud), ("Title", BuildTitle), ("Pause", BuildPause),
            ("Settings", BuildSettings), ("Ending", BuildEnding), ("Error", BuildError), ("EditorMenu", BuildEditorMenu)
        };

        private static TMP_FontAsset font;

        /// <summary>Creates the missing screen prefabs and Interface.prefab, and returns Interface.prefab.</summary>
        public static GameObject EnsurePrefabs()
        {
            ProjectBootstrap.EnsureFolder(Folder);
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null) throw new InvalidOperationException("日本語フォントがありません: " + FontPath);
            var screens = new GameObject[Screens.Length];
            for (int i = 0; i < Screens.Length; i++) screens[i] = EnsureScreen(Screens[i].Name, Screens[i].Build);
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(InterfacePath);
            return existing != null ? existing : Save(BuildInterface(screens), InterfacePath);
        }

        private static GameObject EnsureScreen(string name, Action<GameObject> build)
        {
            string path = Folder + "/" + name + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            var root = new GameObject(name, typeof(RectTransform));
            Stretch(root.GetComponent<RectTransform>());
            build(root);
            return Save(root, path);
        }

        private static GameObject BuildInterface(GameObject[] screens)
        {
            var root = new GameObject("Interface", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(GameUI));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = .5f;
            foreach (var screen in screens)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(screen, root.transform);
                Stretch(instance.GetComponent<RectTransform>());
                // Kept out of the way in the scene view; GameUI shows the screen the scene needs.
                instance.SetActive(false);
            }
            var fade = Panel(root.transform, "Fade", new Color(.014f, .018f, .012f, 0), Vector2.zero, Vector2.one).GetComponent<Image>();
            fade.raycastTarget = false;
            Bind(root.GetComponent<GameUI>(), ("fade", fade));
            return root;
        }

        private static void BuildHud(GameObject root)
        {
            var t = root.transform;
            var hud = root.AddComponent<PlayHud>();
            var streak = Label(t, "Streak", "連続正解  0 / 6\n○○○○○○", 22, V(.035f, .91f), V(.30f, .97f));
            var timer = Label(t, "Timer", "03:00", 30, V(.79f, .90f), V(.965f, .97f), TextAlignmentOptions.Right);
            Label(t, "Crosshair", "・", 22, V(.48f, .47f), V(.52f, .53f), TextAlignmentOptions.Center);
            var prompt = Label(t, "Prompt", "[E]  前へ進む", 22, V(.28f, .39f), V(.72f, .45f), TextAlignmentOptions.Center);
            var banner = Label(t, "Banner", "扉の先へ。  1 / 6", 27, V(.20f, .78f), V(.80f, .87f), TextAlignmentOptions.Center);
            var subtitle = Label(t, "Subtitle", "［低い電気音］", 21, V(.16f, .08f), V(.84f, .15f), TextAlignmentOptions.Center);
            Bind(hud, ("streak", streak), ("timer", timer), ("prompt", prompt), ("banner", banner), ("subtitle", subtitle));
        }

        private static void BuildEditorHud(GameObject root)
        {
            var t = root.transform;
            var hud = root.AddComponent<EditorHud>();
            var stage = Label(t, "Stage", "部屋を編集  ステージ 1", 22, V(.035f, .91f), V(.50f, .97f));
            var selection = Label(t, "Selection", "置くもの：未選択（Tab で選ぶ）", 22, V(.50f, .91f), V(.965f, .97f), TextAlignmentOptions.Right);
            var budget = Label(t, "Budget", "異変 0 / 6     重なりの大きい異変 0 / 0", 18, V(.035f, .86f), V(.60f, .91f), color: Muted);
            var status = Label(t, "Message", "Tab でメニューを開き、置くものを選んでください。置いた異変はその場で儀式を試せます。", 20, V(.16f, .16f), V(.84f, .24f), TextAlignmentOptions.Center);
            AutoSize(status, 14);
            Label(t, "Controls", EditorControls, 17, V(.03f, .025f), V(.97f, .075f), TextAlignmentOptions.Center, Muted);
            Bind(hud, ("stage", stage), ("selection", selection), ("budget", budget), ("status", status));
        }

        private static void BuildTitle(GameObject root)
        {
            var screen = root.AddComponent<TitleScreen>();
            var panel = Panel(root.transform, "Panel", new Color(.025f, .033f, .027f, .94f), Vector2.zero, V(.48f, 1)).transform;
            Label(panel, "Tagline", "記憶より、あなたの違和感を信じろ。", 20, V(.09f, .79f), V(.93f, .86f), color: Muted);
            Label(panel, "Title", "666号扉", 76, V(.08f, .63f), V(.95f, .78f));
            Label(panel, "Rules", "異変がなければ、前へ。\n異変があれば、引き返せ。\n六度、続けて正しく選べ。", 24, V(.10f, .42f), V(.93f, .62f));
            var first = MenuButton(panel, "扉を開ける", .30f, UIAction.StartRun);
            MenuButton(panel, "部屋を編集する", .22f, UIAction.StartEditor);
            MenuButton(panel, "設定", .14f, UIAction.OpenSettings);
            MenuButton(panel, "終了", .06f, UIAction.Quit);
            Label(root.transform, "Controls", "WASD  移動     マウス  視点\nE  扉を操作     左クリック  叩く     Esc  一時停止", 18, V(.55f, .045f), V(.96f, .14f), color: Muted);
            Bind(screen, ("firstSelected", first));
        }

        private static void BuildPause(GameObject root)
        {
            Backdrop(root);
            var screen = root.AddComponent<PauseScreen>();
            var panel = Panel(root.transform, "Panel", new Color(.035f, .043f, .034f, .97f), V(.30f, .18f), V(.70f, .82f)).transform;
            Label(panel, "Heading", "しばしの静寂", 40, V(.10f, .73f), V(.90f, .91f), TextAlignmentOptions.Center);
            var first = MenuButton(panel, "部屋へ戻る", .52f, UIAction.Resume);
            MenuButton(panel, "設定", .35f, UIAction.OpenSettings);
            MenuButton(panel, "探索を終えてタイトルへ", .18f, UIAction.BackToTitle);
            Bind(screen, ("firstSelected", first));
        }

        private static void BuildSettings(GameObject root)
        {
            Backdrop(root);
            var screen = root.AddComponent<SettingsScreen>();
            var panel = Panel(root.transform, "Panel", new Color(.025f, .033f, .027f, .99f), V(.12f, .06f), V(.88f, .94f)).transform;
            Label(panel, "Heading", "設定", 38, V(.06f, .86f), V(.94f, .96f));
            var mouse = Slider(panel, "マウス感度", .73f, .025f, .20f);
            var gamepad = Slider(panel, "ゲームパッド感度", .60f, 45f, 230f);
            var subtitles = MenuButton(panel, "音の字幕  :  表示", .49f, UIAction.ToggleSubtitles);
            Label(panel, "Key bindings", "キー設定  ／  選択後にキーを押す・Escで取消", 18, V(.10f, .40f), V(.94f, .46f), color: Muted);
            var bindings = new[]
            {
                ("前進", BindableAction.Move, 1), ("後退", BindableAction.Move, 2), ("左", BindableAction.Move, 3),
                ("右", BindableAction.Move, 4), ("操作", BindableAction.Interact, 0), ("叩く", BindableAction.Hit, 0)
            };
            for (int i = 0; i < bindings.Length; i++)
            {
                float x = .10f + (i % 3) * .27f;
                float y = .31f - (i / 3) * .095f;
                var button = Button(panel, bindings[i].Item1, V(x, y), V(x + .25f, y + .073f), UIAction.None);
                var binding = button.gameObject.AddComponent<KeyBindingButton>();
                binding.label = bindings[i].Item1;
                binding.action = bindings[i].Item2;
                binding.bindingIndex = bindings[i].Item3;
                Bind(binding, ("caption", Caption(button)));
            }
            Button(panel, "キー設定を初期化", V(.10f, .09f), V(.48f, .16f), UIAction.ResetBindings);
            Button(panel, "戻る", V(.53f, .09f), V(.90f, .16f), UIAction.CloseSettings);
            Bind(screen, ("firstSelected", subtitles), ("mouseSensitivity", mouse), ("gamepadSensitivity", gamepad), ("subtitleCaption", Caption(subtitles)));
        }

        private static void BuildEnding(GameObject root)
        {
            Backdrop(root);
            var screen = root.AddComponent<EndingScreen>();
            var t = root.transform;
            var title = Label(t, "Title", "扉の向こうへ", 62, V(.15f, .60f), V(.85f, .77f), TextAlignmentOptions.Center);
            var body = Label(t, "Body", "六度の判断が、あなたを外へ連れ出した。\nそれでも、背後の扉を見てはいけない。", 25, V(.20f, .42f), V(.80f, .60f), TextAlignmentOptions.Center);
            var first = Button(t, "もう一度、扉を開ける", V(.33f, .27f), V(.67f, .35f), UIAction.StartRun);
            Button(t, "タイトルへ", V(.33f, .16f), V(.67f, .24f), UIAction.BackToTitle);
            Bind(screen, ("firstSelected", first), ("title", title), ("body", body));
        }

        private static void BuildError(GameObject root)
        {
            Backdrop(root);
            var screen = root.AddComponent<ErrorScreen>();
            var t = root.transform;
            Label(t, "Heading", "部屋を開けませんでした", 42, V(.1f, .65f), V(.9f, .8f), TextAlignmentOptions.Center);
            var message = Label(t, "Message", "（ここにエラーの内容が入ります）", 22, V(.12f, .25f), V(.88f, .62f), TextAlignmentOptions.Center);
            var first = Button(t, "タイトルへ", V(.35f, .1f), V(.65f, .19f), UIAction.BackToTitle);
            Bind(screen, ("firstSelected", first), ("message", message));
        }

        private static void BuildEditorMenu(GameObject root)
        {
            Backdrop(root);
            var menu = root.AddComponent<EditorMenu>();
            var panel = Panel(root.transform, "Panel", new Color(.025f, .033f, .027f, .97f), V(.14f, .05f), V(.86f, .95f)).transform;
            Label(panel, "Heading", "部屋を編集", 36, V(.05f, .88f), V(.55f, .97f));
            var unsaved = Label(panel, "Unsaved", "未保存の変更があります", 18, V(.55f, .89f), V(.95f, .95f), TextAlignmentOptions.Right, Accent);

            Label(panel, "Stage number label", "ステージ番号", 18, V(.05f, .81f), V(.30f, .86f), color: Muted);
            var stageId = InputField(panel, "Stage number", "1", V(.05f, .73f), V(.19f, .80f));
            Button(panel, "読込", V(.21f, .73f), V(.33f, .80f), UIAction.EditorLoad);
            Button(panel, "新規", V(.35f, .73f), V(.47f, .80f), UIAction.EditorNew);
            Button(panel, "保存 [F5]", V(.49f, .73f), V(.67f, .80f), UIAction.EditorSave);

            Label(panel, "Catalog label", "置くもの", 18, V(.05f, .64f), V(.14f, .69f), color: Muted);
            var budget = Label(panel, "Budget", "異変 0 / 6     重なりの大きい異変 0 / 0", 16, V(.15f, .64f), V(.50f, .69f), color: Muted);
            var normal = Button(panel, "通常", V(.51f, .63f), V(.72f, .70f), UIAction.EditorNormal);
            var anomaly = Button(panel, "異変", V(.74f, .63f), V(.95f, .70f), UIAction.EditorAnomaly);

            // Cell sizes are in reference pixels (1600×900): two columns filling the panel's width.
            var list = new GameObject("Catalog", typeof(RectTransform), typeof(GridLayoutGroup));
            list.transform.SetParent(panel, false);
            Rect(list, V(.05f, .25f), V(.95f, .59f));
            var grid = list.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(507, 57);
            grid.spacing = new Vector2(23, 12);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            // Visible in the prefab so its look can be edited; hidden at run time and copied per catalog entry.
            var template = Button(list.transform, "席を数える椅子", Vector2.zero, Vector2.one, UIAction.None);
            template.name = "Item template";

            var status = Label(panel, "Message", "ステージ 1 を読み込みました。", 19, V(.05f, .13f), V(.95f, .25f));
            AutoSize(status, 13);
            Label(panel, "Controls", EditorControls, 15, V(.05f, .045f), V(.58f, .12f), color: Muted);
            Button(panel, "閉じる [Tab]", V(.60f, .045f), V(.76f, .115f), UIAction.EditorCloseMenu);
            Button(panel, "タイトルへ（未保存は破棄）", V(.78f, .045f), V(.95f, .115f), UIAction.BackToTitle);
            Bind(menu, ("stageId", stageId), ("unsaved", unsaved), ("budget", budget), ("status", status),
                ("normalCaption", Caption(normal)), ("anomalyCaption", Caption(anomaly)),
                ("catalogList", list.GetComponent<RectTransform>()), ("catalogItemTemplate", template));
        }

        private static Vector2 V(float x, float y) => new Vector2(x, y);

        private static GameObject Save(GameObject root, string path)
        {
            try { return PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { Object.DestroyImmediate(root); }
        }

        private static void Rect(GameObject go, Vector2 min, Vector2 max)
        {
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>Dims the room behind a menu and keeps clicks from reaching it.</summary>
        private static void Backdrop(GameObject root)
        {
            var image = root.AddComponent<Image>();
            image.color = Dim;
            image.raycastTarget = true;
        }

        private static GameObject Panel(Transform parent, string name, Color color, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Rect(go, min, max);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = color.a > .1f;
            return go;
        }

        private static TextMeshProUGUI Label(Transform parent, string name, string text, float size, Vector2 min, Vector2 max,
            TextAlignmentOptions alignment = TextAlignmentOptions.Left, Color? color = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            Rect(go, min, max);
            var label = go.GetComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = text;
            label.fontSize = size;
            label.color = color ?? Ink;
            label.alignment = alignment;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.raycastTarget = false;
            return label;
        }

        private static void AutoSize(TMP_Text label, float minimum)
        {
            label.fontSizeMax = label.fontSize;
            label.fontSizeMin = minimum;
            label.enableAutoSizing = true;
        }

        private static Button MenuButton(Transform parent, string caption, float y, UIAction action) =>
            Button(parent, caption, V(.10f, y), V(.90f, y + .067f), action);

        private static Button Button(Transform parent, string caption, Vector2 min, Vector2 max, UIAction action)
        {
            var go = Panel(parent, caption, Face, min, max);
            var button = go.AddComponent<Button>();
            button.targetGraphic = go.GetComponent<Image>();
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.55f, 1.50f, 1.2f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = Accent;
            button.colors = colors;
            if (action != UIAction.None) go.AddComponent<UIActionButton>().action = action;
            // Captions stay on one line and shrink instead of wrapping out of narrow buttons.
            var label = Label(go.transform, "Caption", caption, 21, V(.05f, .04f), V(.95f, .96f), TextAlignmentOptions.Center);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.enableAutoSizing = true;
            label.fontSizeMin = 12;
            label.fontSizeMax = 21;
            return button;
        }

        private static TMP_Text Caption(Button button) => button.GetComponentInChildren<TMP_Text>(true);

        private static Slider Slider(Transform parent, string caption, float y, float min, float max)
        {
            Label(parent, caption + " label", caption, 22, V(.10f, y + .04f), V(.80f, y + .105f));
            var track = Panel(parent, caption, new Color(.19f, .22f, .17f), V(.12f, y), V(.88f, y + .025f));
            var slider = track.AddComponent<Slider>();
            var handle = Panel(track.transform, "Handle", Ink, Vector2.zero, Vector2.one).GetComponent<RectTransform>();
            handle.sizeDelta = new Vector2(22, 18);
            slider.handleRect = handle;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = (min + max) / 2;
            return slider;
        }

        private static TMP_InputField InputField(Transform parent, string name, string value, Vector2 min, Vector2 max)
        {
            var go = Panel(parent, name, new Color(.15f, .18f, .13f), min, max);
            var field = go.AddComponent<TMP_InputField>();
            field.targetGraphic = go.GetComponent<Image>();
            var label = Label(go.transform, "Text", "", 24, V(.1f, .08f), V(.9f, .92f));
            field.textViewport = label.rectTransform;
            field.textComponent = label;
            field.contentType = TMP_InputField.ContentType.IntegerNumber;
            field.text = value;
            return field;
        }

        private static void Bind(Object target, params (string Field, Object Value)[] references)
        {
            var serialized = new SerializedObject(target);
            foreach (var (field, value) in references)
            {
                var property = serialized.FindProperty(field);
                if (property == null) throw new InvalidOperationException(target.GetType().Name + " に " + field + " がありません。");
                property.objectReferenceValue = value;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
