using UnityEngine;

public class InstructionToggle : MonoBehaviour
{
    private CanvasGroup canvasGroup;
    private bool isVisible = false;

    void Awake()
    {
        // このスクリプトを InstructionPanel に直接アタッチする想定
        canvasGroup = gameObject.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        // 最初は非表示状態
        SetVisible(false);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.H))
        {
            isVisible = !isVisible;
            SetVisible(isVisible);
        }
    }

    private void SetVisible(bool visible)
    {
        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
    }
}
