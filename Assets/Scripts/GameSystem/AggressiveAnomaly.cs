using UnityEngine;
using UnityEngine.AI;

/// <summary>認識後に追跡する異変。NavMesh があれば利用し、未ベイクの検証用フィールドでは直進へ戻す。</summary>
public sealed class AggressiveAnomaly : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float chaseSpeed = 2.8f;
    [SerializeField, Min(0.1f)] private float captureDistance = 1.1f;
    [SerializeField, Min(1.2f)] private float graceDuration = 1.2f;

    private GameManager gameManager;
    private Transform player;
    private NavMeshAgent navMeshAgent;
    private float chaseStartTime;
    private bool active;

    public void Configure(GameManager owner, Transform playerTransform)
    {
        gameManager = owner;
        player = playerTransform;
        active = false;
        enabled = false;
    }

    public void Activate()
    {
        active = true;
        chaseStartTime = Time.time + Mathf.Max(1.2f, graceDuration);
        TryPrepareNavMeshAgent();
        if (navMeshAgent != null && navMeshAgent.isOnNavMesh)
        {
            navMeshAgent.isStopped = true;
        }
        enabled = true;
    }

    private void Update()
    {
        if (!active || gameManager == null || !gameManager.IsRoundActive) return;
        ResolvePlayer();
        if (player == null) return;

        if (Time.time < chaseStartTime)
        {
            if (navMeshAgent != null && navMeshAgent.isOnNavMesh) navMeshAgent.isStopped = true;
            return;
        }

        Vector3 offset = player.position - transform.position;
        Vector3 planarOffset = Vector3.ProjectOnPlane(offset, Vector3.up);
        if (planarOffset.magnitude <= captureDistance)
        {
            active = false;
            if (navMeshAgent != null && navMeshAgent.isOnNavMesh) navMeshAgent.isStopped = true;
            gameManager.PlayerCaught(name.Replace("(Clone)", string.Empty).Trim());
            return;
        }

        if (TryChaseWithNavMesh()) return;
        ChaseDirectly(planarOffset);
    }

    private bool TryChaseWithNavMesh()
    {
        if (navMeshAgent == null || !navMeshAgent.enabled || !navMeshAgent.isOnNavMesh) return false;

        navMeshAgent.isStopped = false;
        navMeshAgent.speed = chaseSpeed;
        navMeshAgent.stoppingDistance = captureDistance * 0.7f;
        navMeshAgent.SetDestination(player.position);
        return true;
    }

    private void ChaseDirectly(Vector3 planarOffset)
    {
        if (planarOffset.sqrMagnitude <= 0.0001f) return;

        Vector3 direction = planarOffset.normalized;
        transform.position += direction * (chaseSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            Quaternion.LookRotation(direction, Vector3.up),
            Time.deltaTime * 8f);
    }

    private void TryPrepareNavMeshAgent()
    {
        if (!NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 1.5f, NavMesh.AllAreas)) return;

        navMeshAgent = GetComponent<NavMeshAgent>();
        if (navMeshAgent == null) navMeshAgent = gameObject.AddComponent<NavMeshAgent>();
        navMeshAgent.speed = chaseSpeed;
        navMeshAgent.angularSpeed = 540f;
        navMeshAgent.acceleration = 12f;
        navMeshAgent.stoppingDistance = captureDistance * 0.7f;
        navMeshAgent.autoBraking = true;
        transform.position = hit.position;
    }

    private void ResolvePlayer()
    {
        if (player != null) return;
        var playerObject = GameObject.FindGameObjectWithTag("Player");
        player = playerObject != null ? playerObject.transform : null;
    }
}
