using System.Collections;
using UnityEngine;

public sealed class ReturningDollPhenomenon : MonoBehaviour
{
    private AnomalyRitualController ritual;
    private Renderer[] renderers;
    private Collider[] colliders;

    public void Configure(AnomalyRitualController controller)
    {
        if (ritual != null) ritual.Recognized -= OnRecognized;
        ritual = controller;
        renderers = GetComponentsInChildren<Renderer>(true);
        colliders = GetComponentsInChildren<Collider>(true);
        if (ritual != null) ritual.Recognized += OnRecognized;
    }

    private void OnDestroy()
    {
        if (ritual != null) ritual.Recognized -= OnRecognized;
    }

    private void OnRecognized(AnomalyRitualController _)
    {
        StartCoroutine(ReturnWithChangedPose());
    }

    private IEnumerator ReturnWithChangedPose()
    {
        ProceduralAnomalyAudio.PlayAtPosition(AnomalySoundKind.Scrape, transform.position, 0.55f);
        SetVisible(false);
        yield return new WaitForSeconds(0.35f);

        transform.rotation *= Quaternion.Euler(0f, 180f, 12f);
        transform.position += Vector3.up * 0.12f;
        SetVisible(true);
    }

    private void SetVisible(bool visible)
    {
        foreach (var rendererComponent in renderers) rendererComponent.enabled = visible;
        foreach (var colliderComponent in colliders) colliderComponent.enabled = visible;
    }
}
