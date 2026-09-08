using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class EndButtonHandler : MonoBehaviour
{
    public void OnStartButtonPressed()
    {
        SceneManager.LoadScene("MainScene"); // ゲーム本編のシーン名に変更
    }
    public void OnQuitButtonPressed()
    {
        Debug.Log("Quit"); // Unity のコンソールに出力
        Application.Quit(); // ビルド後にゲーム終了
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false; // エディタ用
#endif
    }

    void Start()
    {
        // マウスカーソルを表示
        Cursor.visible = true;

        // マウスカーソルをロック解除（自由に動ける状態）
        Cursor.lockState = CursorLockMode.None;
    }


}
