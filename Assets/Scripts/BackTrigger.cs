using UnityEngine;

public class BackTrigger : MonoBehaviour, IPlayerInteractable
{
    private bool consumed;

    public string InteractionPrompt => "後ろの扉を開く  [E / X]";
    public bool CanInteract => !consumed;

    public void Interact(GameObject interactor)
    {
        if (consumed || interactor == null || !interactor.CompareTag("Player")) return;

        var gameManager = FindFirstObjectByType<GameManager>();
        if (gameManager == null) return;

        consumed = true;
        gameManager.PlayerChose(false);
    }
}
