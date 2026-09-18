using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Door666.Runtime
{
    public enum BindableAction
    {
        [InspectorName("移動")] Move,
        [InspectorName("扉を操作")] Interact,
        [InspectorName("叩く")] Hit
    }

    /// <summary>A key-rebinding button on the settings screen. Duplicate it to offer another binding.</summary>
    [RequireComponent(typeof(Button))]
    [DisallowMultipleComponent]
    public sealed class KeyBindingButton : MonoBehaviour
    {
        [Tooltip("ボタンに出す名前。")]
        public string label = "前進";
        [Tooltip("割り当てを変える操作。")]
        public BindableAction action;
        [Tooltip("操作の中の何番目の割り当てか。移動は 1 前進 / 2 後退 / 3 左 / 4 右、ほかは 0（キーボード・マウス）。")]
        [Min(0)] public int bindingIndex;
        [Tooltip("キー名を書き込む文字。空ならボタンの子から探す。")]
        [SerializeField] private TMP_Text caption;

        public Button Button => GetComponent<Button>();

        public void SetCaption(string text)
        {
            if (caption == null) caption = GetComponentInChildren<TMP_Text>(true);
            if (caption != null) caption.text = text;
        }
    }
}
