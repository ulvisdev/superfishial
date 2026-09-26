using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public class DeliveryDestination : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private DeliveryBox deliveryBox;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private Transform placementPoint;
    [SerializeField] private GameObject netVisual;
    [SerializeField] private GameObject destinationMarker;

    [Header("Marker Bob")]
    [SerializeField, Min(0f)] private float markerBobHeight = 0.12f;
    [SerializeField, Min(0f)] private float markerBobSpeed = 1f;

    [Header("Placement")]
    [SerializeField, Min(0f)] private float placementDuration = 0.4f;
    [SerializeField] private bool matchPlacementRotation = true;
    [SerializeField, Min(0f)] private float jumpHeight = 0.4f;
    [SerializeField, Min(0f)] private float bounceDuration = 0.3f;
    [SerializeField, Min(0f)] private float bounceHeight = 0.1f;
    [SerializeField, Range(1, 4)] private int bounceCount = 2;

    [Header("Completion")]
    [SerializeField] private UnityEvent onDelivered = new UnityEvent();

    public bool HasDelivered { get; private set; }

    private Vector3 markerStartPosition;
    private float markerTime;
    private Rigidbody boxBody;
    private DeliveryBoxHealth boxHealth;
    private Collider[] boxColliders;
    private bool[] colliderStates;
    private bool placing;
    private float placementTime;
    private Vector3 startPosition, endPosition;
    private Quaternion startRotation, endRotation;
    private bool IsPaused => PauseController.IsGamePaused || (playerMovement != null && !playerMovement.IsMovementEnabled);

    [SerializeField] private QuestObjectiveCompletion questObjectiveCompletion;

    private void Awake()
    {
        GetComponent<BoxCollider>().isTrigger = true;
        if (deliveryBox != null)
        {
            boxBody = deliveryBox.GetComponent<Rigidbody>();
            boxHealth = deliveryBox.GetComponent<DeliveryBoxHealth>();
        }

        if (destinationMarker != null && (destinationMarker == gameObject || transform.IsChildOf(destinationMarker.transform)))
        {
            Debug.LogWarning("Destination Marker must be a separate visual child, not this component's root or parent.", this);
            destinationMarker = null;
        }
        
        if (destinationMarker != null)
        {
            markerStartPosition = destinationMarker.transform.localPosition;
            destinationMarker.SetActive(false);
        }
    }

    private void Update()
    {
        UpdateMarker();

        if (!placing || IsPaused || boxBody == null)
            return;

        placementTime += Time.deltaTime;
        float t = placementDuration <= 0f ? 1f : Mathf.Clamp01(placementTime / placementDuration);
        float eased = Mathf.SmoothStep(0f, 1f, t);
        Vector3 position = Vector3.Lerp(startPosition, endPosition, eased);
        Quaternion rotation = Quaternion.Slerp(startRotation, endRotation, eased);

        if (t < 1f)
            position += Vector3.up * (4f * jumpHeight * t * (1f - t));
        else if (bounceDuration > 0f)
        {
            float bounce = Mathf.Clamp01((placementTime - placementDuration) / bounceDuration);
            float height = Mathf.Abs(Mathf.Sin(bounce * bounceCount * Mathf.PI));
            position += Vector3.up * (height * bounceHeight * (1f - bounce));
        }

        SetBoxPose(position, rotation);

        if (placementTime >= placementDuration + bounceDuration)
        {
            SetBoxPose(endPosition, endRotation);
            FinishDelivery();
        }
    }

    private void UpdateMarker()
    {
        if (destinationMarker == null)
            return;

        bool visible = !HasDelivered && !placing && deliveryBox != null && deliveryBox.IsCollected && boxHealth != null && !boxHealth.IsBroken;

        destinationMarker.SetActive(visible);

        if (!visible)
        {
            markerTime = 0f;
            destinationMarker.transform.localPosition = markerStartPosition;
            return;
        }

        if (IsPaused)
            return;

        markerTime += Time.deltaTime;
        float offset = Mathf.Sin(markerTime * markerBobSpeed * Mathf.PI * 2f) * markerBobHeight;
        destinationMarker.transform.localPosition = markerStartPosition + Vector3.up * offset;
    }

    private void SetBoxPose(Vector3 position, Quaternion rotation)
    {
        boxBody.position = position;
        boxBody.rotation = rotation;
        deliveryBox.transform.SetPositionAndRotation(position, rotation);
    }

    private void OnTriggerEnter(Collider other)
    {
        TryDeliver(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryDeliver(other);
    }

    private void TryDeliver(Collider other)
    {
        if (!isActiveAndEnabled || HasDelivered || placing || IsPaused)
            return;

        if (other.isTrigger || boxBody == null || other.attachedRigidbody != boxBody)
            return;

        if (deliveryBox == null || placementPoint == null || boxBody == null || boxHealth == null)
            return;

        if (!boxHealth.CompleteDelivery())
            return;

        placing = true;
        placementTime = 0f;

        if (destinationMarker != null)
            destinationMarker.SetActive(false);

        foreach (DeliveryBoxRope rope in deliveryBox.GetComponentsInChildren<DeliveryBoxRope>(true))
            rope.enabled = false;

        if (netVisual != null && netVisual != deliveryBox.gameObject && netVisual.transform.IsChildOf(deliveryBox.transform))
            netVisual.SetActive(false);

        if (!boxBody.isKinematic)
        {
            boxBody.linearVelocity = Vector3.zero;
            boxBody.angularVelocity = Vector3.zero;
        }

        boxBody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        boxBody.isKinematic = true;
        boxBody.interpolation = RigidbodyInterpolation.None;
        boxColliders = deliveryBox.GetComponentsInChildren<Collider>(true);
        colliderStates = new bool[boxColliders.Length];

        for (int i = 0; i < boxColliders.Length; i++)
        {
            colliderStates[i] = boxColliders[i].enabled;
            boxColliders[i].enabled = false;
        }

        startPosition = boxBody.position;
        startRotation = boxBody.rotation;
        endPosition = placementPoint.position;
        endRotation = matchPlacementRotation ? placementPoint.rotation : startRotation;
    }

    private void FinishDelivery()
    {
        placing = false;
        HasDelivered = true;

        if (questObjectiveCompletion != null) 
            questObjectiveCompletion.CompleteObjective();

        for (int i = 0; i < boxColliders.Length; i++)
        {
            if (boxColliders[i] != null)
                boxColliders[i].enabled = colliderStates[i];
        }

        onDelivered.Invoke();
    }
}
