using System.Collections.Generic;
using UnityEngine;

public class WorldItem : MonoBehaviour
{
    [Header("Item")]
    [SerializeField] private ItemData itemData;
    [SerializeField, Min(1)] private int quantity = 1;

    [Header("Visual")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private WorldItemVisuals visuals;

    private readonly HashSet<Collider> playerCollidersInside = new();

    private bool waitForPlayerExit;
    private bool collected;

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (visuals == null)
            visuals = GetComponent<WorldItemVisuals>();

        RefreshVisual();
    }

    private void Start()
    {
        UpdateQuestVisuals();
    }

    public void Initialize(ItemData newItemData, int newQuantity, bool requirePlayerExit)
    {
        itemData = newItemData;
        quantity = Mathf.Max(1, newQuantity);
        waitForPlayerExit = requirePlayerExit;
        collected = false;

        playerCollidersInside.Clear();

        RefreshVisual();
        UpdateQuestVisuals();
    }

    private void OnTriggerEnter(Collider other)
    {
        InventorySystem inventory = other.GetComponentInParent<InventorySystem>();

        if (inventory == null)
            return;

        playerCollidersInside.Add(other);

        if (waitForPlayerExit)
            return;

        TryCollect(inventory);
    }

    private void OnTriggerStay(Collider other)
    {
        if (!waitForPlayerExit)
            return;

        InventorySystem inventory = other.GetComponentInParent<InventorySystem>();

        if (inventory != null)
            playerCollidersInside.Add(other);
    }

    private void OnTriggerExit(Collider other)
    {
        InventorySystem inventory = other.GetComponentInParent<InventorySystem>();

        if (inventory == null)
            return;

        playerCollidersInside.Remove(other);

        if (waitForPlayerExit && playerCollidersInside.Count == 0)
            waitForPlayerExit = false;
    }

    private void TryCollect(InventorySystem inventory)
    {
        if (collected || itemData == null)
            return;

        if (inventory.AddItem(itemData, quantity))
        {
            collected = true;
            Destroy(gameObject);
        }
    }

    private void RefreshVisual()
    {
        if (spriteRenderer != null && itemData != null)
            spriteRenderer.sprite = itemData.Icon;
    }

    private void UpdateQuestVisuals()
    {
        if (visuals == null)
            return;

        bool isQuestItem = itemData != null && itemData.IsQuestItem;
        visuals.SetQuestItem(isQuestItem);
    }

    private void OnValidate()
    {
        quantity = Mathf.Max(1, quantity);

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        RefreshVisual();
    }
}