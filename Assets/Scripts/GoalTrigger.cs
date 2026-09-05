using UnityEngine;
using UnityEngine.SceneManagement;

public class GoalTrigger : MonoBehaviour, IPlayerInteractable
{
    private bool consumed;

    public string InteractionPrompt => "666号扉を開く  [E / X]";
    public bool CanInteract => !consumed;

    public void Interact(GameObject interactor)
    {
        if (consumed || interactor == null || !interactor.CompareTag("Player")) return;

        consumed = true;
        Debug.Log("Goal reached");
        GameManager gameManager = FindFirstObjectByType<GameManager>();
        if (gameManager != null)
        {
            gameManager.CompleteGoal();
        }
        else
        {
            SceneManager.LoadScene("endTitle");
        }
    }
}
