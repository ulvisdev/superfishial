using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(StoryState))]
public class SaveController : MonoBehaviour
{
    public static SaveController Instance { get; private set; }

    [Header("References")]
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private Quest[] questCatalog;

    [Header("Saving")]
    [SerializeField] private bool loadOnStart = true;
    [SerializeField] private float autosaveSeconds = 15f;

    public bool IsReady { get; private set; }
    public bool IsLoading { get; private set; }
    public bool HasSave => File.Exists(saveLocation);

    private string saveLocation;
    private InventoryController inventory;
    private float nextAutosave;
    private bool saveBlocked;
    private readonly Dictionary<string, Quest> questsByID = new();

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void SuperfishialSyncSave();
#endif

    private void OnEnable()
    {
        StoryState.Changed += RequestSave;
    }

    private void OnDisable()
    {
        StoryState.Changed -= RequestSave;
    }

    public void RequestSave()
    {
        if (IsReady && !IsLoading && autosaveSeconds > 0f)
            nextAutosave = Mathf.Min(nextAutosave, Time.unscaledTime + 0.3f);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        saveLocation = Path.Combine(Application.persistentDataPath, "storySave_v2.json");
    }

    private IEnumerator Start()
    {
        StoryState.Instance.SetReady(false);
        yield return null;
        inventory = InventoryController.Instance;

        if (playerMovement == null)
            playerMovement = FindFirstObjectByType<PlayerMovement>();

        if (inventory == null || playerMovement == null || QuestController.Instance == null || !BuildCatalog())
        {
            Debug.LogError("Save setup is incomplete. Assign Player Movement and every quest to Quest Catalog.", this);
            yield break;
        }

        IsReady = true;

        if (loadOnStart && HasSave)
            LoadGame();
        else
        {
            saveBlocked = !loadOnStart && HasSave;
            inventory.SetInventoryItems(new List<InventorySaveData>());
            StoryState.Instance.SetReady(true);
            nextAutosave = Time.unscaledTime + Mathf.Max(1f, autosaveSeconds);

            if (!HasSave)
                SaveGame();
        }
    }

    private bool BuildCatalog()
    {
        questsByID.Clear();

        if (questCatalog == null || questCatalog.Length == 0)
            return false;

        foreach (Quest quest in questCatalog)
        {
            if (quest == null || string.IsNullOrWhiteSpace(quest.questID) || !questsByID.TryAdd(quest.questID, quest))
                return false;

            HashSet<string> objectiveIDs = new();

            if (quest.objectives == null)
                return false;

            foreach (QuestObjective objective in quest.objectives)
                if (objective == null || string.IsNullOrWhiteSpace(objective.objectiveID) || objective.requiredAmount < 1 || !objectiveIDs.Add(objective.objectiveID))
                    return false;
        }

        return true;
    }

    private void Update()
    {
        if (!IsReady || IsLoading || autosaveSeconds <= 0f || Time.unscaledTime < nextAutosave)
            return;

        if (!CanSave())
            return;

        SaveGame();
    }

    private bool CanSave()
    {
        return inventory != null && playerMovement != null && QuestController.Instance != null && StoryState.Instance != null && IsReady && !IsLoading && !saveBlocked && StoryState.Instance.IsReady && NPC.ActiveNPC == null && !PauseController.IsGamePaused && playerMovement.IsMovementEnabled && (PlayerFreeze.Instance == null || !PlayerFreeze.Instance.IsFrozen);
    }

