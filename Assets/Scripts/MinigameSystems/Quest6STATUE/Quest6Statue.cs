using UnityEngine;

public class Quest6Statue : MonoBehaviour, iInteractable
{
    [Header("Statue")]
    public GameObject dirtyStatue;
    public GameObject cleanStatue;

    [Header("Required Tools")]
    public Item spongeItem;
    public Item barnacleScraperItem;

    [Header("Minigame")]
    public Quest6CleaningManager cleaningManager;

    bool cleaned;

    void Start()
    {
        dirtyStatue.SetActive(true);
        cleanStatue.SetActive(false);
    }

    public void Interact()
    {
        if (InventoryController.Instance == null)
        {
            Debug.LogWarning("InventoryController not found.");
            return;
        }

        bool hasSponge = InventoryController.Instance.HasItem(spongeItem.ID);
        bool hasScraper = InventoryController.Instance.HasItem(barnacleScraperItem.ID);

        if (!hasSponge && !hasScraper)
        {
            Debug.Log("I better get some tools to clean this.");
            return;
        }

        if (!hasSponge)
        {
            Debug.Log("I'll need something to scrub all this grime off.");
            return;
        }

        if (!hasScraper)
        {
            Debug.Log("I still need something to scrape off those barnacles.");
            return;
        }

        //Debug.Log("Both cleaning tools found! Cleaning minigame can begin.");
        cleaningManager.OpenMinigame();
    }

    public bool CanInteract()
    {
        return !cleaned;
    }

    public void SetCleaned()
    {
        cleaned = true;

        dirtyStatue.SetActive(false);
        cleanStatue.SetActive(true);
    }
}