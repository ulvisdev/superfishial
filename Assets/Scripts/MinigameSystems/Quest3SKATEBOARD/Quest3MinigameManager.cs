using UnityEngine;

public class Quest3MinigameManager : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject instructionsPanel;
    [SerializeField] private GameObject minigamePanel;

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

    void Start()
    {
        instructionsPanel.SetActive(false);
        minigamePanel.SetActive(false);
    }

    public void OpenInstructions()
    {
        minigamePanel.SetActive(false);
        instructionsPanel.SetActive(true);
    }

    public void PressGo()
    {
        instructionsPanel.SetActive(false);
        minigamePanel.SetActive(true);

        if (miniPlayer != null)
            miniPlayer.BeginMinigame();

        if (currentScroller != null)
            currentScroller.BeginMinigame();

        if (obstacleManager != null)
            obstacleManager.BeginMinigame();

        if (skateboard != null)
            skateboard.BeginMinigame();

        Canvas.ForceUpdateCanvases();
        miniPlayer.BeginMinigame();
    }

    public void EndMinigame()
    {
        miniPlayer.StopMinigame();
        minigamePanel.SetActive(false);
        PlayerFreeze.Instance.UnfreezePlayer();

        if (currentScroller != null)
            currentScroller.StopMinigame();

        if (obstacleManager != null)
            obstacleManager.StopMinigame();
    }

    public void CompleteMinigame()
    {
        if (InventoryController.Instance == null || skateboardInventoryPrefab == null)
            return;

        bool addedToInventory = InventoryController.Instance.AddItem(skateboardInventoryPrefab);

        if (!addedToInventory)
        {
            Debug.LogWarning("Skateboard could not be added because the inventory is full.");
            return;
        }

        miniPlayer.StopMinigame();
        currentScroller.StopMinigame();
        obstacleManager.StopMinigame();
        skateboard.StopMinigame();

        Item skateboardItem = skateboardInventoryPrefab.GetComponent<Item>();

        skateboardItem.ShowPopUp();
        minigamePanel.SetActive(false);
        worldSkateboard.SetActive(false);
        directionArrow.SetActive(false);
        skateboardEscapeTrigger.SetActive(false);
        minigameTrigger.SetActive(false);

        PlayerFreeze.Instance.UnfreezePlayer();

        Debug.Log("Quest 3 skateboard collected!");
    }
}