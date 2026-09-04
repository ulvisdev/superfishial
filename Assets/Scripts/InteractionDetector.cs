using UnityEngine;
using UnityEngine.InputSystem;

public class InteractionDetector : MonoBehaviour
{
    private iInteractable interactableInRange = null;
    public GameObject interactionIcon;

    void Start()
    {
        interactionIcon.SetActive(false);
    }

    public void OnInteract(InputAction.CallbackContext context)
    {
        if (context.started && interactableInRange != null)
        {
            interactableInRange.Interact();
            if (!interactableInRange.CanInteract())
                interactionIcon.SetActive(false);
        }
    }

    void OnTriggerEnter(Collider collision)
    {
        if (collision.TryGetComponent(out iInteractable interactable) && interactable.CanInteract())
        {
            interactableInRange = interactable;
            interactionIcon.SetActive(true);
        }
    }

    void OnTriggerExit(Collider collision)
    {
        if (collision.TryGetComponent(out iInteractable interactable) && interactable == interactableInRange)
        {
            interactableInRange = null;
            interactionIcon.SetActive(false);
        }
    }
}
