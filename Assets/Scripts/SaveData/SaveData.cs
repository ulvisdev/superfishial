using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SaveData
{
    public Vector3 playerPositon;
    // public string mapBoundary;
    public List<InventorySaveData> inventorySaveData;
    public List<QuestProgress> questProgressData;
    public List<String> handinQuestIDs;
}