using TMPro;
using UnityEngine;

namespace Door666.Runtime
{
    /// <summary>The state of the stage being edited, handed to the editor HUD and menu.</summary>
    public struct EditorStatus
    {
        public int StageId;
        public bool Unsaved;
        public bool AnomalyCategory;
        /// <summary>The prefabId chosen for placing, or null.</summary>
        public string SelectedPrefab;
        public string SelectedName;
        public int Anomalies;
        public int MaximumAnomalies;
        public int HeavyAnomalies;
        public int AllowedHeavyAnomalies;
    }

    /// <summary>The editor's overlay while walking (EditorHud.prefab), drawn on top of HUD.prefab.</summary>
    public sealed class EditorHud : UIScreen
    {
        [SerializeField] private TMP_Text stage;
        [SerializeField] private TMP_Text selection;
        [SerializeField] private TMP_Text budget;
        [Tooltip("操作の結果などのメッセージ。")]
        [SerializeField] private TMP_Text status;

        [Header("文言")]
        [Tooltip("{0} ステージ番号、{1} 未保存の印。")]
        [SerializeField] private string stageFormat = "部屋を編集  ステージ {0}{1}";
        [SerializeField] private string unsavedMark = "  ＊未保存";
        [SerializeField] private string noSelection = "置くもの：未選択（Tab で選ぶ）";
        [Tooltip("{0} 置くものの名前、{1} 「通常」か「異変」。")]
        [SerializeField] private string selectionFormat = "置くもの：{0}（{1}）";
        [SerializeField] private string normalWord = "通常";
        [SerializeField] private string anomalyWord = "異変";
        [Tooltip("{0} 異変の数、{1} 異変の上限、{2} 重なりの大きい異変の数、{3} その枠。")]
        [SerializeField] private string budgetFormat = "異変 {0} / {1}     重なりの大きい異変 {2} / {3}";

        public void Present(EditorStatus state)
        {
            SetText(stage, Format(stageFormat, state.StageId, state.Unsaved ? unsavedMark : ""));
            SetText(selection, state.SelectedPrefab == null ? noSelection
                : Format(selectionFormat, state.SelectedName, state.AnomalyCategory ? anomalyWord : normalWord));
            SetText(budget, Format(budgetFormat, state.Anomalies, state.MaximumAnomalies, state.HeavyAnomalies, state.AllowedHeavyAnomalies));
        }

        public void SetMessage(string message) => SetText(status, message);
    }
}
