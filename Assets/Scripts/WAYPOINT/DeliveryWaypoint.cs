using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(WaypointTarget))]
public class DeliveryWaypoint : MonoBehaviour
{
    [SerializeField] private DeliveryBox deliveryBox;
    [SerializeField] private DeliveryBoxHealth boxHealth;

    private WaypointTarget waypoint;
    private bool wasVisible;

    private void Awake()
    {
        waypoint = GetComponent<WaypointTarget>();
        
        if (boxHealth == null && deliveryBox != null)
            boxHealth = deliveryBox.GetComponent<DeliveryBoxHealth>();

        waypoint.Hide();
    }

    private void Update()
    {
        bool visible = deliveryBox != null && boxHealth != null && deliveryBox.IsCollected && !boxHealth.IsBroken && !boxHealth.IsDelivered;
        
        if (visible == wasVisible)
            return;

        wasVisible = visible;

        if (visible)
            waypoint.Show();
        else
            waypoint.Hide();
    }

    private void OnDisable()
    {
        wasVisible = false;

        if (waypoint != null)
            waypoint.Hide();

    }
}
