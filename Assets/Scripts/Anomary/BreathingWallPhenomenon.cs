using UnityEngine;

public sealed class BreathingWallPhenomenon : MonoBehaviour
{
    private AnomalyRitualController ritual;
    private Transform player;
    private AudioSource source;
    private Vector3 baseScale;
    private float nextBreathTime;

    public void Configure(AnomalyRitualController controller, Transform playerTransform)
    {
        ritual = controller;
        player = playerTransform;
        source = ProceduralAnomalyAudio.EnsureSpatialSource(gameObject);
        baseScale = transform.localScale;
        nextBreathTime = Time.time + 1.5f;
    }

    private void Awake()
    {
        baseScale = transform.localScale;
    }

    private void Update()
    {
        if (player == null)
        {
            var playerObject = GameObject.FindGameObjectWithTag("Player");
            player = playerObject != null ? playerObject.transform : null;
        }

        float intensity = ritual != null && ritual.IsPrimed ? 0.035f : 0.014f;
        float breath = (Mathf.Sin(Time.time * 1.45f) + 1f) * 0.5f;
        transform.localScale = new Vector3(baseScale.x, baseScale.y, baseScale.z * (1f + breath * intensity));

        bool playerNear = player != null && Vector3.Distance(player.position, transform.position) <= 7f;
        if (playerNear && Time.time >= nextBreathTime)
        {
            ProceduralAnomalyAudio.Play(source, AnomalySoundKind.Breath, 0.48f);
            nextBreathTime = Time.time + 4.2f;
        }
    }
}
