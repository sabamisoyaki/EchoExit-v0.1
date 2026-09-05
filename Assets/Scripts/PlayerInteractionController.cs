using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public interface IPlayerInteractable
{
    string InteractionPrompt { get; }
    bool CanInteract { get; }
    void Interact(GameObject interactor);
}

/// <summary>
/// 一人称視点から注視・叩く・扉操作を送る共通入力。
/// </summary>
public sealed class PlayerInteractionController : MonoBehaviour
{
    [SerializeField] private Camera viewCamera;
    [SerializeField, Min(0.5f)] private float interactionDistance = 4f;
    [SerializeField] private LayerMask interactionMask = ~0;
    [SerializeField] private TMP_Text promptText;

    private AnomalyRitualController observedRitual;

    private void Start()
    {
        if (viewCamera == null)
        {
            viewCamera = Camera.main;
        }

        EnsurePromptUi();
    }

    private void Update()
    {
        if (viewCamera == null)
        {
            viewCamera = Camera.main;
            if (viewCamera == null) return;
        }

        var ray = new Ray(viewCamera.transform.position, viewCamera.transform.forward);
        bool hasHit = Physics.Raycast(
            ray,
            out RaycastHit hit,
            interactionDistance,
            interactionMask,
            QueryTriggerInteraction.Collide);

        observedRitual = hasHit ? hit.collider.GetComponentInParent<AnomalyRitualController>() : null;
        observedRitual?.Observe(Time.deltaTime);

        IPlayerInteractable interactable = null;
        if (hasHit)
        {
            interactable = hit.collider.GetComponentInParent(typeof(IPlayerInteractable)) as IPlayerInteractable;
        }

        if (promptText != null)
        {
            promptText.text = interactable != null && interactable.CanInteract
                ? interactable.InteractionPrompt
                : string.Empty;
        }

        if (interactable != null && interactable.CanInteract && InteractionPressed())
        {
            interactable.Interact(gameObject);
        }

        if (observedRitual != null && HitPressed())
        {
            observedRitual.Hit();
        }
    }

    private static bool InteractionPressed()
    {
        return Input.GetKeyDown(KeyCode.E) ||
               (Gamepad.current != null && Gamepad.current.buttonWest.wasPressedThisFrame);
    }

    private static bool HitPressed()
    {
        return Input.GetMouseButtonDown(0) ||
               (Gamepad.current != null && Gamepad.current.rightTrigger.wasPressedThisFrame);
    }

    private void EnsurePromptUi()
    {
        if (promptText != null) return;

        var canvas = FindFirstObjectByType<Canvas>();
        var template = canvas != null ? canvas.GetComponentInChildren<TMP_Text>(true) : null;
        if (canvas == null || template == null) return;

        var promptObject = Instantiate(template.gameObject, canvas.transform, false);
        promptObject.name = "InteractionPrompt";
        promptText = promptObject.GetComponent<TMP_Text>();
        promptText.text = string.Empty;
        promptText.alignment = TextAlignmentOptions.Center;
        promptText.fontSize = 28f;
        promptText.raycastTarget = false;

        var rect = promptText.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 80f);
        rect.sizeDelta = new Vector2(900f, 80f);

        var crosshairObject = Instantiate(template.gameObject, canvas.transform, false);
        crosshairObject.name = "Crosshair";
        var crosshair = crosshairObject.GetComponent<TMP_Text>();
        crosshair.text = "+";
        crosshair.alignment = TextAlignmentOptions.Center;
        crosshair.fontSize = 30f;
        crosshair.raycastTarget = false;
        var crosshairRect = crosshair.rectTransform;
        crosshairRect.anchorMin = new Vector2(0.5f, 0.5f);
        crosshairRect.anchorMax = new Vector2(0.5f, 0.5f);
        crosshairRect.pivot = new Vector2(0.5f, 0.5f);
        crosshairRect.anchoredPosition = Vector2.zero;
        crosshairRect.sizeDelta = new Vector2(60f, 60f);
    }
}
