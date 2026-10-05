using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class InteractionDetector : MonoBehaviour
{
    public GameObject interactionIcon;

    private readonly List<Collider> nearby = new();

    private void Start()
    {
        ClearNearby();
    }

    private iInteractable GetNearby()
    {
        for (int i = nearby.Count - 1; i >= 0; i--)
        {
            Collider candidate = nearby[i];

            if (candidate == null || !candidate.enabled || !candidate.gameObject.activeInHierarchy)
            {
                nearby.RemoveAt(i);
                continue;
            }

            if (candidate.TryGetComponent(out iInteractable target) && target.CanInteract())
                return target;
        }

        return null;
    }

    private void Update()
    {
        if (interactionIcon != null)
            interactionIcon.SetActive(!StoryCutsceneController.IsPlaying && !RoomTravelController.IsTravelling && NPC.ActiveNPC == null && GetNearby() != null);
    }

    public void OnInteract(InputAction.CallbackContext context)
    {
        if (!context.started || StoryCutsceneController.IsPlaying || RoomTravelController.IsTravelling)
            return;

        if (NPC.ActiveNPC != null)
        {
            NPC.ActiveNPC.Interact();
            return;
        }

        GetNearby()?.Interact();
    }

    public void ClearNearby()
    {
        nearby.Clear();

        if (interactionIcon != null)
            interactionIcon.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent(out iInteractable target) && !nearby.Contains(other))
            nearby.Add(other);
    }

    private void OnTriggerExit(Collider other)
    {
        nearby.Remove(other);
    }
}
