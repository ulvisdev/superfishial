// using UnityEditor.Rendering;
using UnityEngine;

public class PlayerItemCollector : MonoBehaviour
{
    private InventoryController inventoryController;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    [System.Obsolete]
    void Start()
    {
        inventoryController = FindObjectOfType<InventoryController>();
    }
    private void OnTriggerEnter(Collider collision)
    {
        if (!collision.CompareTag("Item") || !collision.gameObject.activeInHierarchy)
            return;

        if (StoryState.Instance == null || !StoryState.Instance.IsReady || PauseController.IsGamePaused)
            return;

        Item item = collision.GetComponent<Item>();

        if (item == null)
            return;

        if (!string.IsNullOrWhiteSpace(item.collectedFlag) && StoryState.Instance.HasFlag(item.collectedFlag))
            return;

        if (inventoryController == null)
            inventoryController = InventoryController.Instance;

        if (inventoryController == null || !inventoryController.AddItem(collision.gameObject))
            return;

        item.ShowPopUp();

        if (!string.IsNullOrWhiteSpace(item.collectedFlag))
            StoryState.Instance.SetFlag(item.collectedFlag);

        collision.gameObject.SetActive(false);
        Destroy(collision.gameObject);
    }
}
