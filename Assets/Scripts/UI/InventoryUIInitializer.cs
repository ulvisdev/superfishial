using System.Collections;
using UnityEngine;

public class InventoryStartupInitializer : MonoBehaviour
{
    [SerializeField] private GameObject menu;
    [SerializeField] private GameObject inventoryPage;
    [SerializeField] private CanvasGroup menuCanvasGroup;

    [SerializeField] private float startupDelay = 0.25f;

    private IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(startupDelay);

        menuCanvasGroup.alpha = 0f;
        menuCanvasGroup.interactable = false;
        menuCanvasGroup.blocksRaycasts = false;

        menu.SetActive(true);
        inventoryPage.SetActive(true);

        yield return null;
        yield return new WaitForEndOfFrame();

        Canvas.ForceUpdateCanvases();

        inventoryPage.SetActive(false);
        menu.SetActive(false);

        menuCanvasGroup.alpha = 1f;
        menuCanvasGroup.interactable = true;
        menuCanvasGroup.blocksRaycasts = true;

        Debug.Log("Inventory UI initialized.");
    }
}