using UnityEngine;
using UnityEngine.SceneManagement;

public class GoalTrigger : MonoBehaviour
{
    [SerializeField] private GameManager gameManager;
    [SerializeField] private string fallbackSceneName = "endTitle";

    private void Awake()
    {
        if (gameManager == null)
        {
            gameManager = FindFirstObjectByType<GameManager>();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        Debug.Log("Goal reached");
        if (gameManager != null)
        {
            gameManager.OnGoalTriggerReached();
            return;
        }

        SceneManager.LoadScene(fallbackSceneName);
    }
}
