using System.IO;
using UnityEngine;

public static class SavePathProvider
{
    public const string SaveFolderName = "Saves";

    public static string SaveDirectory
    {
        get
        {
            var path = Path.Combine(Application.persistentDataPath, SaveFolderName);
            Directory.CreateDirectory(path);
            return path;
        }
    }

    public static string GetSaveFilePath(string fileName, string defaultFileName = "anomalies.json")
    {
        var resolvedName = string.IsNullOrWhiteSpace(fileName) ? defaultFileName : fileName;
        resolvedName = Path.GetFileName(resolvedName);
        if (string.IsNullOrWhiteSpace(resolvedName))
        {
            resolvedName = defaultFileName;
        }

        foreach (char invalidChar in Path.GetInvalidFileNameChars())
        {
            resolvedName = resolvedName.Replace(invalidChar, '_');
        }

        return Path.Combine(SaveDirectory, resolvedName);
    }

    public static bool EnsureSeedFileExists(string destinationPath, string resourceName = "default_scenes")
    {
        if (File.Exists(destinationPath)) return true;

        var seed = Resources.Load<TextAsset>(resourceName);
        if (seed == null)
        {
            Debug.LogError($"Initial scene data resource was not found: {resourceName}");
            return false;
        }

        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(destinationPath, seed.text);
        Debug.Log($"Created initial scene data: {destinationPath}");
        return true;
    }
}
