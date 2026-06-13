using UnityEngine;
using System.Collections;

public class TeddyVanishRespawn : MonoBehaviour
{
    public Transform player;
    public float activationDistance = 2.5f;
    public AudioClip vanishSound;
    public Material blackMaterial;
    public float vanishDelay = 0.3f;
    public float respawnDelay = 5.0f;

    private AudioSource audioSource;
    private bool isVanishing = false;
    private Renderer[] renderers;
    private Material[] originalMaterials;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        renderers = GetComponentsInChildren<Renderer>();

        // プレイヤー自動取得
        if (player == null)
        {
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null) player = playerObj.transform;
            else Debug.LogError("Playerオブジェクトが見つかりません。タグ 'Player' を確認してください。");
        }

        // 元マテリアル保存
        originalMaterials = new Material[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            originalMaterials[i] = renderers[i].material;
        }
    }

    void Update()
    {
        if (isVanishing || player == null) return;

        float dist = Vector3.Distance(transform.position, player.position);
        if (dist <= activationDistance)
        {
            StartCoroutine(VanishAndRespawnSequence());
        }
    }

    IEnumerator VanishAndRespawnSequence()
    {
        isVanishing = true;

        // 効果音再生（1秒後に0.3秒フェードアウト）
        if (vanishSound)
        {
            audioSource.clip = vanishSound;
            audioSource.volume = 0.3f;
            audioSource.Play();
            StartCoroutine(FadeOutAfterDelay(1.0f, 0.3f));
        }

        // 黒マテリアル適用
        foreach (var r in renderers)
        {
            r.material = blackMaterial;
        }

        yield return new WaitForSeconds(vanishDelay);

        // 非表示化
        foreach (var r in renderers)
        {
            r.enabled = false;
        }

        yield return new WaitForSeconds(respawnDelay);

        // 元に戻して再表示
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].material = originalMaterials[i];
            renderers[i].enabled = true;
        }

        isVanishing = false; // 再トリガー可能に
    }

    IEnumerator FadeOutAfterDelay(float delay, float fadeDuration)
    {
        yield return new WaitForSeconds(delay);

        float startVolume = audioSource.volume;
        float t = 0f;

        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            audioSource.volume = Mathf.Lerp(startVolume, 0f, t / fadeDuration);
            yield return null;
        }

        audioSource.Stop();
        audioSource.volume = 1.0f; // 次回再生用にリセット
    }
}
