using UnityEditor.Rendering;
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
        if (collision.CompareTag("Item"))
        {
            Item item = collision.GetComponent<Item>();
            Rigidbody rb = collision.GetComponent<Rigidbody>();
            if(item != null)
            {
                rb.useGravity = false;
                //Add item inventory
                bool itemAdded = inventoryController.AddItem(collision.gameObject);

                if (itemAdded)
                {
                    item.ShowPopUp();
                    Destroy(collision.gameObject);
                }
            }
        }
    }
}
