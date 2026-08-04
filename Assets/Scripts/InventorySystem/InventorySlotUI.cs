using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventorySlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Slot UI")]
    [SerializeField] private Image itemIcon;
    [SerializeField] private TMP_Text quantityText;

    [Tooltip("Optional background behind the quantity.")]
    [SerializeField] private GameObject quantityBackground;

    private InventoryUI inventoryUI;
    private ItemData displayedItem;

    public void Initialize(InventoryUI owner)
    {
        inventoryUI = owner;
        Display(null);
    }

    public void Display(InventoryStack stack)
    {
        bool occupied = stack != null && stack.item != null && stack.quantity > 0;

        displayedItem = occupied ? stack.item : null;

        if (itemIcon != null)
        {
            itemIcon.enabled = occupied;
            itemIcon.sprite = occupied ? stack.item.Icon : null;
        }

        if (quantityText != null)
        {
            quantityText.text = occupied ? stack.quantity.ToString() : string.Empty;
        }

        if (quantityBackground != null)
        {
            quantityBackground.SetActive(occupied);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (displayedItem != null && inventoryUI != null)
            inventoryUI.ShowItemDetails(displayedItem);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (inventoryUI != null)
            inventoryUI.ClearItemDetails();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (displayedItem != null && inventoryUI != null && eventData.button == PointerEventData.InputButton.Right)
            inventoryUI.DropItem(displayedItem);
    }
}