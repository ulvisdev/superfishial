using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class InventoryUI : MonoBehaviour
{
    [Header("Inventory")]
    [SerializeField] private InventorySystem inventory;

    [Header("Panel")]
    [SerializeField] private GameObject inventoryPanel;
    [SerializeField] private Key toggleKey = Key.I;

    [Header("Grid")]
    [SerializeField] private Transform slotParent;
    [SerializeField] private GridLayoutGroup gridLayout;
    [SerializeField] private InventorySlotUI slotPrefab;

    [Header("Item Details")]
    [SerializeField] private TMP_Text itemNameText;
    [SerializeField] private TMP_Text itemDescriptionText;

    [SerializeField] private string emptyNameText = "";
    [SerializeField] private string emptyDescriptionText = "Hover over an item.";

    private readonly List<InventorySlotUI> slots = new();

    private bool isOpen;

    [SerializeField] private RectTransform gridRect;

    private void Awake()
    {
        isOpen = false;

        if (inventoryPanel != null)
        {
            inventoryPanel.SetActive(false);
        }

        ClearItemDetails();
    }

    private void OnEnable()
    {
        if (inventory != null)
        {
            inventory.InventoryChanged += Refresh;
        }
    }

    private void Start()
    {
        BuildGrid();
        ResizeSlotsToFit();
        Refresh();
    }

    private void OnDisable()
    {
        if (inventory != null)
        {
            inventory.InventoryChanged -= Refresh;
        }
    }

    private void Update()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current[toggleKey].wasPressedThisFrame)
        {
            ToggleInventory();
        }
    }

    public void ToggleInventory()
    {
        isOpen = !isOpen;

        if (inventoryPanel != null)
            inventoryPanel.SetActive(isOpen);

        if (isOpen)
        {
            ResizeSlotsToFit();
            Refresh();
        }
        else
        {
            ClearItemDetails();
        }
    }

    public void ShowItemDetails(ItemData item)
    {
        if (item == null)
        {
            ClearItemDetails();
            return;
        }

        if (itemNameText != null)
        {
            itemNameText.text = item.ItemName;
        }

        if (itemDescriptionText != null)
        {
            itemDescriptionText.text = item.Description;
        }
    }

    public void ClearItemDetails()
    {
        if (itemNameText != null)
        {
            itemNameText.text = emptyNameText;
        }

        if (itemDescriptionText != null)
        {
            itemDescriptionText.text = emptyDescriptionText;
        }
    }

    public void DropItem(ItemData item)
    {
        if (inventory == null || item == null)
        {
            return;
        }

        inventory.DropItem(item);
        ClearItemDetails();
    }

    private void BuildGrid()
    {
        if (inventory == null || slotParent == null || slotPrefab == null || gridLayout == null)
        {
            Debug.LogWarning("Inventory UI references are missing.");
            return;
        }

        foreach (InventorySlotUI slot in slots)
        {
            if (slot != null)
                Destroy(slot.gameObject);
        }

        slots.Clear();

        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = inventory.Columns;

        for (int i = 0; i < inventory.Capacity; i++)
        {
            InventorySlotUI newSlot = Instantiate(slotPrefab, slotParent);

            newSlot.gameObject.SetActive(true);
            newSlot.Initialize(this);
            newSlot.Display(null);

            slots.Add(newSlot);
        }
    }

    private void Refresh()
    {
        if (inventory == null)
        {
            return;
        }

        if (slots.Count != inventory.Capacity)
        {
            BuildGrid();
        }

        if (gridLayout != null)
        {
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = inventory.Columns;
        }

        for (int i = 0; i < slots.Count; i++)
        {
            InventoryStack stack = i < inventory.Items.Count ? inventory.Items[i] : null;
            slots[i].Display(stack);
        }
    }

private void ResizeSlotsToFit()
{
    if (inventory == null || gridLayout == null || gridRect == null)
        return;

    Canvas.ForceUpdateCanvases();

    float availableWidth = gridRect.rect.width - gridLayout.padding.left - gridLayout.padding.right - gridLayout.spacing.x * (inventory.Columns - 1);
    float availableHeight = gridRect.rect.height - gridLayout.padding.top - gridLayout.padding.bottom - gridLayout.spacing.y * (inventory.Rows - 1);

    float widthBasedSize = availableWidth / inventory.Columns;
    float heightBasedSize = availableHeight / inventory.Rows;

    float squareSize = Mathf.Min(widthBasedSize, heightBasedSize);
    gridLayout.cellSize = new Vector2(squareSize, squareSize);
}

}