using UnityEngine;

[CreateAssetMenu(fileName = "New Item", menuName = "Inventory/Item")]
public class ItemData : ScriptableObject
{
    [Header("Item Information")]
    [SerializeField] private string itemId = "item_id";
    [SerializeField] private string itemName = "New Item";

    [TextArea(3, 8)]
    [SerializeField] private string description;

    [Header("Visuals")]
    [SerializeField] private Sprite icon;

    [SerializeField] private GameObject worldPrefab;

    public string Id => itemId;
    public string ItemName => itemName;
    public string Description => description;
    public Sprite Icon => icon;
    public GameObject WorldPrefab => worldPrefab;

    private void OnValidate()
    {
        itemId = itemId.Trim();

        if (string.IsNullOrEmpty(itemId))
        {
            itemId = name.Trim().ToLowerInvariant().Replace(" ", "_");
        }
    }
}