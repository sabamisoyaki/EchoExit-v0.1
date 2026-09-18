using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Door666.Runtime
{
    /// <summary>Sensitivity, subtitles and key bindings (Settings.prefab). The slider ranges are set on the sliders.</summary>
    public sealed class SettingsScreen : UIScreen
    {
        [SerializeField] private Slider mouseSensitivity;
        [SerializeField] private Slider gamepadSensitivity;
        [Tooltip("「音の字幕を切り替える」ボタンの文字。")]
        [SerializeField] private TMP_Text subtitleCaption;

        [Header("文言")]
        [Tooltip("{0} は下の「表示」「非表示」。")]
        [SerializeField] private string subtitleFormat = "音の字幕  :  {0}";
        [SerializeField] private string subtitleOn = "表示";
        [SerializeField] private string subtitleOff = "非表示";
        [Tooltip("キー設定のボタン。{0} ボタンの名前、{1} 割り当てたキー。")]
        [SerializeField] private string bindingFormat = "{0}  {1}";
        [SerializeField] private string waitingForKey = "キーを押してください";

        private SceneController game;
        private KeyBindingButton[] bindings = new KeyBindingButton[0];

        public void Initialize(SceneController owner)
        {
            game = owner;
            if (mouseSensitivity != null) mouseSensitivity.onValueChanged.AddListener(value =>
            {
                game.Player.MouseSensitivity = value;
                PlayerPrefs.SetFloat(GameConstants.SensitivityPreference, value);
            });
            if (gamepadSensitivity != null) gamepadSensitivity.onValueChanged.AddListener(value =>
            {
                game.Player.GamepadSensitivity = value;
                PlayerPrefs.SetFloat(GameConstants.GamepadSensitivityPreference, value);
            });
            bindings = GetComponentsInChildren<KeyBindingButton>(true);
            foreach (var binding in bindings)
            {
                var target = binding;
                target.Button.onClick.AddListener(() => Rebind(target));
            }
        }

        public void Refresh()
        {
            if (game == null) return;
            if (mouseSensitivity != null) mouseSensitivity.SetValueWithoutNotify(game.Player.MouseSensitivity);
            if (gamepadSensitivity != null) gamepadSensitivity.SetValueWithoutNotify(game.Player.GamepadSensitivity);
            SetText(subtitleCaption, Format(subtitleFormat, game.SubtitlesEnabled ? subtitleOn : subtitleOff));
            foreach (var binding in bindings)
            {
                var action = Action(binding.action);
                bool valid = binding.bindingIndex < action.bindings.Count;
                binding.SetCaption(Format(bindingFormat, binding.label, valid ? action.GetBindingDisplayString(binding.bindingIndex) : "?"));
            }
        }

        private void Rebind(KeyBindingButton binding)
        {
            var action = Action(binding.action);
            if (game.Input.IsRebinding || binding.bindingIndex >= action.bindings.Count) return;
            binding.SetCaption(waitingForKey);
            game.Input.Rebind(action, binding.bindingIndex, Refresh);
        }

        private InputAction Action(BindableAction action)
        {
            switch (action)
            {
                case BindableAction.Interact: return game.Input.Interact;
                case BindableAction.Hit: return game.Input.Hit;
                default: return game.Input.Move;
            }
        }
    }
}
