using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Quest3MinigameTrigger : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject directionArrow;
    [SerializeField] private Quest3MinigameManager minigameManager;

    private bool triggered = false;
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
        if (triggered || !other.CompareTag("Player"))
            return;

        BeginMinigame();
    }

    public void ShowArrowIfNeeded()
    {
        if (triggered)
            return;

        if (directionArrow != null)
            directionArrow.SetActive(true);
    }

    private void BeginMinigame()
    {
        triggered = true;
        triggerCollider.enabled = false;

        if (directionArrow != null)
            directionArrow.SetActive(false);

        PlayerFreeze.Instance.FreezePlayer();

        if (minigameManager != null)
            minigameManager.OpenInstructions();
    }
}