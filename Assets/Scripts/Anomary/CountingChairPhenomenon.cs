using UnityEngine;

public sealed class CountingChairPhenomenon : MonoBehaviour
{
    private AnomalyRitualController ritual;
    private bool revealed;

    public void Configure(AnomalyRitualController controller)
    {
        if (ritual != null) ritual.Primed -= OnPrimed;
        ritual = controller;
        if (ritual != null) ritual.Primed += OnPrimed;
    }

    private void OnDestroy()
    {
        if (ritual != null) ritual.Primed -= OnPrimed;
    }

    private void OnPrimed(AnomalyRitualController _)
    {
        if (revealed) return;
        revealed = true;
        ProceduralAnomalyAudio.PlayAtPosition(AnomalySoundKind.Scrape, transform.position, 0.45f);

        for (int i = 0; i < 3; i++)
        {
            var shadow = GameObject.CreatePrimitive(PrimitiveType.Quad);
            shadow.name = $"CountedSeatShadow_{i + 1}";
            shadow.transform.SetParent(transform, false);
            shadow.transform.localPosition = new Vector3((i - 1) * 0.42f, -0.48f, -0.55f - i * 0.08f);
            shadow.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            shadow.transform.localScale = new Vector3(0.32f, 0.5f, 0.32f);

            var colliderComponent = shadow.GetComponent<Collider>();
            if (colliderComponent != null) Destroy(colliderComponent);
            var rendererComponent = shadow.GetComponent<Renderer>();
            rendererComponent.material = CreateShadowMaterial();
        }
    }

    private static Material CreateShadowMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
        var material = new Material(shader) { color = new Color(0.02f, 0.02f, 0.025f, 0.82f) };
        return material;
    }
}
