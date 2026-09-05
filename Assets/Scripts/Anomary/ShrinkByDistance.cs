using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class ShrinkByDistance : MonoBehaviour
{
    public Transform player;

    [Header("Distance Settings")]
    public float vanishDistance = 2.5f;
    public float reappearDistance = 5.0f;

    [Header("Scale Settings")]
    public float minScale = 0.1f;
    public float maxScale = 1.0f;
    public Vector2 shrinkSpeedRange = new Vector2(0.01f, 0.03f);
    public float growSpeed = 0.02f;

    private Vector3 originalScale;
    private float scaleRatio = 1f;
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
                Debug.LogError("Player object was not found. Check the Player tag.");
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
        scaleRatio = Mathf.Max(minScale, scaleRatio - shrinkAmount);
        transform.localScale = originalScale * scaleRatio;

        if (scaleRatio <= minScale)
        {
            isHidden = true;
            SetVisible(false);
            SetCollidable(false);
        }
    }

    void Reappear()
    {
        SetVisible(true);
        SetCollidable(true);
        scaleRatio = Mathf.Min(maxScale, scaleRatio + growSpeed);
        transform.localScale = originalScale * scaleRatio;

        if (scaleRatio >= maxScale)
        {
            scaleRatio = 1f;
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
