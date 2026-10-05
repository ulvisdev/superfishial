using UnityEngine;

public class MudSearchSpot : MonoBehaviour, iInteractable
{
    [Header("Mud Spot")]
    [SerializeField] private int spotID;
    [SerializeField] private GameObject mudVisual;

    private bool isCleared = false;

    public int SpotID
    {
        get
        {
            return spotID;
        }
    }

    public bool IsCleared
    {
        get
        {
            return isCleared;
        }
    }

    public void Interact()
    {
        if (!CanInteract())
            return;

        MudSearchManager.Instance.OpenSearch(this);
    }

    public bool CanInteract()
    {
        if (isCleared)
            return false;

        if (MudSearchManager.Instance == null)
            return false;

        return MudSearchManager.Instance.CanSearch(this);
    }

    public bool WouldHideManager(Transform manager)
    {
        return mudVisual != null && manager.IsChildOf(mudVisual.transform);
    }

    public void ApplyState(bool cleared, bool visible)
    {
        isCleared = cleared;

        if (mudVisual != null)
            mudVisual.SetActive(visible);
    }

    public void ClearSpot()
    {
        if (MudSearchManager.Instance != null)
            MudSearchManager.Instance.RecordClearedSpot(this);
    }
}
