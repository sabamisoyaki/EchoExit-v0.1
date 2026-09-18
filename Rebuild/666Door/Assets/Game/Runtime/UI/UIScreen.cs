using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Door666.Runtime
{
    /// <summary>
    /// One screen of Interface.prefab. Layout, colours and fixed wording are edited in the screen's prefab;
    /// code only switches screens and fills in the values that change.
    /// </summary>
    [DisallowMultipleComponent]
    public class UIScreen : MonoBehaviour
    {
        [Tooltip("画面を開いたときに選んでおくボタン（ゲームパッド操作の起点）。空なら一番上のボタン。")]
        [SerializeField] private Selectable firstSelected;

        public bool IsShown => gameObject.activeSelf;

        public void SetShown(bool shown)
        {
            if (gameObject.activeSelf != shown) gameObject.SetActive(shown);
        }

        public void SelectFirst()
        {
            if (EventSystem.current == null) return;
            Selectable target = firstSelected;
            if (target == null) target = GetComponentInChildren<Button>();
            if (target != null) EventSystem.current.SetSelectedGameObject(target.gameObject);
        }

        // Labels may be deleted in the prefab; a missing label is simply not updated.
        protected static void SetText(TMP_Text label, string text)
        {
            if (label != null) label.text = text;
        }

        /// <summary>string.Format that shows the wording as written when a hand-edited format is malformed.</summary>
        protected static string Format(string format, params object[] values)
        {
            if (string.IsNullOrEmpty(format)) return string.Empty;
            try { return string.Format(format, values); }
            catch (FormatException) { return format; }
        }
    }
}
