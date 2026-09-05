using UnityEngine;

public sealed class LookAwayColorPhenomenon : MonoBehaviour
{
    private AnomalyRitualController ritual;
    private Renderer[] renderers;
    private bool changed;

    public void Configure(AnomalyRitualController controller)
    {
        if (ritual != null) ritual.Recognized -= OnRecognized;
        ritual = controller;
        renderers = GetComponentsInChildren<Renderer>(true);
        if (ritual != null) ritual.Recognized += OnRecognized;
    }

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
    }

    private void OnDestroy()
    {
        if (ritual != null) ritual.Recognized -= OnRecognized;
    }

    private void OnRecognized(AnomalyRitualController _)
    {
        if (changed) return;
        changed = true;
        foreach (var rendererComponent in renderers)
        {
            foreach (var material in rendererComponent.materials)
            {
                material.color = Color.red;
            }
        }
    }
}
