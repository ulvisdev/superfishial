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

    [Header("Story")]
    [SerializeField] private string startedFlag = "quest4_started";
    [SerializeField] private string accessFlag = "can_enter_depths";
    [SerializeField] private string collectedFlag = "skateboard_found";
    [SerializeField] private string handedInFlag = "quest4_complete";
    [SerializeField] private string pendingFlag = "skateboard_reward_pending";

    private PlayerFreeze freeze;
    private bool ownsFreeze;
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

    private void OnEnable()
    {
        StoryState.Changed += RefreshStory;
        RefreshStory();
    }

    private void Start()
    {
        RefreshStory();
    }

    private void RefreshStory()
    {
        StoryState state = StoryState.Instance;

        if (state == null || !state.IsReady)
            return;

        completed = state.HasFlag(collectedFlag) || state.HasFlag(handedInFlag);
        rewardPending = !completed && state.HasFlag(pendingFlag);

        if (completed)
            HideWorldObjects();
    }

    private bool StoryAllowsMinigame()
    {
        StoryState state = StoryState.Instance;
        return state != null && state.IsReady && state.HasFlag(startedFlag) && state.HasFlag(accessFlag) && !state.HasFlag(collectedFlag) && !state.HasFlag(handedInFlag);
    }

    private void FreezePlayer()
    {
        if (ownsFreeze)
            return;

        freeze = PlayerFreeze.Instance;

        if (freeze == null)
            return;

        freeze.FreezePlayer();
        ownsFreeze = true;
    }

    private void ReleasePlayer()
    {
        if (!ownsFreeze)
            return;

        ownsFreeze = false;

        if (freeze != null)
            freeze.UnfreezePlayer();
    }

    public void OpenInstructions()
    {
        if (!StoryAllowsMinigame() || PauseController.IsGamePaused || StoryCutsceneController.IsPlaying || RoomTravelController.IsTravelling || NPC.ActiveNPC != null)
            return;

        if (PlayerFreeze.Instance != null && PlayerFreeze.Instance.IsFrozen)
            return;

        if (completed || running || instructionsPanel.activeSelf)
            return;

        FreezePlayer();

        if (rewardPending)
        {
            CompleteMinigame();
            return;
        }

        minigamePanel.SetActive(false);
        instructionsPanel.SetActive(true);
    }

    public void PressGo()
    {
        if (PauseController.IsGamePaused)
            return;

        if (!StoryAllowsMinigame() || !instructionsPanel.activeSelf || running || completed || rewardPending)
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

        ReleasePlayer();

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
        StoryState.Instance.SetFlag(pendingFlag);

        if (InventoryController.Instance == null || skateboardInventoryPrefab == null)
        {
            Debug.LogWarning("Skateboard reward is missing its inventory controller or prefab.");
            EndMinigame();
            SaveController.Instance?.RequestSave();
            return;
        }

        if (!InventoryController.Instance.AddItem(skateboardInventoryPrefab))
        {
            EndMinigame();

            if (inventoryFullPanel != null)
                inventoryFullPanel.SetActive(true);

            Debug.LogWarning("Inventory full. Free a slot, then enter the skateboard trigger again to claim the skateboard.");
            SaveController.Instance?.RequestSave();
            return;
        }

        completed = true;
        rewardPending = false;

        EndMinigame();

        if (inventoryFullPanel != null)
            inventoryFullPanel.SetActive(false);

        HideWorldObjects();
        StoryState.Instance.SetFlag(collectedFlag);
        StoryState.Instance.ClearFlag(pendingFlag);

        if (questObjectiveCompletion != null)
            questObjectiveCompletion.CompleteObjective();

        SaveController.Instance?.RequestSave();

        Item skateboardItem = skateboardInventoryPrefab.GetComponent<Item>();

        if (skateboardItem != null)
            skateboardItem.ShowPopUp();
    }

    private void HideWorldObjects()
    {
        if (worldSkateboard != null)
            worldSkateboard.SetActive(false);

        if (directionArrow != null)
            directionArrow.SetActive(false);

        if (skateboardEscapeTrigger != null)
            skateboardEscapeTrigger.SetActive(false);

        if (minigameTrigger != null)
            minigameTrigger.SetActive(false);
    }

    private void OnDisable()
    {
        StoryState.Changed -= RefreshStory;

        if (running || ownsFreeze)
            EndMinigame();
    }
}
