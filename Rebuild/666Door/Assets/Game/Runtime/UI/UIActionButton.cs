using UnityEngine;
using UnityEngine.UI;

namespace Door666.Runtime
{
    /// <summary>What a button in Interface.prefab does. The values are stored as numbers: add new ones at the end.</summary>
    public enum UIAction
    {
        [InspectorName("なし")] None,
        [InspectorName("扉を開ける（ゲームを始める）")] StartRun,
        [InspectorName("部屋を編集する")] StartEditor,
        [InspectorName("設定を開く")] OpenSettings,
        [InspectorName("設定を閉じる（元の画面へ）")] CloseSettings,
        [InspectorName("部屋へ戻る（一時停止を解除）")] Resume,
        [InspectorName("タイトルへ")] BackToTitle,
        [InspectorName("終了")] Quit,
        [InspectorName("音の字幕を切り替える")] ToggleSubtitles,
        [InspectorName("キー設定を初期化")] ResetBindings,
        [InspectorName("編集：ステージを読込")] EditorLoad,
        [InspectorName("編集：新しいステージ")] EditorNew,
        [InspectorName("編集：保存")] EditorSave,
        [InspectorName("編集：通常オブジェクトを置く")] EditorNormal,
        [InspectorName("編集：異変を置く")] EditorAnomaly,
        [InspectorName("編集：メニューを閉じる")] EditorCloseMenu
    }

    /// <summary>Put on a button anywhere in Interface.prefab and choose what it does; GameUI wires it at start.</summary>
    [RequireComponent(typeof(Button))]
    [DisallowMultipleComponent]
    public sealed class UIActionButton : MonoBehaviour
    {
        [Tooltip("押したときの動作。")]
        public UIAction action;

        public Button Button => GetComponent<Button>();
    }
}
