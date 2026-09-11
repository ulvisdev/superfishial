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

    void Update()
    {
        if (interactableInRange == null)
        {
            interactionIcon.SetActive(false);
            return;
        }

        interactionIcon.SetActive(interactableInRange.CanInteract());
    }

    public void OnInteract(InputAction.CallbackContext context)
    {
        if (!context.started)
            return;

        if (interactableInRange == null)
            return;

        if (!interactableInRange.CanInteract())
            return;

        interactableInRange.Interact();
    }

    void OnTriggerEnter(Collider collision)
    {
        if (collision.TryGetComponent(out iInteractable interactable))
            interactableInRange = interactable;
    }

    void OnTriggerExit(Collider collision)
    {
        if (collision.TryGetComponent(out iInteractable interactable))
        {
            if (interactable == interactableInRange)
                interactableInRange = null;
        }
    }
}