using UnityEngine;

public class ForwardTrigger : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("Forward trigger entered");
            GameManager gm = FindFirstObjectByType<GameManager>();
            if (gm != null)
                gm.PlayerChose(true);
        }
    }
}

