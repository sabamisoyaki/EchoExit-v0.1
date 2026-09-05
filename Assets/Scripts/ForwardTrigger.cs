using UnityEngine;

public class ForwardTrigger : MonoBehaviour, IPlayerInteractable
{
    private bool consumed;

    public string InteractionPrompt => "前の扉を開く  [E / X]";
    public bool CanInteract => !consumed;

    public void Interact(GameObject interactor)
    {
        if (consumed || interactor == null || !interactor.CompareTag("Player")) return;

        var gameManager = FindFirstObjectByType<GameManager>();
        if (gameManager == null) return;

        consumed = true;
        gameManager.PlayerChose(true);
    }
}

