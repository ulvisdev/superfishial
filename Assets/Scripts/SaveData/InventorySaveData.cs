using UnityEngine;

[System.Serializable]
public class InventorySaveData 
{
    public int ItemID;
    public int slotIndex; //The index of the slot where the item is placed within the inventory
    public int quantity = 1;
}
