using UnityEngine;

public sealed class ExtraFootstepAnomaly : MonoBehaviour
{
    private AnomalyRitualController ritual;
    private Transform player;
    private Vector3 previousPlayerPosition;
    private float movingTime;
    private float nextCueTime;
    private bool hasPreviousPosition;

    public void Configure(
        AnomalyRitualController controller,
        Transform playerTransform,
        bool hideMarker)
    {
        ritual = controller;
        player = playerTransform;
        nextCueTime = Time.time + 1.6f;
        if (hideMarker)
        {
            foreach (var rendererComponent in GetComponentsInChildren<Renderer>(true))
            {
                rendererComponent.enabled = false;
            }

            foreach (var colliderComponent in GetComponentsInChildren<Collider>(true))
            {
                colliderComponent.enabled = false;
            }
        }
    }

    private void Update()
    {
        if (ritual == null || ritual.IsRecognized) return;
        if (player == null)
        {
            var playerObject = GameObject.FindGameObjectWithTag("Player");
            player = playerObject != null ? playerObject.transform : null;
            if (player == null) return;
        }

        float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
        float speed = hasPreviousPosition
            ? Vector3.Distance(player.position, previousPlayerPosition) / deltaTime
            : 0f;
        movingTime = speed >= 0.55f ? movingTime + Time.deltaTime : 0f;

        if (movingTime >= 1.2f && Time.time >= nextCueTime && !ritual.IsPrimed)
        {
            float side = Mathf.Sin(Time.time * 2.7f) >= 0f ? 0.45f : -0.45f;
            Vector3 cuePosition = player.position - player.forward * 1.35f + player.right * side;
            cuePosition.y = Mathf.Max(transform.position.y, player.position.y - 0.8f);
            ProceduralAnomalyAudio.PlayAtPosition(AnomalySoundKind.Footstep, cuePosition, 0.8f);
            ritual.NotifyAudioCue(cuePosition);
            movingTime = 0f;
            nextCueTime = Time.time + 2.5f;
        }

        previousPlayerPosition = player.position;
        hasPreviousPosition = true;
    }
}
