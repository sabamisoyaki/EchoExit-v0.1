using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.IO;

public class ButtonHandler : MonoBehaviour
{
    [SerializeField] private Button playButton;
    [SerializeField] private TMP_Text warningMessageText;
    [SerializeField] private SharedString sharedString;

    private bool isDataValid = false;
    private string finalPath;
    private string lastCheckedFileName = "";

    private void Awake()
    {
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
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        if (sharedString == null || playButton == null || warningMessageText == null)
        {
            Debug.LogError("ButtonHandler: sharedString/playButton/warningMessageText is not assigned.");
            enabled = false;
            return;
        }

        UpdateFileCheck(); // 初回チェック
    }

    void Update()
    {
        if (sharedString.value != lastCheckedFileName)
        {
            UpdateFileCheck(); // 値が変わったら再チェック
        }
    }

    private void UpdateFileCheck()
    {
        lastCheckedFileName = sharedString.value;
        isDataValid = CheckJsonFile();

        if (isDataValid)
        {
            playButton.interactable = true;
            warningMessageText.gameObject.SetActive(false);
        }
        else
        {
            playButton.interactable = false;
            warningMessageText.gameObject.SetActive(true);
            warningMessageText.text = $"Cannot play. Please add {sharedString.value} first.";
        }
    }

    private bool CheckJsonFile()
    {
        if (string.IsNullOrEmpty(sharedString.value))
        {
            Debug.LogWarning("SharedString.value が空です。");
            return false;
        }

        finalPath = SavePathProvider.GetSaveFilePath(sharedString.value, "anomalies.json");

        if (!File.Exists(finalPath))
        {
            File.WriteAllText(finalPath, "");
            return false;
        }

        string content = File.ReadAllText(finalPath).Trim();
        return !string.IsNullOrEmpty(content);
    }

    public void OnStartButtonPressed()
    {
        if (isDataValid)
        {
            SceneManager.LoadScene("MainScene");
        }
    }

    public void OnEditButtonPressed()
    {
        SceneManager.LoadScene("EditMode");
    }
}
