using UnityEngine;

public class BackTrigger : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("Back trigger entered");
            GameManager gm = FindFirstObjectByType<GameManager>();
            if (gm != null)
                gm.PlayerChose(false);
        }
    }
}
