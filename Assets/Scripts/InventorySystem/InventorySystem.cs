using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class InventorySystem : MonoBehaviour
{
    [Header("Inventory Size")]
    [SerializeField, Min(1)] private int rows = 4;
    [SerializeField, Min(1)] private int columns = 5;

    [Header("Dropping")]
    [SerializeField] private Transform dropPoint;
    [SerializeField] private bool dropWholeStackOnRightClick;

    private readonly List<InventoryStack> items = new();

    public event Action InventoryChanged;

    public int Rows => rows;
    public int Columns => columns;
    public int Capacity => rows * columns;
    public IReadOnlyList<InventoryStack> Items => items;

    public bool AddItem(ItemData item, int quantity = 1)
    {
        if (item == null || quantity <= 0)
        {
            return false;
        }

        int existingIndex = items.FindIndex(stack => stack.item != null && stack.item.Id == item.Id);

        if (existingIndex >= 0)
        {
            items[existingIndex].quantity += quantity;
            InventoryChanged?.Invoke();
            return true;
        }

        if (items.Count >= Capacity)
        {
            Debug.Log("Inventory is full.");
            return false;
        }

        items.Add(new InventoryStack(item, quantity));
        InventoryChanged?.Invoke();

        return true;
    }

    public bool DropItem(ItemData item)
    {
        if (item == null)
        {
            return false;
        }

        int stackIndex = items.FindIndex(stack => stack.item != null && stack.item.Id == item.Id);

        if (stackIndex < 0)
        {
            return false;
        }

        InventoryStack stack = items[stackIndex];

        if (item.WorldPrefab == null)
        {
            Debug.LogWarning($"Item '{item.ItemName}' has no world prefab.");
            return false;
        }

        int amountToDrop = dropWholeStackOnRightClick ? stack.quantity : 1;
        Vector3 spawnPosition = dropPoint != null ? dropPoint.position : transform.position;
        GameObject droppedObject = Instantiate(item.WorldPrefab, spawnPosition, Quaternion.identity);

        WorldItem worldItem = droppedObject.GetComponent<WorldItem>();

        if (worldItem == null)
        {
            worldItem = droppedObject.GetComponentInChildren<WorldItem>();
        }

        if (worldItem == null)
        {
            Debug.LogWarning($"The world prefab for '{item.ItemName}' needs a WorldItem component.");
            Destroy(droppedObject);
            return false;
        }

        worldItem.Initialize(item, amountToDrop, true);
        stack.quantity -= amountToDrop;
        if (stack.quantity <= 0)
        {
            items.RemoveAt(stackIndex);
        }

        InventoryChanged?.Invoke();
        return true;
    }

    public int GetItemCount(ItemData item)
    {
        if (item == null)
        {
            return 0;
        }

        InventoryStack stack = items.Find(stack => stack.item != null && stack.item.Id == item.Id);
        return stack != null ? stack.quantity : 0;
    }

    public bool HasItem(ItemData item, int requiredQuantity = 1)
    {
        return GetItemCount(item) >= requiredQuantity;
    }

    private void OnValidate()
    {
        rows = Mathf.Max(1, rows);
        columns = Mathf.Max(1, columns);
    }
}