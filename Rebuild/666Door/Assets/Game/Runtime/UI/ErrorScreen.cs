using TMPro;
using UnityEngine;

namespace Door666.Runtime
{
    /// <summary>Shown when the save file, the stages or the field cannot be loaded (Error.prefab).</summary>
    public sealed class ErrorScreen : UIScreen
    {
        [Tooltip("エラーの内容を書き込む文字。")]
        [SerializeField] private TMP_Text message;

        public void Present(string text) => SetText(message, text);
    }
}
