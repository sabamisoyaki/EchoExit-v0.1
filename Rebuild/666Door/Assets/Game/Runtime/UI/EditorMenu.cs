using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Door666.Runtime
{
    /// <summary>The editor's Tab menu (EditorMenu.prefab): stage number, save, what to place.</summary>
    public sealed class EditorMenu : UIScreen
    {
        [SerializeField] private TMP_InputField stageId;
        [SerializeField] private TMP_Text unsaved;
        [SerializeField] private TMP_Text budget;
        [Tooltip("操作の結果などのメッセージ。")]
        [SerializeField] private TMP_Text status;
        [Tooltip("「通常」ボタンの文字。選ばれているときに印を付ける。")]
        [SerializeField] private TMP_Text normalCaption;
        [Tooltip("「異変」ボタンの文字。選ばれているときに印を付ける。")]
        [SerializeField] private TMP_Text anomalyCaption;
        [Tooltip("置くもののボタンを並べる場所。並び方は Grid Layout Group で変える。")]
        [SerializeField] private RectTransform catalogList;
        [Tooltip("置くもののボタンの見本。実行時に隠され、配置物ごとに複製される。")]
        [SerializeField] private Button catalogItemTemplate;
        [Tooltip("オンなら、ボタンの幅を並べる場所の幅に合わせる（列の数は Grid Layout Group の Constraint Count、間隔は Spacing）。オフなら Cell Size のまま。")]
        [SerializeField] private bool fitColumnsToWidth = true;

        [Header("文言")]
        [SerializeField] private string unsavedText = "未保存の変更があります";
        [Tooltip("選ばれているボタンの先頭に付ける印。")]
        [SerializeField] private string selectedMark = "● ";
        [Tooltip("{0} 異変の数、{1} 異変の上限、{2} 重なりの大きい異変の数、{3} その枠。")]
        [SerializeField] private string budgetFormat = "異変 {0} / {1}     重なりの大きい異変 {2} / {3}";
        [SerializeField] private string invalidStageId = "1以上の番号を入力してください。";

        private readonly List<(string Id, string Name, TMP_Text Caption)> items = new List<(string, string, TMP_Text)>();
        private string normalText;
        private string anomalyText;

        public string StageIdText => stageId != null ? stageId.text : string.Empty;
        public string InvalidStageIdMessage => invalidStageId;

        /// <summary>Creates one button per catalog entry from the template. Called once per scene.</summary>
        public void Initialize(ObjectCatalog catalog, Action<string> choose)
        {
            if (normalCaption != null) normalText = normalCaption.text;
            if (anomalyCaption != null) anomalyText = anomalyCaption.text;
            if (catalogItemTemplate == null || catalogList == null) return;
            catalogItemTemplate.gameObject.SetActive(false);
            foreach (var id in catalog.PrefabIds)
            {
                string key = id;
                var button = Instantiate(catalogItemTemplate, catalogList);
                button.name = id;
                button.gameObject.SetActive(true);
                button.onClick.AddListener(() => choose(key));
                items.Add((key, catalog.DisplayName(key), button.GetComponentInChildren<TMP_Text>(true)));
            }
        }

        public void Present(EditorStatus state)
        {
            // Keep what is being typed; otherwise show the stage being edited.
            if (stageId != null && !IsSelected(stageId.gameObject)) stageId.SetTextWithoutNotify(state.StageId.ToString());
            SetText(unsaved, state.Unsaved ? unsavedText : "");
            SetText(budget, Format(budgetFormat, state.Anomalies, state.MaximumAnomalies, state.HeavyAnomalies, state.AllowedHeavyAnomalies));
            SetText(normalCaption, Mark(!state.AnomalyCategory) + normalText);
            SetText(anomalyCaption, Mark(state.AnomalyCategory) + anomalyText);
            foreach (var item in items) SetText(item.Caption, Mark(item.Id == state.SelectedPrefab) + item.Name);
        }

        public void SetMessage(string message) => SetText(status, message);

        // Grid cells are sized in pixels; keep the columns filling the list when the screen is not 16:9.
        private void LateUpdate()
        {
            if (!fitColumnsToWidth || catalogList == null) return;
            var grid = catalogList.GetComponent<GridLayoutGroup>();
            if (grid == null || grid.constraint != GridLayoutGroup.Constraint.FixedColumnCount || grid.constraintCount < 1) return;
            int columns = grid.constraintCount;
            float width = (catalogList.rect.width - grid.padding.horizontal - grid.spacing.x * (columns - 1)) / columns;
            if (width > 0 && !Mathf.Approximately(width, grid.cellSize.x)) grid.cellSize = new Vector2(width, grid.cellSize.y);
        }

        private string Mark(bool selected) => selected ? selectedMark : "";

        private static bool IsSelected(GameObject target) =>
            EventSystem.current != null && EventSystem.current.currentSelectedGameObject == target;
    }
}
