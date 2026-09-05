using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ラウンド判定結果を短時間表示する。参照未設定時も既存 Canvas 上へUIを補完する。
/// </summary>
public sealed class RoundFeedbackPresenter : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Image overlayImage;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private CanvasGroup announcementGroup;
    [SerializeField] private Image announcementImage;
    [SerializeField] private TMP_Text announcementText;

    [Header("表示")]
    [SerializeField, Min(0f)] private float displayDuration = 0.8f;
    [SerializeField, Min(0f)] private float fadeDuration = 0.15f;
    [SerializeField] private Color correctColor = new Color(0.08f, 0.75f, 0.25f, 0.42f);
    [SerializeField] private Color incorrectColor = new Color(0.85f, 0.08f, 0.08f, 0.42f);

    [Header("SE（任意）")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip correctClip;
    [SerializeField] private AudioClip incorrectClip;

    private void Awake()
    {
        EnsureUi();
        SetVisible(false);
    }

    private void OnDisable()
    {
        SetVisible(false);
        SetAnnouncementVisible(false);
    }

    public IEnumerator ShowResult(bool isCorrect, int count, int threshold)
    {
        if (!EnsureUi())
        {
            yield break;
        }

        overlayImage.color = isCorrect ? correctColor : incorrectColor;
        messageText.text = isCorrect
            ? $"正解\n{count}/{threshold}"
            : $"不正解\n{count}/{threshold}";

        AudioClip clip = isCorrect ? correctClip : incorrectClip;
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }

        canvasGroup.gameObject.SetActive(true);
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
        canvasGroup.alpha = 0f;

        float effectiveFade = Mathf.Min(fadeDuration, displayDuration * 0.5f);
        yield return Fade(0f, 1f, effectiveFade);

        float holdDuration = Mathf.Max(0f, displayDuration - effectiveFade * 2f);
        yield return WaitRealtime(holdDuration);
        yield return Fade(1f, 0f, effectiveFade);

        SetVisible(false);
    }

    public IEnumerator ShowAnnouncement(string message, bool isDanger, float duration = 1f)
    {
        if (!EnsureAnnouncementUi())
        {
            yield break;
        }

        announcementImage.color = isDanger
            ? new Color(0.55f, 0.035f, 0.035f, 0.88f)
            : new Color(0.06f, 0.11f, 0.18f, 0.88f);
        announcementText.text = message ?? string.Empty;
        announcementGroup.gameObject.SetActive(true);
        announcementGroup.blocksRaycasts = false;
        announcementGroup.interactable = false;
        announcementGroup.alpha = 0f;

        float effectiveFade = Mathf.Min(fadeDuration, duration * 0.5f);
        yield return Fade(announcementGroup, 0f, 1f, effectiveFade);
        yield return WaitRealtime(Mathf.Max(0f, duration - effectiveFade * 2f));
        yield return Fade(announcementGroup, 1f, 0f, effectiveFade);
        SetAnnouncementVisible(false);
    }

    public void SetRoundTimer(float secondsRemaining)
    {
        if (!EnsureTimerUi()) return;

        int totalSeconds = Mathf.Max(0, Mathf.CeilToInt(secondsRemaining));
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        timerText.text = $"{minutes:00}:{seconds:00}";
        timerText.color = totalSeconds <= 30 ? new Color(1f, 0.3f, 0.25f) : Color.white;
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        return Fade(canvasGroup, from, to, duration);
    }

    private static IEnumerator Fade(CanvasGroup target, float from, float to, float duration)
    {
        if (duration <= 0f)
        {
            target.alpha = to;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            target.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        target.alpha = to;
    }

    private static IEnumerator WaitRealtime(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private void SetVisible(bool visible)
    {
        if (canvasGroup == null) return;

        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
        canvasGroup.gameObject.SetActive(visible);
    }

    private void SetAnnouncementVisible(bool visible)
    {
        if (announcementGroup == null) return;

        announcementGroup.alpha = visible ? 1f : 0f;
        announcementGroup.blocksRaycasts = false;
        announcementGroup.interactable = false;
        announcementGroup.gameObject.SetActive(visible);
    }

    private bool EnsureAnnouncementUi()
    {
        if (announcementGroup != null && announcementImage != null && announcementText != null)
        {
            return true;
        }

        var canvas = FindFirstObjectByType<Canvas>();
        var templateText = canvas != null ? canvas.GetComponentInChildren<TMP_Text>(true) : null;
        if (canvas == null || templateText == null) return false;

        var root = new GameObject("AnomalyAnnouncement", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        root.layer = LayerMask.NameToLayer("UI");
        var rootRect = (RectTransform)root.transform;
        rootRect.SetParent(canvas.transform, false);
        rootRect.anchorMin = new Vector2(0.5f, 1f);
        rootRect.anchorMax = new Vector2(0.5f, 1f);
        rootRect.pivot = new Vector2(0.5f, 1f);
        rootRect.anchoredPosition = new Vector2(0f, -28f);
        rootRect.sizeDelta = new Vector2(760f, 108f);
        rootRect.SetAsLastSibling();

        announcementGroup = root.GetComponent<CanvasGroup>();
        announcementImage = root.GetComponent<Image>();
        announcementImage.raycastTarget = false;

        var message = Instantiate(templateText.gameObject, rootRect, false);
        message.name = "AnnouncementText";
        message.layer = root.layer;
        announcementText = message.GetComponent<TMP_Text>();
        announcementText.text = string.Empty;
        announcementText.alignment = TextAlignmentOptions.Center;
        announcementText.fontSize = 31f;
        announcementText.fontStyle = FontStyles.Bold;
        announcementText.color = Color.white;
        announcementText.raycastTarget = false;
        var messageRect = announcementText.rectTransform;
        Stretch(messageRect);
        messageRect.offsetMin = new Vector2(24f, 10f);
        messageRect.offsetMax = new Vector2(-24f, -10f);

        SetAnnouncementVisible(false);
        return true;
    }

    private bool EnsureUi()
    {
        if (canvasGroup != null && overlayImage != null && messageText != null)
        {
            EnsureAudioSource();
            return true;
        }

        var canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("RoundFeedbackPresenter: Canvas が見つからないため表示できません。");
            return false;
        }

        var root = new GameObject("RoundFeedback", typeof(RectTransform), typeof(CanvasGroup));
        root.layer = LayerMask.NameToLayer("UI");
        var rootRect = (RectTransform)root.transform;
        rootRect.SetParent(canvas.transform, false);
        Stretch(rootRect);
        rootRect.SetAsLastSibling();
        canvasGroup = root.GetComponent<CanvasGroup>();

        var overlay = new GameObject("Overlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        overlay.layer = root.layer;
        var overlayRect = (RectTransform)overlay.transform;
        overlayRect.SetParent(rootRect, false);
        Stretch(overlayRect);
        overlayImage = overlay.GetComponent<Image>();
        overlayImage.raycastTarget = false;

        var templateText = canvas.GetComponentInChildren<TMP_Text>(true);
        if (templateText == null)
        {
            Debug.LogWarning("RoundFeedbackPresenter: 複製元のTMPテキストが見つかりません。");
            Destroy(root);
            canvasGroup = null;
            overlayImage = null;
            return false;
        }

        var message = Instantiate(templateText.gameObject, rootRect, false);
        message.name = "Message";
        message.layer = root.layer;
        var messageRect = message.GetComponent<RectTransform>();
        messageRect.anchorMin = new Vector2(0.5f, 0.5f);
        messageRect.anchorMax = new Vector2(0.5f, 0.5f);
        messageRect.pivot = new Vector2(0.5f, 0.5f);
        messageRect.sizeDelta = new Vector2(700f, 220f);
        messageRect.anchoredPosition = Vector2.zero;

        messageText = message.GetComponent<TMP_Text>();
        messageText.alignment = TextAlignmentOptions.Center;
        messageText.fontSize = 56f;
        messageText.fontStyle = FontStyles.Bold;
        messageText.color = Color.white;
        messageText.raycastTarget = false;

        EnsureAudioSource();
        return true;
    }

    private bool EnsureTimerUi()
    {
        if (timerText != null) return true;

        var canvas = FindFirstObjectByType<Canvas>();
        var templateText = canvas != null ? canvas.GetComponentInChildren<TMP_Text>(true) : null;
        if (canvas == null || templateText == null)
        {
            return false;
        }

        var timerObject = Instantiate(templateText.gameObject, canvas.transform, false);
        timerObject.name = "RoundTimer";
        timerText = timerObject.GetComponent<TMP_Text>();
        timerText.alignment = TextAlignmentOptions.TopRight;
        timerText.fontSize = 34f;
        timerText.fontStyle = FontStyles.Bold;
        timerText.raycastTarget = false;

        var rect = timerText.rectTransform;
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-32f, -24f);
        rect.sizeDelta = new Vector2(260f, 80f);
        return true;
    }

    private void EnsureAudioSource()
    {
        if (audioSource != null) return;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
