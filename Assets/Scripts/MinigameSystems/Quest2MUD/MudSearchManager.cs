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
    [SerializeField] private bool startQuestAutomaticallyForTesting = true;

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

    void Start()
    {
        if (searchPanel != null)
            searchPanel.SetActive(false);

        if (instructionsPanel != null)
            instructionsPanel.SetActive(false);

        if (startQuestAutomaticallyForTesting)
            BeginQuest();
    }

    public void BeginQuest()
    {
        if (questActive)
            return;

        questActive = true;

        ChooseRingLocation();

        Debug.Log("Mud quest started. Ring is hidden in spot: " + ringSpotID);
    }

    private void ChooseRingLocation()
    {
        List<MudSearchSpot> validSpots = new List<MudSearchSpot>();

        foreach (MudSearchSpot spot in searchSpots)
            if (spot != null)
                validSpots.Add(spot);

        if (validSpots.Count == 0)
        {
            Debug.LogError("MudSearchManager has no search spots!");
            return;
        }

        int randomIndex = UnityEngine.Random.Range(0, validSpots.Count);

        ringSpotID = validSpots[randomIndex].SpotID;
    }

    public bool CanSearch(MudSearchSpot spot)
    {
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
        if (!CanSearch(spot))
            return;

        currentSpot = spot;
        panelOpen = true;

        PlayerFreeze.Instance.FreezePlayer();

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
        int patchCount = UnityEngine.Random.Range(minimumPatchCount, maximumPatchCount + 1);

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

        if (shouldContainJunk && junkOptions.Length > 0)
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
            StartCoroutine(FinishSpotAfterDelay());
    }

    private void FindRing()
    {
        if (ringFound)
            return;

        bool addedToInventory = InventoryController.Instance.AddItem(ringInventoryPrefab);

        if (!addedToInventory)
        {
            SetStatus("You found the ring, but your inventory is full!");
            return;
        }

        ringFound = true;
        closingAfterRing = true;

        Item ringItem = ringInventoryPrefab.GetComponent<Item>();

        if (ringItem != null)
            ringItem.ShowPopUp();

        SetStatus("You found the wedding ring!");
        SoundEffectManager.Play("MudRing");

        Debug.Log("Wedding ring found!");

        StartCoroutine(CloseAfterFindingRing());

        // Later:
        // DialogueState.Instance.SetFlag("ring_found");
    }

    private IEnumerator CloseAfterFindingRing()
    {
        yield return new WaitForSeconds(ringFoundCloseDelay);
        CloseSearch();
    }

    private IEnumerator FinishSpotAfterDelay()
    {
        yield return new WaitForSeconds(0.6f);

        if (currentSpot != null)
            currentSpot.ClearSpot();

        CloseSearch();
    }

    private void CloseSearch()
    {
        searchPanel.SetActive(false);
        instructionsPanel.SetActive(false);

        ClearOldPatches();

        panelOpen = false;
        closingAfterRing = false;
        currentSpot = null;

        PlayerFreeze.Instance.UnfreezePlayer();
    }

    private void ClearOldPatches()
    {
        foreach (Transform child in searchArea)
            Destroy(child.gameObject);
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
        questActive = false;
    }
}