    public void SaveGame()
    {
        if (!CanSave())
            return;

        nextAutosave = Time.unscaledTime + Mathf.Max(1f, autosaveSeconds);
        SaveData data = new SaveData
        {
            sceneName = SceneManager.GetActiveScene().name,
            playerPositon = playerMovement.transform.position,
            inventorySaveData = inventory.GetInventoryItems(),
            handinQuestIDs = new List<string>(QuestController.Instance.handinQuestIDs),
            storyFlags = StoryState.Instance.GetFlags()
        };

        foreach (QuestProgress progress in QuestController.Instance.activateQuests)
        {
            if (!questsByID.ContainsKey(progress.QuestID))
            {
                Debug.LogError("Cannot save a quest missing from Quest Catalog: " + progress.QuestID, this);
                return;
            }

            QuestSaveEntry entry = new QuestSaveEntry { questID = progress.QuestID };

            foreach (QuestObjective objective in progress.objectives)
                entry.objectives.Add(new ObjectiveSaveEntry { objectiveID = objective.objectiveID, currentAmount = objective.currentAmount });

            data.quests.Add(entry);
        }

        try
        {
            Directory.CreateDirectory(Application.persistentDataPath);
            string temporary = saveLocation + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(data, true));

            if (File.Exists(saveLocation))
                File.Copy(saveLocation, saveLocation + ".bak", true);

            File.Copy(temporary, saveLocation, true);
            File.Delete(temporary);
#if UNITY_WEBGL && !UNITY_EDITOR
            SuperfishialSyncSave();
#endif
        }
        catch (Exception exception)
        {
            Debug.LogError("Save failed: " + exception.Message, this);
        }
    }

    public void LoadGame()
    {
        if (!IsReady || IsLoading || NPC.ActiveNPC != null || PauseController.IsGamePaused || (PlayerFreeze.Instance != null && PlayerFreeze.Instance.IsFrozen))
            return;

        if (!HasSave)
            return;

        try
        {
            SaveData data = JsonUtility.FromJson<SaveData>(File.ReadAllText(saveLocation));
            List<QuestProgress> restored = ValidateAndRestore(data);
            saveBlocked = false;
            StartCoroutine(ApplySave(data, restored));
        }
        catch (Exception exception)
        {
            saveBlocked = true;
            StoryState.Instance.SetReady(true);
            Debug.LogError("Load failed; autosaving is blocked to protect the save. " + exception.Message, this);
        }
    }

    private List<QuestProgress> ValidateAndRestore(SaveData data)
    {
        if (data == null || data.version != 2 || data.sceneName != SceneManager.GetActiveScene().name)
            throw new InvalidDataException("This save requires its original scene and save version.");

        if (data.quests == null || data.inventorySaveData == null || data.handinQuestIDs == null || data.storyFlags == null)
            throw new InvalidDataException("Save lists are missing.");

        if (float.IsNaN(data.playerPositon.x) || float.IsNaN(data.playerPositon.y) || float.IsNaN(data.playerPositon.z) || float.IsInfinity(data.playerPositon.x) || float.IsInfinity(data.playerPositon.y) || float.IsInfinity(data.playerPositon.z))
            throw new InvalidDataException("The saved position is invalid.");

        ItemDictionary items = FindFirstObjectByType<ItemDictionary>();
        HashSet<int> occupiedSlots = new();

        foreach (InventorySaveData item in data.inventorySaveData)
            if (item == null || item.slotIndex < 0 || item.slotIndex >= inventory.slotCount || item.quantity < 1 || !occupiedSlots.Add(item.slotIndex) || items == null || items.GetItemPrefab(item.ItemID) == null)
                throw new InvalidDataException("An inventory item or slot could not be restored.");

        List<QuestProgress> restored = new();
        HashSet<string> usedIDs = new();

        foreach (string questID in data.handinQuestIDs)
            if (!questsByID.ContainsKey(questID) || !usedIDs.Add(questID))
                throw new InvalidDataException("A handed-in quest is missing or duplicated: " + questID);

        foreach (QuestSaveEntry entry in data.quests)
        {
            if (entry == null || entry.questID == null || !questsByID.TryGetValue(entry.questID, out Quest quest) || !usedIDs.Add(entry.questID) || entry.objectives == null)
                throw new InvalidDataException("An active quest is missing or duplicated in Quest Catalog.");

            QuestProgress progress = new QuestProgress(quest);
            HashSet<string> objectiveIDs = new();

            foreach (ObjectiveSaveEntry saved in entry.objectives)
            {
                QuestObjective objective = saved == null ? null : progress.objectives.Find(o => o.objectiveID == saved.objectiveID);

                if (objective == null || !objectiveIDs.Add(saved.objectiveID))
                    throw new InvalidDataException("A saved objective no longer matches its quest.");

                objective.currentAmount = Mathf.Clamp(saved.currentAmount, 0, objective.requiredAmount);
            }

            restored.Add(progress);
        }

        return restored;
    }

    private IEnumerator ApplySave(SaveData data, List<QuestProgress> restored)
    {
        IsLoading = true;
        StoryState.Instance.SetReady(false);
        bool movementEnabled = playerMovement.IsMovementEnabled;
        playerMovement.SetMovementEnabled(false);
        QuestController.Instance.activateQuests = new();
        QuestController.Instance.handinQuestIDs = new List<string>(data.handinQuestIDs);
        StoryState.Instance.RestoreFlags(data.storyFlags);
        inventory.SetInventoryItems(data.inventorySaveData);
        yield return null;
        QuestController.Instance.LoadQuestProgress(restored);
        playerMovement.TeleportTo(data.playerPositon);
        playerMovement.SetMovementEnabled(movementEnabled);
        IsLoading = false;
        StoryState.Instance.SetReady(true);
        nextAutosave = Time.unscaledTime + Mathf.Max(1f, autosaveSeconds);
    }

    public void StartNewGame()
    {
        if (!IsReady || IsLoading || NPC.ActiveNPC != null || PauseController.IsGamePaused || (PlayerFreeze.Instance != null && PlayerFreeze.Instance.IsFrozen))
            return;

        try
        {
            if (HasSave)
            {
                File.Copy(saveLocation, saveLocation + ".before-new-game", true);
                File.Delete(saveLocation);
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            SuperfishialSyncSave();
#endif
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
        catch (Exception exception)
        {
            Debug.LogError("Could not start a new game: " + exception.Message, this);
        }
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
            SaveGame();
    }

    private void OnApplicationQuit()
    {
        SaveGame();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
