using UnityEngine;

public class ForwardTrigger : MonoBehaviour
{
    private bool consumed;

    private void OnTriggerEnter(Collider other)
    {
        if (!consumed && other.CompareTag("Player"))
        {
            consumed = true;
            Debug.Log("Forward trigger entered");
            GameManager gm = FindFirstObjectByType<GameManager>();
            if (gm != null) gm.PlayerChose(true);
            else consumed = false;
        }
    }
}

