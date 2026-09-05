using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Presents round instructions and judgement feedback without requiring scene-specific UI wiring.
/// A scene can provide styled references in the Inspector; otherwise a compact overlay is built at runtime.
/// </summary>
public class RoundFeedbackController : MonoBehaviour
{
    private const string TutorialSeenKey = "EchoExit.TutorialSeen";

    [Header("Optional scene references")]
    [SerializeField] private CanvasGroup overlay;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private TMP_Text progressText;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip correctClip;
    [SerializeField] private AudioClip incorrectClip;

    [Header("Timing")]
    [SerializeField, Min(0.25f)] private float resultDuration = 1.15f;
    [SerializeField, Min(0.05f)] private float fadeDuration = 0.2f;

    public float ResultDuration => resultDuration;

    private void Awake()
    {
        EnsureUi();
        SetOverlayVisible(false);
    }

    public void ShowFirstRunTutorial(bool isLearningRound = false)
    {
        bool tutorialSeen = PlayerPrefs.GetInt(TutorialSeenKey, 0) != 0;
        if (tutorialSeen && !isLearningRound) return;

        if (!tutorialSeen)
        {
            PlayerPrefs.SetInt(TutorialSeenKey, 1);
            PlayerPrefs.Save();
        }
        string introduction = isLearningRound && !tutorialSeen
            ? "まず、この場所を覚えてください\n異変なし：先へ進む　／　異変あり：引き返す"
            : isLearningRound
                ? "まず、この場所を覚えてください"
            : "部屋をよく観察してください\n異変なし：先へ進む　／　異変あり：引き返す";
        StartCoroutine(ShowTemporaryMessage(
            introduction,
            4.5f));
    }

    public void UpdateProgress(int current, int target)
    {
        if (progressText == null) return;
        progressText.text = current >= target
            ? "出口が開いた"
            : $"出口まで、あと {Mathf.Max(0, target - current)}";
    }

    public IEnumerator PlayResult(bool isCorrect, bool hadAnomaly, int current, int target)
    {
        EnsureUi();
        StopAllCoroutines();

        string detail = hadAnomaly ? "この部屋には異変があった" : "この部屋に異変はなかった";
        messageText.text = isCorrect
            ? $"正解\n{detail}"
            : hadAnomaly
                ? $"不正解：見落とした\n{detail}"
                : $"不正解：疑いすぎた\n{detail}";
        messageText.color = isCorrect
            ? new Color(0.55f, 1f, 0.75f)
            : new Color(1f, 0.45f, 0.45f);

        UpdateProgress(current, target);
        StartCoroutine(PulseProgress());
        if (audioSource != null)
        {
            AudioClip clip = isCorrect ? correctClip : incorrectClip;
            if (clip != null) audioSource.PlayOneShot(clip);
        }

        yield return FadeOverlay(0f, 1f);
        yield return new WaitForSecondsRealtime(resultDuration);
        yield return FadeOverlay(1f, 0f);
    }

    private IEnumerator PulseProgress()
    {
        if (progressText == null) yield break;

        Transform target = progressText.transform;
        const float pulseTime = 0.22f;
        float elapsed = 0f;
        while (elapsed < pulseTime)
        {
            elapsed += Time.unscaledDeltaTime;
            float amount = Mathf.Sin(Mathf.Clamp01(elapsed / pulseTime) * Mathf.PI);
            target.localScale = Vector3.one * Mathf.Lerp(1f, 1.16f, amount);
            yield return null;
        }

        target.localScale = Vector3.one;
    }

    private IEnumerator ShowTemporaryMessage(string message, float duration)
    {
        EnsureUi();
        messageText.text = message;
        messageText.color = Color.white;
        yield return FadeOverlay(0f, 1f);
        yield return new WaitForSecondsRealtime(duration);
        yield return FadeOverlay(1f, 0f);
    }

    private IEnumerator FadeOverlay(float from, float to)
    {
        if (overlay == null) yield break;

        overlay.gameObject.SetActive(true);
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            overlay.alpha = Mathf.Lerp(from, to, elapsed / fadeDuration);
            yield return null;
        }

        overlay.alpha = to;
        if (to <= 0f) overlay.gameObject.SetActive(false);
    }

    private void SetOverlayVisible(bool visible)
    {
        if (overlay == null) return;
        overlay.alpha = visible ? 1f : 0f;
        overlay.blocksRaycasts = false;
        overlay.interactable = false;
        overlay.gameObject.SetActive(visible);
    }

    private void EnsureUi()
    {
        if (overlay != null && messageText != null && progressText != null) return;

        // Reuse the scene's configured font so Japanese glyphs and project styling carry over.
        TMP_FontAsset sceneFont = FindFirstObjectByType<TMP_Text>()?.font;

        var canvasObject = new GameObject("Round Feedback Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        var panel = new GameObject("Feedback Overlay", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        panel.transform.SetParent(canvasObject.transform, false);
        var panelRect = (RectTransform)panel.transform;
        panelRect.anchorMin = new Vector2(0.18f, 0.36f);
        panelRect.anchorMax = new Vector2(0.82f, 0.64f);
        panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;
        panel.GetComponent<Image>().color = new Color(0.015f, 0.02f, 0.025f, 0.88f);
        overlay = panel.GetComponent<CanvasGroup>();

        messageText = CreateText(panel.transform, "Result Message", 36, new Vector2(0.05f, 0.26f), new Vector2(0.95f, 0.92f));
        progressText = CreateText(panel.transform, "Progress Message", 24, new Vector2(0.05f, 0.04f), new Vector2(0.95f, 0.28f));
        if (sceneFont != null)
        {
            messageText.font = sceneFont;
            progressText.font = sceneFont;
        }
    }

    private static TMP_Text CreateText(Transform parent, string objectName, float size, Vector2 min, Vector2 max)
    {
        var textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        var rect = (RectTransform)textObject.transform;
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;

        var text = textObject.GetComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = true;
        return text;
    }
}
