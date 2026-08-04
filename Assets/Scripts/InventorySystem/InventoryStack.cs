using System;
using UnityEngine;

[Serializable]
public class InventoryStack
{
    public ItemData item;

    [Min(1)]
    public int quantity;

    public InventoryStack(ItemData item, int quantity)
    {
        this.item = item;
        this.quantity = quantity;
    }
}