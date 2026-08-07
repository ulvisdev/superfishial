using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Item : MonoBehaviour
{
    public int ID;
    public string itemName;
    public string itemDescription;
    public int quantity = 1;
    public bool IsQuestItem = false;


    private TMP_Text quantityText;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private ItemVisuals visuals;

    void Awake()
    {
        quantityText = GetComponentInChildren<TMP_Text>();
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }
        if (visuals == null)
        {
            visuals = GetComponent<ItemVisuals>();
        }
    }
    void Start()
    {
        UpdateQuestVisuals();
        UpdateQuantityDisplay();
    }

    public void UpdateQuantityDisplay()
    {
        if(quantityText != null)
        {
            quantityText.text = quantity > 1 ? quantity.ToString() : "";
        }
    }

    public void AddToStack(int amount = 1)
    {
        quantity += amount;
        UpdateQuantityDisplay();
    }

    public int RemoveFromStack(int amount = 1)
    {
        int removed = Mathf.Min(amount, quantity);
        quantity -= removed;
        UpdateQuantityDisplay();
        return removed;
    }

    public GameObject CloneItem(int newQuantity)
    {
        GameObject clone = Instantiate(gameObject);
        Item cloneItem = clone.GetComponent<Item>();
        cloneItem.quantity = newQuantity;
        cloneItem.UpdateQuantityDisplay();
        return clone;
    }

    public virtual void ShowPopUp()
    {
        Sprite itemIcon = GetComponent<Image>().sprite;
        if(ItemPickupUIController.Instance != null)
        {
            ItemPickupUIController.Instance.ShowItemPickup(itemName, itemIcon);
        }
    }

    private void UpdateQuestVisuals()
    {
        if (visuals == null)
            return;

        bool isQuestItem = IsQuestItem;
        visuals.SetQuestItem(isQuestItem);
    }
}
