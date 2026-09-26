using UnityEngine;

public class Quest3MinigameManager : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject instructionsPanel;
    [SerializeField] private GameObject minigamePanel;
    [SerializeField] private GameObject inventoryFullPanel;

    [Header("Minigame")]
    [SerializeField] private Quest3MiniPlayer miniPlayer;
    [SerializeField] private Quest3CurrentScroller currentScroller;
    [SerializeField] private Quest3ObstacleManager obstacleManager;
    [SerializeField] private Quest3MinigameSkateboard skateboard;

    [Header("Reward")]
    [SerializeField] private GameObject skateboardInventoryPrefab;

    [Header("World Quest Objects")]
    [SerializeField] private GameObject worldSkateboard;
    [SerializeField] private GameObject directionArrow;
    [SerializeField] private GameObject skateboardEscapeTrigger;
    [SerializeField] private GameObject minigameTrigger;

    [SerializeField] private QuestObjectiveCompletion questObjectiveCompletion;

    private bool running;
    private bool completed;
    private bool rewardPending;

    void Awake()
    {
        instructionsPanel.SetActive(false);
        minigamePanel.SetActive(false);

        if (inventoryFullPanel != null)
            inventoryFullPanel.SetActive(false);
    }

    public void OpenInstructions()
    {
        if (completed || running)
            return;

        if (rewardPending)
        {
            CompleteMinigame();
            return;
        }

        if (PlayerFreeze.Instance != null)
            PlayerFreeze.Instance.FreezePlayer();

        minigamePanel.SetActive(false);
        instructionsPanel.SetActive(true);
    }

    public void PressGo()
    {
        if (running || completed || rewardPending)
            return;

        instructionsPanel.SetActive(false);
        minigamePanel.SetActive(true);
        Canvas.ForceUpdateCanvases();
        running = true;

        if (miniPlayer != null)
            miniPlayer.BeginMinigame();

        if (currentScroller != null)
            currentScroller.BeginMinigame();

        if (skateboard != null)
            skateboard.BeginMinigame();

        if (obstacleManager != null)
            obstacleManager.BeginMinigame();
    }

    private void StopRun()
    {
        running = false;

        if (obstacleManager != null)
            obstacleManager.StopMinigame();

        if (miniPlayer != null)
            miniPlayer.StopMinigame();

        if (currentScroller != null)
            currentScroller.StopMinigame();

        if (skateboard != null)
            skateboard.StopMinigame();
    }

    public void EndMinigame()
    {
        StopRun();
        instructionsPanel.SetActive(false);
        minigamePanel.SetActive(false);

        if (PlayerFreeze.Instance != null)
            PlayerFreeze.Instance.UnfreezePlayer();

        if (!completed && minigameTrigger != null)
        {
            Quest3MinigameTrigger trigger = minigameTrigger.GetComponent<Quest3MinigameTrigger>();

            if (trigger != null)
                trigger.Rearm();
        }
    }

    public void CompleteMinigame()
    {
        if (completed || (!running && !rewardPending))
            return;

        rewardPending = true;
        StopRun();

        if (InventoryController.Instance == null || skateboardInventoryPrefab == null)
        {
            Debug.LogWarning("Quest 3 reward is missing its inventory controller or prefab.");
            EndMinigame();
            return;
        }

        if (!InventoryController.Instance.AddItem(skateboardInventoryPrefab))
        {
            EndMinigame();

            if (inventoryFullPanel != null)
                inventoryFullPanel.SetActive(true);

            Debug.LogWarning("Inventory full. Free a slot, then enter the Quest 3 trigger again to claim the skateboard.");
            return;
        }

        completed = true;
        rewardPending = false;

        if (questObjectiveCompletion != null) 
            questObjectiveCompletion.CompleteObjective();

        EndMinigame();

        if (inventoryFullPanel != null)
            inventoryFullPanel.SetActive(false);

        if (worldSkateboard != null)
            worldSkateboard.SetActive(false);

        if (directionArrow != null)
            directionArrow.SetActive(false);

        if (skateboardEscapeTrigger != null)
            skateboardEscapeTrigger.SetActive(false);

        if (minigameTrigger != null)
            minigameTrigger.SetActive(false);

        Item skateboardItem = skateboardInventoryPrefab.GetComponent<Item>();

        if (skateboardItem != null)
            skateboardItem.ShowPopUp();
    }
}
