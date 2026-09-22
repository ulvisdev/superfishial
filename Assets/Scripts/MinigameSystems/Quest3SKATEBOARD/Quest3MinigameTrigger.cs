using UnityEngine;

public class Quest3MinigameTrigger : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject directionArrow;
    [SerializeField] private Quest3MinigameManager minigameManager;

    private bool triggered = false;
    private bool waitingForExit;
    private Collider triggerCollider;

    void Awake()
    {
        triggerCollider = GetComponent<Collider>();
    }

    void Start()
    {
        if (directionArrow != null)
            directionArrow.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (triggered || waitingForExit || !other.CompareTag("Player"))
            return;

        BeginMinigame();
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
            waitingForExit = false;
    }

    public void ShowArrowIfNeeded()
    {
        if (triggered)
            return;

        if (directionArrow != null)
            directionArrow.SetActive(true);
    }

    public void Rearm()
    {
        triggered = false;
        waitingForExit = true;
        triggerCollider.enabled = true;
        ShowArrowIfNeeded();
    }

    private void BeginMinigame()
    {
        if (minigameManager == null)
            return;

        triggered = true;
        triggerCollider.enabled = false;

        if (directionArrow != null)
            directionArrow.SetActive(false);

        minigameManager.OpenInstructions();
    }
}
