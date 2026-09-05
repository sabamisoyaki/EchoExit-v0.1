using System.IO;
using UnityEngine;

public static class SavePathProvider
{
    public const string SaveFolderName = "Saves";
    public const string DefaultFileName = "anomalies.json";

    // Resources/DefaultAnomalies.json（新形式）。初回起動時にセーブフォルダへ展開する
    private const string DefaultDataResource = "DefaultAnomalies";

    public static string SaveDirectory
    {
        get
        {
            var path = Path.Combine(Application.persistentDataPath, SaveFolderName);
            Directory.CreateDirectory(path);
            return path;
        }
    }

    public static string GetSaveFilePath(string fileName, string defaultFileName = DefaultFileName)
    {
        var resolvedName = string.IsNullOrWhiteSpace(fileName) ? defaultFileName : fileName;
        return Path.Combine(SaveDirectory, resolvedName);
    }

    /// <summary>
    /// 対象ファイルが存在しない、または中身が空の場合、
    /// Resources のデフォルトデータを書き込んでからパスを返す。
    /// </summary>
    public static string EnsureSaveFileWithDefaultData(string fileName)
    {
        var path = GetSaveFilePath(fileName);

        try
        {
            bool needsDefault = !File.Exists(path) || string.IsNullOrWhiteSpace(File.ReadAllText(path));
            if (needsDefault)
            {
                var defaultData = Resources.Load<TextAsset>(DefaultDataResource);
                if (defaultData != null && !string.IsNullOrWhiteSpace(defaultData.text))
                {
                    File.WriteAllText(path, defaultData.text);
                    Debug.Log($"SavePathProvider: デフォルトデータを展開しました → {path}");
                }
                else
                {
                    Debug.LogWarning($"SavePathProvider: Resources/{DefaultDataResource} が見つからないため展開できません");
                }
            }
        }
        catch (IOException e)
        {
            Debug.LogWarning($"SavePathProvider: デフォルトデータ展開に失敗: {e.Message}");
        }

        return path;
    }
}
