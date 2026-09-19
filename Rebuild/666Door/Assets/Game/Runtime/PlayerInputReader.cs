using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Door666.Runtime
{
    public sealed class PlayerInputReader : IDisposable
    {
        public InputActionAsset Actions { get; }
        public InputAction Move { get; }
        public InputAction Look { get; }
        public InputAction Interact { get; }
        public InputAction Hit { get; }
        public InputAction Pause { get; }
        public InputAction Rotate { get; }
        public InputAction Delete { get; }
        public InputAction Save { get; }
        public bool IsRebinding => rebinding != null;
        public bool UsingGamepad { get; private set; }
        private InputActionRebindingExtensions.RebindingOperation rebinding;

        public PlayerInputReader()
        {
            Actions = UnityEngine.Object.Instantiate(Resources.Load<InputActionAsset>(GameConstants.InputResource));
            Move = Actions.FindAction("Player/Move", true);
            Look = Actions.FindAction("Player/Look", true);
            Interact = Actions.FindAction("Player/Interact", true);
            Hit = Actions.FindAction("Player/Hit", true);
            Pause = Actions.FindAction("Player/Pause", true);
            Rotate = Actions.FindAction("Player/Rotate", true);
            Delete = Actions.FindAction("Player/Delete", true);
            Save = Actions.FindAction("Player/Save", true);
            string saved = PlayerPrefs.GetString(GameConstants.BindingPreference, "");
            if (!string.IsNullOrEmpty(saved))
            {
                try { Actions.LoadBindingOverridesFromJson(saved); }
                catch (Exception exception) { GameLog.Warning("入力", "キー設定を読み込めないため初期設定を使用します: " + exception.Message); }
            }
            Actions.FindActionMap("Player", true).actionTriggered += OnAction;
            Actions.Enable();
        }

        private void OnAction(InputAction.CallbackContext context)
        {
            if (context.performed && context.control != null)
                UsingGamepad = context.control.device is Gamepad;
        }

        public string Binding(InputAction action) => action.GetBindingDisplayString(UsingGamepad ? 1 : 0);

        public void Rebind(InputAction action, int index, Action completed)
        {
            if (IsRebinding) return;
            action.Disable();
            rebinding = action.PerformInteractiveRebinding(index)
                .WithControlsExcluding("<Pointer>/position")
                .WithControlsExcluding("<Pointer>/delta")
                .WithControlsExcluding("<Gamepad>")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnCancel(_ =>
                {
                    GameLog.Info("入力", action.name + " のキー設定を取り消しました。");
                    FinishRebinding(action, completed);
                })
                .OnComplete(_ =>
                {
                    PlayerPrefs.SetString(GameConstants.BindingPreference, Actions.SaveBindingOverridesAsJson());
                    PlayerPrefs.Save();
                    GameLog.Info("入力", action.name + " のキーを「" + action.GetBindingDisplayString(index) + "」に変更しました。");
                    FinishRebinding(action, completed);
                });
            rebinding.Start();
        }

        private void FinishRebinding(InputAction action, Action completed)
        {
            rebinding.Dispose();
            rebinding = null;
            action.Enable();
            completed?.Invoke();
        }

        public void ResetBindings()
        {
            Actions.RemoveAllBindingOverrides();
            PlayerPrefs.DeleteKey(GameConstants.BindingPreference);
            PlayerPrefs.Save();
            GameLog.Info("入力", "キー設定を初期設定に戻しました。");
        }

        public void Dispose()
        {
            rebinding?.Dispose();
            Actions.FindActionMap("Player", true).actionTriggered -= OnAction;
            Actions.Disable();
            UnityEngine.Object.Destroy(Actions);
        }
    }
}
