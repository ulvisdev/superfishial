using UnityEngine;
using UnityEngine.Events;

public class StoryDoor : MonoBehaviour, iInteractable
{
    [SerializeField] private string destinationRoomID;
    [SerializeField] private Transform arrivalOverride;
    [SerializeField] private StoryCondition condition = new();
    [SerializeField] private bool enterAutomatically;
    [SerializeField] private bool useBubbles;
    [SerializeField] private UnityEvent onLocked;

    public bool CanInteract()
    {
        return RoomTravelController.Instance != null && RoomTravelController.Instance.CanTravel();
    }

    public void Interact()
    {
        if (!CanInteract())
            return;

        if (condition != null && !condition.IsMet())
        {
            onLocked?.Invoke();
            return;
        }

        if (!RoomTravelController.Instance.Travel(destinationRoomID, arrivalOverride, useBubbles))
            Debug.LogError("Door destination is missing from the room catalog: " + destinationRoomID, this);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (enterAutomatically && other.GetComponentInParent<PlayerMovement>() != null)
            Interact();
    }
}
