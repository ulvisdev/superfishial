using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class MenuController : MonoBehaviour
{
    public GameObject menuCanvas;
    public TMP_Text itemNameText;
    public TMP_Text itemDescText;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        menuCanvas.SetActive(false);
    }

    public void ToggleMenu(InputAction.CallbackContext context)
    {
        if (!context.performed)
            return;

        if (!menuCanvas.activeSelf && PauseController.IsGamePaused)
            return;

        bool openingMenu = !menuCanvas.activeSelf;

        if (openingMenu)
            PlayerFreeze.Instance.FreezePlayer();
        else
            PlayerFreeze.Instance.UnfreezePlayer();

        menuCanvas.SetActive(openingMenu);
        PauseController.SetPause(openingMenu);
    }

    public void ShowItemDetails(string itemName, string itemDesc)
    {
        if (itemName == null || itemDesc == null)
        {
            ClearItemDetails();
            return;
        }
        if (itemNameText != null)
        {
            itemNameText.text = itemName;
        }

        if (itemDescText != null)
        {
            itemDescText.text = itemDesc;
        }
    }

    public void ClearItemDetails()
    {
        if (itemNameText != null)
        {
            itemNameText.text = "Hover over an item";
        }

        if (itemDescText != null)
        {
            itemDescText.text = "for more information on that item.";
        }
    }

}
