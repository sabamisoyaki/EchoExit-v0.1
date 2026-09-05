using UnityEngine;
using TMPro;

public class TMP_DropdownSaver : MonoBehaviour
{
    [SerializeField] private TMP_Dropdown dropdown;
    [SerializeField] private SharedString sharedString;

    private const string PrefKey = "SelectedDropdownIndex";

    private void Start()
    {
        if (dropdown == null || sharedString == null)
        {
            Debug.LogError("Dropdown または SharedString が未設定です。");
            return;
        }

        int savedIndex = PlayerPrefs.GetInt(PrefKey, 0);

        // 選択肢が変わって保存済み index が範囲外になった場合は先頭にフォールバック。
        // sharedString.value が前回の値のまま残らないよう、必ずここで設定する。
        if (savedIndex < 0 || savedIndex >= dropdown.options.Count)
        {
            savedIndex = 0;
        }

        if (dropdown.options.Count > 0)
        {
            dropdown.value = savedIndex;

            string suffix = dropdown.options[savedIndex].text;
            sharedString.value = $"anomaly_{suffix}.json";
            Debug.Log($"[起動時] ファイル名: {sharedString.value}");
        }

        dropdown.RefreshShownValue();
        dropdown.onValueChanged.AddListener(OnDropdownChanged);
    }

    private void OnDropdownChanged(int index)
    {
        string suffix = dropdown.options[index].text;
        string fileName = $"anomaly_{suffix}.json";

        sharedString.value = fileName;

        PlayerPrefs.SetInt(PrefKey, index);
        PlayerPrefs.Save();

        Debug.Log($"[変更] 選択肢: {suffix} → ファイル名: {fileName}");
    }

    private void OnDestroy()
    {
        if (dropdown != null)
            dropdown.onValueChanged.RemoveListener(OnDropdownChanged);
    }
}
