using UnityEngine;

public sealed class SmoothDistanceShrink : MonoBehaviour
{
    private Transform player;
    private Vector3 originalScale;
    private bool shrinking;
    private bool hidden;
    private const float VanishDistance = 2.5f;
    private const float ReappearDistance = 5f;
    private const float ScaleSpeed = 1.2f;

    public void Configure(Transform playerTransform)
    {
        player = playerTransform;
    }

    private void Awake()
    {
        originalScale = transform.localScale;
    }

    private void Update()
    {
        ResolvePlayer();
        if (player == null) return;

        float distance = Vector3.Distance(player.position, transform.position);
        if (!shrinking && !hidden && distance <= VanishDistance)
        {
            shrinking = true;
        }
        else if (hidden && distance >= ReappearDistance)
        {
            hidden = false;
            shrinking = false;
            SetEnabled(true);
        }

        if (shrinking)
        {
            Vector3 target = originalScale * 0.08f;
            transform.localScale = Vector3.MoveTowards(transform.localScale, target, ScaleSpeed * Time.deltaTime);
            if ((transform.localScale - target).sqrMagnitude <= 0.0001f)
            {
                transform.localScale = target;
                shrinking = false;
                hidden = true;
                SetEnabled(false);
            }
        }
        else if (!hidden && transform.localScale != originalScale)
        {
            transform.localScale = Vector3.MoveTowards(transform.localScale, originalScale, ScaleSpeed * Time.deltaTime);
        }
    }

    private void ResolvePlayer()
    {
        if (player != null) return;
        var playerObject = GameObject.FindGameObjectWithTag("Player");
        player = playerObject != null ? playerObject.transform : null;
    }

    private void SetEnabled(bool value)
    {
        foreach (var rendererComponent in GetComponentsInChildren<Renderer>(true))
        {
            rendererComponent.enabled = value;
        }

        foreach (var colliderComponent in GetComponentsInChildren<Collider>(true))
        {
            colliderComponent.enabled = value;
        }
    }
}
