using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

[Serializable]
public class BuriedJunkOption
{
    public string displayName;
    public Sprite sprite;
}

public class MudSearchManager : MonoBehaviour
{
    public static MudSearchManager Instance { get; private set; }

    [Header("Quest")]
    [SerializeField] private MudSearchSpot[] searchSpots;
    [SerializeField] private bool startQuestAutomaticallyForTesting = false;
    [SerializeField] private string questStartedFlag = "quest2_started";
    [SerializeField] private string ringFoundFlag = "ring_found";
    [SerializeField] private string questCompleteFlag = "quest2_complete";
    [SerializeField] private string ringLocationPrefix = "q2_ring_spot_";
    [SerializeField] private string clearedSpotPrefix = "q2_mud_cleared_";

    [Header("UI")]
    [SerializeField] private GameObject searchPanel;
    [SerializeField] private GameObject instructionsPanel;
    [SerializeField] private RectTransform searchArea;
    [SerializeField] private TMP_Text statusText;

    [Header("Patch Generation")]
    [SerializeField] private GameObject mudPatchPrefab;
    [SerializeField] private int minimumPatchCount = 6;
    [SerializeField] private int maximumPatchCount = 8;
    [SerializeField] private float edgePadding = 40f;
    [SerializeField] private float minimumPatchSpacing = 100f;

    [Header("Buried Objects")]
    [SerializeField] private Sprite ringSprite;
    [SerializeField] private GameObject ringInventoryPrefab;
    [SerializeField] private BuriedJunkOption[] junkOptions;

    [Range(0f, 1f)]
    [SerializeField] private float junkChance = 0.7f;

    [Header("Ring Found")]
    [SerializeField] private float ringFoundCloseDelay = 1.5f;

    private bool closingAfterRing = false;
    private bool rewardPending;
    private bool syncing;
    private bool initialized;
    private bool ownsFreeze;
    private bool validSetup;

    public bool CanDig => panelOpen && currentSpot != null && !closingAfterRing && !rewardPending && !PauseController.IsGamePaused;

    private int ringSpotID = -1;

    private bool questActive = false;
    private bool ringFound = false;
    private bool panelOpen = false;
    private bool instructionsSeen = false;

    private MudSearchSpot currentSpot;

    private int patchesRemaining = 0;

    private List<Vector2> generatedPositions = new List<Vector2>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnEnable()
    {
        StoryState.Changed += SyncStory;
    }

    private void OnDisable()
    {
        StoryState.Changed -= SyncStory;
        StopAllCoroutines();
        CloseSearch();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        if (searchPanel != null)
            searchPanel.SetActive(false);

        if (instructionsPanel != null)
            instructionsPanel.SetActive(false);

        validSetup = searchPanel != null && instructionsPanel != null && searchArea != null && mudPatchPrefab != null && mudPatchPrefab.GetComponent<MudPatchUI>() != null && mudPatchPrefab.GetComponent<RectTransform>() != null && ringInventoryPrefab != null && ringInventoryPrefab.GetComponent<Item>() != null && searchSpots != null && searchSpots.Length > 0;
        HashSet<int> ids = new();

        if (searchSpots != null)
            foreach (MudSearchSpot spot in searchSpots)
            {
                if (spot == null || spot.SpotID < 0 || !ids.Add(spot.SpotID))
                    validSetup = false;

                if (spot != null)
                {
                    if (spot.WouldHideManager(transform))
                        validSetup = false;
                    else
                        spot.ApplyState(false, false);
                }
            }

        if (!validSetup)
            Debug.LogError("Mud setup needs valid UI, prefabs and unique non-negative Spot IDs. Keep the manager outside the spots and their visuals.", this);

        if (startQuestAutomaticallyForTesting)
            Debug.LogWarning("Automatic mud testing is no longer used. Accept Q2 through dialogue to start it.", this);

        initialized = true;
        SyncStory();
    }

