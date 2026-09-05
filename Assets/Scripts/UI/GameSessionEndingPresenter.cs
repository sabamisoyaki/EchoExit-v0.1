using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 既存の終了シーンを通常脱出・捕獲・時間切れの共通エンディング表示として利用する。
/// シーンへの参照追加を不要にするためロードイベントからUIを補完する。
/// </summary>
public static class GameSessionEndingPresenter
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!string.Equals(scene.name, "endTitle", System.StringComparison.OrdinalIgnoreCase)) return;

        var title = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(text => text.name == "TittleText");
        if (title == null) return;

        switch (GameSessionState.Ending)
        {
            case GameEndingKind.Escaped:
                title.text = "666号扉から脱出した";
                break;
            case GameEndingKind.Caught:
                title.text = $"異変に捕まった\n<size=55%>{GameSessionState.Detail}</size>";
                break;
            case GameEndingKind.TimeExpired:
                title.text = "時間切れ\n<size=55%>空間に取り残された</size>";
                break;
        }
    }
}
