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
        return Path.Combine(SaveDirectory, resolvedName);
    }
}
