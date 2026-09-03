using UnityEngine;

public class BackTrigger : MonoBehaviour
{
    private bool consumed;

    private void OnTriggerEnter(Collider other)
    {
        if (!consumed && other.CompareTag("Player"))
        {
            consumed = true;
            Debug.Log("Back trigger entered");
            GameManager gm = FindFirstObjectByType<GameManager>();
            if (gm != null) gm.PlayerChose(false);
            else consumed = false;
        }
    }
}