    private void SyncStory()
    {
        StoryState state = StoryState.Instance;

        if (!initialized || !validSetup || syncing || state == null || !state.IsReady)
            return;

        syncing = true;

        try
        {
            ringFound = state.HasFlag(ringFoundFlag);
            questActive = state.HasFlag(questStartedFlag) && !state.HasFlag(questCompleteFlag);
            ringSpotID = -1;

            foreach (MudSearchSpot spot in searchSpots)
                if (state.HasFlag(ringLocationPrefix + spot.SpotID))
                {
                    if (ringSpotID >= 0)
                    {
                        Debug.LogError("More than one saved ring location exists. Check the Q2 ring-location flags.", this);
                        questActive = false;
                        break;
                    }

                    ringSpotID = spot.SpotID;
                }

            if (questActive && !ringFound && ringSpotID < 0)
            {
                List<MudSearchSpot> candidates = new();

                foreach (MudSearchSpot spot in searchSpots)
                    if (!state.HasFlag(clearedSpotPrefix + spot.SpotID))
                        candidates.Add(spot);

                if (candidates.Count > 0)
                {
                    ringSpotID = candidates[UnityEngine.Random.Range(0, candidates.Count)].SpotID;
                    state.SetFlag(ringLocationPrefix + ringSpotID);
                }
                else
                {
                    questActive = false;
                    Debug.LogError("No uncleared mud spot remains for the ring. Check the saved Q2 flags.", this);
                }
            }

            foreach (MudSearchSpot spot in searchSpots)
            {
                bool cleared = state.HasFlag(clearedSpotPrefix + spot.SpotID);
                spot.ApplyState(cleared, questActive && !ringFound && !cleared);
            }

            if (panelOpen && (!questActive || ringFound || currentSpot == null || currentSpot.IsCleared) && !closingAfterRing)
            {
                StopAllCoroutines();
                CloseSearch();
            }
        }
        finally
        {
            syncing = false;
        }
    }

    public void BeginQuest()
    {
        SyncStory();
    }

    public void RecordClearedSpot(MudSearchSpot spot)
    {
        if (spot == null || !questActive || spot.SpotID == ringSpotID || StoryState.Instance == null || !StoryState.Instance.IsReady)
            return;

        StoryState.Instance.SetFlag(clearedSpotPrefix + spot.SpotID);
    }

    public bool CanSearch(MudSearchSpot spot)
    {
        if (!validSetup || StoryState.Instance == null || !StoryState.Instance.IsReady || PauseController.IsGamePaused || StoryCutsceneController.IsPlaying || RoomTravelController.IsTravelling || NPC.ActiveNPC != null || PlayerFreeze.Instance == null || PlayerFreeze.Instance.IsFrozen)
            return false;

        if (!questActive)
            return false;

        if (ringFound)
            return false;

        if (panelOpen)
            return false;

        if (spot == null)
            return false;

        if (spot.IsCleared)
            return false;

        return true;
    }

    public void OpenSearch(MudSearchSpot spot)
    {
        if (PauseController.IsGamePaused)
            return;

        if (!CanSearch(spot))
            return;

        currentSpot = spot;
        panelOpen = true;

        PlayerFreeze.Instance.FreezePlayer();
        ownsFreeze = true;

        if (!instructionsSeen)
        {
            OpenInstructions();
            return;
        }

        OpenSearchPanel();
    }

    private void OpenInstructions()
    {
        searchPanel.SetActive(false);
        instructionsPanel.SetActive(true);
    }

    public void PressInstructionsGo()
    {
        if (PauseController.IsGamePaused)
            return;

        if (!panelOpen)
            return;

        if (currentSpot == null)
            return;

        instructionsSeen = true;
        instructionsPanel.SetActive(false);
        OpenSearchPanel();
    }

    private void OpenSearchPanel()
    {
        instructionsPanel.SetActive(false);
        searchPanel.SetActive(true);

        Canvas.ForceUpdateCanvases();

        ClearOldPatches();
        GeneratePatches();

        SetStatus("SEARCHING...");
    }

    private void GeneratePatches()
    {
        generatedPositions.Clear();
        int minimum = Mathf.Max(1, minimumPatchCount);
        int maximum = Mathf.Max(minimum, maximumPatchCount);
        int patchCount = UnityEngine.Random.Range(minimum, maximum + 1);

        patchesRemaining = patchCount;

        bool currentSpotContainsRing = currentSpot.SpotID == ringSpotID;
        int ringPatchIndex = -1;

        if (currentSpotContainsRing)
            ringPatchIndex = UnityEngine.Random.Range(0, patchCount);

        for (int i = 0; i < patchCount; i++)
        {
            GameObject patchObject = Instantiate(mudPatchPrefab, searchArea);

            MudPatchUI patch = patchObject.GetComponent<MudPatchUI>();
            RectTransform patchRect = patchObject.GetComponent<RectTransform>();

            patchRect.anchoredPosition = GetRandomPatchPosition(patchRect);

            bool thisPatchContainsRing = i == ringPatchIndex;

            if (thisPatchContainsRing)
                patch.Setup(this, true, "Wedding Ring", ringSprite);
            else
                SetupRandomJunk(patch);
        }
    }

