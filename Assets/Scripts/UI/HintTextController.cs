using UnityEngine;
using TMPro;
using System.Collections;

public class HintTextController : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI hintText;
    [SerializeField] private float delayBeforeShow = 1f; // 何秒後に表示
    [SerializeField] private float showDuration = 2f;   // 何秒表示
    [SerializeField] private float fadeTime = 1f;       // フェード時間

    void Start()
    {
        if (hintText != null)
        {
            Color c = hintText.color;
            c.a = 0f; // 最初は透明
            hintText.color = c;

            StartCoroutine(ShowAndFadeOut());
        }
    }

    private IEnumerator ShowAndFadeOut()
    {
        // 表示前に待機
        yield return new WaitForSeconds(delayBeforeShow);

        // パッと表示
        Color c = hintText.color;
        c.a = 1f;
        hintText.color = c;

        // 指定時間表示
        yield return new WaitForSeconds(showDuration);

        // フェードアウト
        float elapsed = 0f;
        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsed / fadeTime);
            c.a = alpha;
            hintText.color = c;
            yield return null;
        }

        // 最後にUIごと削除
        Destroy(gameObject);
    }
}
