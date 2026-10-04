using System;
using System.Collections.Generic;
using UnityEngine;

public class QuestController : MonoBehaviour
{
    public static QuestController Instance { get; private set; }
    public List<QuestProgress> activateQuests = new();
    public List<string> handinQuestIDs = new();
    public event Action OnQuestsChanged;

    private QuestUI questUI;
    private InventoryController inventory;
    private bool handingIn;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        questUI = FindFirstObjectByType<QuestUI>();
    }

    private void Start()
    {
        inventory = InventoryController.Instance;

        if (inventory != null)
            inventory.OnInventoryChanged += CheckInventoryForQuests;

        CheckInventoryForQuests();
    }

    public bool CanAcceptQuest(Quest quest)
    {
        return quest != null && !string.IsNullOrWhiteSpace(quest.questID) && !IsQuestActive(quest.questID) && !IsQuestHandedIn(quest.questID) && (quest.availability == null || quest.availability.IsMet());
    }

    public void AcceptQuest(Quest quest)
    {
        TryAcceptQuest(quest);
    }

    public bool TryAcceptQuest(Quest quest)
    {
        if (!CanAcceptQuest(quest))
            return false;

        activateQuests.Add(new QuestProgress(quest));
        StoryState.Instance.SetFlag(quest.startedFlag);
        CheckInventoryForQuests();
        Refresh();
        return true;
    }

    public bool CompleteObjective(string questID, string objectiveID)
    {
        QuestProgress progress = activateQuests.Find(q => q.QuestID == questID);

        if (progress == null)
            return false;

        QuestObjective objective = progress.objectives.Find(o => o.objectiveID == objectiveID);

        if (objective == null || objective.type == ObjectiveType.CollectItem)
            return false;

        if (objective.IsCompleted)
            return true;

        objective.currentAmount = objective.requiredAmount;
        Refresh();
        return true;
    }

    public bool IsQuestActive(string questID)
    {
        return activateQuests.Exists(q => q.QuestID == questID);
    }

    public bool IsQuestCompleted(string questID)
    {
        QuestProgress quest = activateQuests.Find(q => q.QuestID == questID);
        return quest != null && quest.IsCompleted;
    }

    public bool IsQuestHandedIn(string questID)
    {
        return handinQuestIDs.Contains(questID);
    }

    public void CheckInventoryForQuests()
    {
        if (handingIn || InventoryController.Instance == null)
            return;

        Dictionary<int, int> counts = InventoryController.Instance.GetItemCounts();

        foreach (QuestProgress quest in activateQuests)
            foreach (QuestObjective objective in quest.objectives)
            {
                if (objective.type != ObjectiveType.CollectItem || !int.TryParse(objective.objectiveID, out int itemID))
                    continue;

                objective.currentAmount = counts.TryGetValue(itemID, out int count) ? Mathf.Min(count, objective.requiredAmount) : 0;
            }

        Refresh();
    }

    public void HandInQuest(string questID)
    {
        TryHandInQuest(questID);
    }

    public bool TryHandInQuest(string questID)
    {
        if (handingIn || IsQuestHandedIn(questID) || !IsQuestCompleted(questID))
            return false;

        QuestProgress progress = activateQuests.Find(q => q.QuestID == questID);

        if (progress.quest.questRewards != null && progress.quest.questRewards.Count > 0 && RewardsController.Instance == null)
        {
            Debug.LogError("Quest hand-in requires RewardsController.", this);
            return false;
        }

        handingIn = true;

        try
        {
            if (!RemoveRequiredItemsFromInventory(questID))
                return false;

            activateQuests.Remove(progress);
            handinQuestIDs.Add(questID);
            StoryState.Instance.SetFlag(progress.quest.handedInFlag);

            if (RewardsController.Instance != null)
                RewardsController.Instance.GiveQuestReward(progress.quest);
        }
        finally
        {
            handingIn = false;
            CheckInventoryForQuests();
        }

        return true;
    }

    public bool RemoveRequiredItemsFromInventory(string questID)
    {
        QuestProgress progress = activateQuests.Find(q => q.QuestID == questID);

        if (progress == null || InventoryController.Instance == null)
            return false;

        Dictionary<int, int> required = new();

        foreach (QuestObjective objective in progress.objectives)
            if (objective.type == ObjectiveType.CollectItem && int.TryParse(objective.objectiveID, out int itemID))
                required[itemID] = required.GetValueOrDefault(itemID) + objective.requiredAmount;

        foreach (var item in required)
            if (!InventoryController.Instance.HasItem(item.Key, item.Value))
                return false;

        foreach (var item in required)
            InventoryController.Instance.RemoveItemsFromInventory(item.Key, item.Value);

        return true;
    }

    public void LoadQuestProgress(List<QuestProgress> savedQuests)
    {
        activateQuests = savedQuests ?? new();
        CheckInventoryForQuests();
    }

    public void Refresh()
    {
        if (questUI != null)
            questUI.UpdateQuestUI();

        OnQuestsChanged?.Invoke();
        StoryState.NotifyChanged();
    }

    private void OnDestroy()
    {
        if (inventory != null)
            inventory.OnInventoryChanged -= CheckInventoryForQuests;

        if (Instance == this)
            Instance = null;
    }
}