    private void SetupRandomJunk(MudPatchUI patch)
    {
        bool shouldContainJunk = UnityEngine.Random.value <= junkChance;

        if (shouldContainJunk && junkOptions != null && junkOptions.Length > 0)
        {
            int junkIndex = UnityEngine.Random.Range(0, junkOptions.Length);
            BuriedJunkOption junk = junkOptions[junkIndex];
            patch.Setup(this, false, junk.displayName, junk.sprite);
        }
        else
            patch.Setup(this, false, "Nothing", null);
    }

    private Vector2 GetRandomPatchPosition(RectTransform patchRect)
    {
        float halfWidth = patchRect.rect.width / 2f;
        float halfHeight = patchRect.rect.height / 2f;

        float minimumX = searchArea.rect.xMin + halfWidth + edgePadding;
        float maximumX = searchArea.rect.xMax - halfWidth - edgePadding;
        float minimumY = searchArea.rect.yMin + halfHeight + edgePadding;
        float maximumY = searchArea.rect.yMax - halfHeight - edgePadding;

        Vector2 candidatePosition = Vector2.zero;

        for (int attempt = 0; attempt < 100; attempt++)
        {
            candidatePosition = new Vector2(UnityEngine.Random.Range(minimumX, maximumX), UnityEngine.Random.Range(minimumY, maximumY));

            bool tooClose = false;

            foreach (Vector2 existingPosition in generatedPositions)
            {
                float distance = Vector2.Distance(candidatePosition, existingPosition);

                if (distance < minimumPatchSpacing)
                {
                    tooClose = true;
                    break;
                }
            }

            if (!tooClose)
            {
                generatedPositions.Add(candidatePosition);
                return candidatePosition;
            }
        }

        generatedPositions.Add(candidatePosition);

        return candidatePosition;
    }

    public void PatchCleared(bool containsRing, string resultName)
    {
        if (!CanDig)
            return;

        patchesRemaining--;

        if (containsRing)
        {
            FindRing();
            return;
        }

        if (resultName == "Nothing")
        {
            SetStatus("Nothing here.");
            SoundEffectManager.Play("MudJunk");
        }
        else
        {
            SetStatus("You found " + resultName + ".");
            SoundEffectManager.Play("MudJunk");
        }

        if (patchesRemaining <= 0 && !closingAfterRing)
        {
            closingAfterRing = true;
            StartCoroutine(FinishSpotAfterDelay());
        }
    }

    private void FindRing()
    {
        if (ringFound)
            return;

        bool addedToInventory = InventoryController.Instance != null && InventoryController.Instance.AddItem(ringInventoryPrefab);

        if (!addedToInventory)
        {
            rewardPending = true;
            closingAfterRing = true;
            SetStatus("Free an inventory slot, then search this spot again to claim the ring.");
            StartCoroutine(CloseAfterFindingRing());
            return;
        }

        ringFound = true;
        closingAfterRing = true;
        StoryState.Instance.SetFlag(ringFoundFlag);

        Item ringItem = ringInventoryPrefab.GetComponent<Item>();

        if (ringItem != null)
            ringItem.ShowPopUp();

        SetStatus("You found the wedding ring!");
        SoundEffectManager.Play("MudRing");

        Debug.Log("Wedding ring found!");

        StartCoroutine(CloseAfterFindingRing());
    }

    private IEnumerator CloseAfterFindingRing()
    {
        yield return MinigameTime.Wait(ringFoundCloseDelay);
        CloseSearch();
    }

    private IEnumerator FinishSpotAfterDelay()
    {
        yield return MinigameTime.Wait(0.6f);

        if (currentSpot != null)
            currentSpot.ClearSpot();

        CloseSearch();
    }

    public void CancelSearch()
    {
        StopAllCoroutines();
        CloseSearch();
    }

    private void CloseSearch()
    {
        if (searchPanel != null)
            searchPanel.SetActive(false);

        if (instructionsPanel != null)
            instructionsPanel.SetActive(false);

        ClearOldPatches();

        panelOpen = false;
        rewardPending = false;
        closingAfterRing = false;
        currentSpot = null;

        if (ownsFreeze && PlayerFreeze.Instance != null)
            PlayerFreeze.Instance.UnfreezePlayer();

        ownsFreeze = false;

        if (SaveController.Instance != null)
            SaveController.Instance.RequestSave();
    }

    private void ClearOldPatches()
    {
        if (searchArea == null)
            return;

        foreach (Transform child in searchArea)
        {
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }
    }

    private void SetStatus(string message)
    {
        if (statusText == null)
            return;

        statusText.text = message;
    }

    public bool HasFoundRing()
    {
        return ringFound;
    }

    public void CompleteQuest()
    {
        SyncStory();
    }
}