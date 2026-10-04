using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SaveData
{
    public int version = 2;
    public string sceneName;
    public string roomID;
    public Vector3 playerPositon;
    public List<InventorySaveData> inventorySaveData = new();
    public List<QuestSaveEntry> quests = new();
    public List<string> handinQuestIDs = new();
    public List<string> storyFlags = new();
}

[Serializable]
public class QuestSaveEntry
{
    public string questID;
    public List<ObjectiveSaveEntry> objectives = new();
}

[Serializable]
public class ObjectiveSaveEntry
{
    public string objectiveID;
    public int currentAmount;
}
