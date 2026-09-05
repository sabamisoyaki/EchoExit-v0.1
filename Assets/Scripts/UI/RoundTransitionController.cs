using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Provides unscaled-time failure flashes and scene-reload fades.</summary>
public class RoundTransitionController : MonoBehaviour
{
    [SerializeField, Min(0.05f)] private float fadeDuration = 0.35f;
    [SerializeField, Min(0f)] private float failureHoldDuration = 0.2f;

    private CanvasGroup fadeOverlay;
    private Image fadeImage;

    private void Awake()
    {
        EnsureOverlay();
        StartCoroutine(FadeFromBlack());
    }

    public IEnumerator PlayIncorrectEffect()
    {
        EnsureOverlay();
        fadeImage.color = new Color(0.45f, 0f, 0f, 1f);
        yield return Fade(0f, 0.72f);
        yield return new WaitForSecondsRealtime(failureHoldDuration);
        yield return Fade(0.72f, 0f);
        fadeImage.color = Color.black;
    }

    public IEnumerator FadeToBlack()
    {
        EnsureOverlay();
        fadeImage.color = Color.black;
        yield return Fade(fadeOverlay.alpha, 1f);
    }

    private IEnumerator FadeFromBlack()
    {
        yield return Fade(1f, 0f);
        fadeOverlay.blocksRaycasts = false;
    }

    private IEnumerator Fade(float from, float to)
    {
        fadeOverlay.gameObject.SetActive(true);
        fadeOverlay.blocksRaycasts = true;
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            fadeOverlay.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / fadeDuration));
            yield return null;
        }
        fadeOverlay.alpha = to;
        if (to <= 0f) fadeOverlay.blocksRaycasts = false;
    }

    private void EnsureOverlay()
    {
        if (fadeOverlay != null) return;
        var canvasObject = new GameObject("Round Transition Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        var panel = new GameObject("Transition Overlay", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        panel.transform.SetParent(canvasObject.transform, false);
        RectTransform rect = (RectTransform)panel.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        fadeImage = panel.GetComponent<Image>();
        fadeImage.color = Color.black;
        fadeOverlay = panel.GetComponent<CanvasGroup>();
        fadeOverlay.alpha = 1f;
    }
}
