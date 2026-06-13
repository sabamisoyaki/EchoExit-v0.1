using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.IO;

public class StartButtonHandler : MonoBehaviour
{
    [SerializeField] private Button playButton;
    [SerializeField] private TMP_Text warningMessageText;

    [SerializeField] private string fileName = "anomalies.json";

    private bool isDataValid = false;
    private string finalPath;

    private void Awake()
    {
        // Fallback: allow scene to run even if references were not wired in Inspector.
        if (playButton == null)
        {
            playButton = GetComponentInChildren<Button>(true);
        }

        if (warningMessageText == null)
        {
            warningMessageText = GetComponentInChildren<TMP_Text>(true);
        }
    }

    void Start()
    {
        // マウスカーソルを表示 & ロック解除
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        // JSONの有効性をチェック
        isDataValid = CheckJsonFile();

        if (playButton == null || warningMessageText == null)
        {
            Debug.LogError("StartButtonHandler: playButton or warningMessageText is not assigned.");
            return;
        }

        if (isDataValid)
        {
            playButton.interactable = true;
            warningMessageText.gameObject.SetActive(false);
        }
        else
        {
            playButton.interactable = false;
            warningMessageText.gameObject.SetActive(true);
            warningMessageText.text = "Cannot play. Please add anomalies.json first.";
        }
    }

    private bool CheckJsonFile()
    {
        finalPath = SavePathProvider.GetSaveFilePath(fileName, "anomalies.json");

        // ファイルが無ければ空ファイルを生成
        if (!File.Exists(finalPath))
        {
            File.WriteAllText(finalPath, "");
            return false; // 中身が無いのでプレイ不可
        }

        // 内容チェック（空文字ならNG）
        string content = File.ReadAllText(finalPath).Trim();
        return !string.IsNullOrEmpty(content);
    }


    public void OnStartButtonPressed()
    {
        if (isDataValid)
        {
            SceneManager.LoadScene("oldMainScene");
        }
    }

    public void OnEditButtonPressed()
    {
        SceneManager.LoadScene("oldEditMode");
    }
}
