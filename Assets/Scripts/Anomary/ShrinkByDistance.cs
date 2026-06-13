using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class ShrinkByDistance : MonoBehaviour
{
    public Transform player;

    [Header("距離設定")]
    public float vanishDistance = 2.5f;
    public float reappearDistance = 5.0f;

    [Header("スケール設定")]
    public float minScale = 0.1f;
    public float maxScale = 1.0f;
    public Vector2 shrinkSpeedRange = new Vector2(0.01f, 0.03f);
    public float growSpeed = 0.02f;

    private Vector3 originalScale;
    private bool isHidden = false;
    private Renderer[] renderers;
    private Collider[] colliders;

    void Start()
    {
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                player = playerObj.transform;
            }
            else
            {
                Debug.LogError("Playerが見つかりません。タグを確認してください。");
            }
        }

        originalScale = transform.localScale;
        renderers = GetComponentsInChildren<Renderer>();
        colliders = GetComponentsInChildren<Collider>();

    }

    void Update()
    {
        if (player == null) return;

        float dist = Vector3.Distance(player.position, transform.position);

        if (!isHidden && dist <= vanishDistance)
        {
            Shrink();
        }
        else if (isHidden && dist >= reappearDistance)
        {
            Reappear();
        }
    }

    void Shrink()
    {
        float shrinkAmount = Random.Range(shrinkSpeedRange.x, shrinkSpeedRange.y);
        transform.localScale -= Vector3.one * shrinkAmount;

        if (transform.localScale.x <= minScale)
        {
            transform.localScale = Vector3.one * minScale;
            isHidden = true;
            SetVisible(false);   // 可視状態をfalseに
            SetCollidable(false);
        }
    }

    void Reappear()
    {
        SetVisible(true); // 再び可視化
        SetCollidable(true);
        transform.localScale += Vector3.one * growSpeed;

        if (transform.localScale.x >= maxScale)
        {
            transform.localScale = originalScale;
            isHidden = false;
        }
    }

    void SetVisible(bool visible)
    {
        foreach (var r in renderers)
        {
            r.enabled = visible;
        }
    }

    void SetCollidable(bool enabled)
    {
        foreach (var c in colliders)
        {
            c.enabled = enabled;
        }
    }
}
