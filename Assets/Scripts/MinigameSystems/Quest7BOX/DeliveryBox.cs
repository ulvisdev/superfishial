using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
public class DeliveryBox : MonoBehaviour, iInteractable
{
    [Header("References")]
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private Rigidbody playerBody;

    [Header("Towing")]
    [Tooltip("The tether stays slack inside this distance. Pulling starts beyond it.")]
    [SerializeField, Min(0.1f)] private float followDistance = 1.8f;
    [SerializeField, Min(0f)] private float followStrength = 8f;
    [SerializeField, Min(0f)] private float velocityDamping = 4f;
    [SerializeField, Min(0.1f)] private float maximumAcceleration = 18f;
    [SerializeField, Min(0f)] private float waterDrag = 0.8f;

    [Header("Floating")]
    [SerializeField, Min(0f)] private float bobHeight = 0.12f;
    [SerializeField, Min(0f)] private float bobFrequency = 0.45f;

    [Header("Tether")]
    [SerializeField, Min(0.1f)] private float maximumTetherLength = 3.2f;

    [Header("Events")]
    [SerializeField] private UnityEvent onCollected = new UnityEvent();

    public bool IsCollected { get; private set; }

    private Rigidbody body;
    private ConfigurableJoint tether;
    private Vector3 idlePosition;
    private float bobTime;
    private bool suspended;

    private bool ShouldSuspend => PauseController.IsGamePaused || (playerMovement != null && !playerMovement.IsMovementEnabled);

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        if (playerBody == null && playerMovement != null) playerBody = playerMovement.GetComponent<Rigidbody>();
        if (playerMovement == null && playerBody != null) playerMovement = playerBody.GetComponent<PlayerMovement>();

        body.isKinematic = false;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.constraints = RigidbodyConstraints.FreezeRotation;
        body.linearDamping = 0f;
        body.solverIterations = Mathf.Max(body.solverIterations, 12);
        body.solverVelocityIterations = Mathf.Max(body.solverVelocityIterations, 4);
        idlePosition = body.position;
    }

    public bool CanInteract()
    {
        return isActiveAndEnabled && !IsCollected && playerBody != null && playerMovement != null && !ShouldSuspend;
    }

    public void Interact()
    {
        if (!CanInteract()) return;

        CreateTether();
        IsCollected = true;
        onCollected.Invoke();
    }

    private void CreateTether()
    {
        tether = gameObject.AddComponent<ConfigurableJoint>();
        tether.autoConfigureConnectedAnchor = false;
        tether.connectedBody = playerBody;
        tether.anchor = Vector3.zero;
        tether.connectedAnchor = Vector3.zero;
        tether.xMotion = ConfigurableJointMotion.Limited;
        tether.yMotion = ConfigurableJointMotion.Limited;
        tether.zMotion = ConfigurableJointMotion.Limited;
        tether.angularXMotion = ConfigurableJointMotion.Free;
        tether.angularYMotion = ConfigurableJointMotion.Free;
        tether.angularZMotion = ConfigurableJointMotion.Free;
        tether.enableCollision = true;
        tether.projectionMode = JointProjectionMode.None;

        float length = Mathf.Max(maximumTetherLength, Mathf.Max(followDistance + 0.25f, Vector3.Distance(body.position, playerBody.position)));
        tether.linearLimit = new SoftJointLimit { limit = length, bounciness = 0f, contactDistance = 0.05f };
    }

    private void Update()
    {
        UpdateSuspension();
    }

    private void FixedUpdate()
    {
        UpdateSuspension();
        if (suspended) return;

        bobTime += Time.fixedDeltaTime;
        if (IsCollected && playerBody == null) Release();

        float bobSpeed = bobHeight * bobFrequency * Mathf.PI * 2f * Mathf.Cos(bobTime * bobFrequency * Mathf.PI * 2f);
        Vector3 acceleration;

        if (IsCollected && playerBody != null)
        {
            acceleration = (Vector3.up * bobSpeed - body.linearVelocity) * waterDrag;
            Vector3 toPlayer = playerBody.position - body.position;
            float distance = toPlayer.magnitude;

            if (distance > followDistance)
            {
                Vector3 pullDirection = toPlayer / distance;
                float separatingSpeed = Vector3.Dot(playerBody.linearVelocity - body.linearVelocity, pullDirection);
                float tension = Mathf.Max(0f, (distance - followDistance) * followStrength + separatingSpeed * velocityDamping);
                acceleration += pullDirection * tension;
            }
        }
        else
        {
            Vector3 targetPosition = idlePosition + Vector3.up * (Mathf.Sin(bobTime * bobFrequency * Mathf.PI * 2f) * bobHeight);
            acceleration = (targetPosition - body.position) * followStrength - body.linearVelocity * velocityDamping;
        }

        body.AddForce(Vector3.ClampMagnitude(acceleration, maximumAcceleration), ForceMode.Acceleration);
    }

    private void UpdateSuspension()
    {
        bool freeze = ShouldSuspend;
        if (freeze == suspended) return;

        suspended = freeze;

        if (suspended)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.isKinematic = true;
        }
        else
        {
            body.isKinematic = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.WakeUp();
        }
    }

    public void Release()
    {
        if (tether != null) Destroy(tether);
        tether = null;
        IsCollected = false;
        idlePosition = body.position;
        bobTime = 0f;
    }

    private void OnDisable()
    {
        if (body == null) return;
        Release();

        if (!suspended) return;
        suspended = false;
        body.isKinematic = false;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }
}
