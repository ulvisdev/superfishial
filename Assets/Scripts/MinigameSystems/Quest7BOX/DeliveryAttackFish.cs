using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
public class DeliveryAttackFish : MonoBehaviour
{
    private static readonly List<DeliveryAttackFish> activeFish = new List<DeliveryAttackFish>();

    private enum State { Roam, ReturnHome, Pursue, Windup, Charge, Recover, Retreat }

    [Header("References")]
    [SerializeField] private DeliveryBox deliveryBox;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private Transform shakeRoot;
    [SerializeField] private SpriteRenderer fishSprite;
    [SerializeField] private Camera worldCamera;
    [SerializeField] private bool spriteFacesRight = true;

    [Header("Roaming")]
    [SerializeField] private Transform initialRoamCenter;
    [SerializeField] private bool roamWhereChaseEnds = true;
    [SerializeField, Min(0.1f)] private float roamRadius = 3f;
    [SerializeField, Min(0.1f)] private float roamSpeed = 1.2f;

    [Header("Pursuit")]
    [SerializeField, Min(0.1f)] private float detectionRadius = 6f;
    [SerializeField, Min(0.1f)] private float pursuitSpeed = 3.5f;
    [SerializeField, Min(0.1f)] private float chargeRange = 4f;
    [SerializeField, Min(0.1f)] private float loseDistance = 20f;
    [SerializeField, Min(0f)] private float loseDelay = 2f;
    [SerializeField, Min(0f)] private float reacquireDelay = 3f;

    [Header("Charge")]
    [SerializeField, Min(0.05f)] private float windupDuration = 0.4f;
    [SerializeField, Min(0.1f)] private float chargeSpeed = 5f;
    [SerializeField, Min(0.1f)] private float acceleration = 25f;
    [SerializeField, Min(0.1f)] private float chargeDuration = 1.5f;
    [SerializeField, Min(0.05f)] private float missRecovery = 0.35f;

    [Header("Hit Recovery")]
    [SerializeField, Min(0.05f)] private float hitRecovery = 0.7f;
    [SerializeField, Min(0f)] private float shakeAmount = 0.035f;
    [SerializeField, Min(0.05f)] private float retreatDuration = 0.45f;
    [SerializeField, Min(0.1f)] private float retreatSpeed = 2f;

    private Rigidbody body;
    private Rigidbody boxBody;
    private Rigidbody playerBody;
    private Collider[] fishColliders;
    private Collider[] playerColliders;
    private PlayerMovement boundPlayer;
    private float referenceRetry;
    private DeliveryBoxHealth boxHealth;
    private State state;
    private Vector3 roamCenter;
    private Vector3 originalRoamCenter;
    private Vector3 roamTarget;
    private float roamTimer;
    private float roamWait;
    private float separationTime;
    private float reacquireTimer;
    private Vector3 visualPosition;
    private Vector3 chargeDirection;
    private Vector3 savedVelocity;
    private float timer;
    private bool hitSomething;
    private bool suspended;

    private bool IsPaused => PauseController.IsGamePaused || (playerMovement != null && !playerMovement.IsMovementEnabled);
    private bool TargetAvailable => deliveryBox != null && boxHealth != null && boxBody != null && deliveryBox.IsCollected && !boxHealth.IsBroken && !boxHealth.IsDelivered;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.useGravity = false;
        body.isKinematic = false;
        body.constraints = RigidbodyConstraints.FreezeRotation;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.linearDamping = 0f;
        GetComponent<SphereCollider>().isTrigger = false;

        if (worldCamera == null)
            worldCamera = Camera.main;

        if (shakeRoot != null && (shakeRoot == transform || !shakeRoot.IsChildOf(transform)))
        {
            Debug.LogWarning("Fish Shake Root must be a visual child of the fish.", this);
            shakeRoot = null;
        }

        if (shakeRoot != null)
            visualPosition = shakeRoot.localPosition;

        fishColliders = GetComponentsInChildren<Collider>(true);
    }

    private void OnEnable()
    {
        if (body == null)
            return;

        fishColliders = GetComponentsInChildren<Collider>(true);
        activeFish.Remove(this);
        activeFish.Add(this);
        IgnoreFishAndExceptions();
        originalRoamCenter = initialRoamCenter != null ? initialRoamCenter.position : body.position;
        roamCenter = originalRoamCenter;
        state = State.Roam;
        separationTime = 0f;
        reacquireTimer = 0f;
        PickRoamTarget();
        RefreshReferences();
    }

    private void Start()
    {
        RefreshReferences();
    }

    public void Initialize(DeliveryBox box, PlayerMovement player)
    {
        deliveryBox = box;
        playerMovement = player;
        RefreshReferences();
    }

    private void RefreshReferences()
    {
        if (deliveryBox == null)
            deliveryBox = FindFirstObjectByType<DeliveryBox>();

        if (playerMovement == null)
            playerMovement = FindFirstObjectByType<PlayerMovement>();

        if (worldCamera == null)
            worldCamera = Camera.main;

        if (deliveryBox != null)
        {
            boxBody = deliveryBox.GetComponent<Rigidbody>();
            boxHealth = deliveryBox.GetComponent<DeliveryBoxHealth>();
        }

        if (playerMovement != null)
        {
            boundPlayer = playerMovement;
            playerBody = playerMovement.GetComponentInParent<Rigidbody>();
            Transform playerRoot = playerBody != null ? playerBody.transform : playerMovement.transform;
            playerColliders = playerRoot.GetComponentsInChildren<Collider>(true);
        }
        else
        {
            boundPlayer = null;
            playerBody = null;
            playerColliders = null;
        }

        IgnorePlayerCollisions();
    }

    private void IgnorePlayerCollisions()
    {
        if (fishColliders == null || playerColliders == null || playerMovement == null)
            return;

        foreach (Collider fishCollider in fishColliders)
        {
            if (fishCollider == null || fishCollider.isTrigger || !fishCollider.enabled || !fishCollider.gameObject.activeInHierarchy || fishCollider.attachedRigidbody != body)
                continue;

            foreach (Collider playerCollider in playerColliders)
            {
                if (playerCollider == null || playerCollider.isTrigger || !playerCollider.enabled || !playerCollider.gameObject.activeInHierarchy)
                    continue;

                if (playerBody != null && playerCollider.attachedRigidbody != playerBody)
                    continue;

                if (!Physics.GetIgnoreCollision(fishCollider, playerCollider))
                    Physics.IgnoreCollision(fishCollider, playerCollider, true);
            }
        }
    }

    private void IgnoreFishAndExceptions()
    {
        if (fishColliders == null)
            return;

        foreach (DeliveryAttackFish other in activeFish)
        {
            if (other == null || other == this || !other.isActiveAndEnabled || other.fishColliders == null)
                continue;

            foreach (Collider ownCollider in fishColliders)
            {
                if (!IgnoreAttackFish.CanUse(ownCollider))
                    continue;

                foreach (Collider otherCollider in other.fishColliders)
                {
                    if (!IgnoreAttackFish.CanUse(otherCollider) || ownCollider == otherCollider || Physics.GetIgnoreCollision(ownCollider, otherCollider))
                        continue;

                    Physics.IgnoreCollision(ownCollider, otherCollider, true);
                }
            }
        }

        foreach (IgnoreAttackFish exception in IgnoreAttackFish.Active)
        {
            if (exception != null && exception.isActiveAndEnabled)
                exception.IgnoreWith(fishColliders);
        }
    }

    private void Update()
    {
        referenceRetry -= Time.unscaledDeltaTime;

        if (referenceRetry <= 0f && (deliveryBox == null || boxBody == null || boxHealth == null || playerMovement == null || boundPlayer != playerMovement))
        {
            referenceRetry = 0.5f;
            RefreshReferences();
        }

        UpdatePause();
    }

    private void FixedUpdate()
    {
        IgnorePlayerCollisions();
        IgnoreFishAndExceptions();
        UpdatePause();

        if (suspended)
            return;

        reacquireTimer = Mathf.Max(0f, reacquireTimer - Time.fixedDeltaTime);
        bool canHunt = playerMovement != null && TargetAvailable;
        float distance = canHunt ? Vector3.Distance(body.position, boxBody.position) : float.PositiveInfinity;

        if (state != State.Roam && state != State.ReturnHome)
        {
            if (canHunt && distance > Mathf.Max(loseDistance, detectionRadius + 1f))
                separationTime += Time.fixedDeltaTime;
            else
                separationTime = 0f;

            if (!canHunt || (distance > Mathf.Max(loseDistance, detectionRadius + 1f) && separationTime >= loseDelay))
                EndChase();
        }

        timer -= Time.fixedDeltaTime;

        switch (state)
        {
            case State.Roam:
                Roam();

                if (canHunt && reacquireTimer <= 0f && distance <= detectionRadius)
                {
                    state = State.Pursue;
                    separationTime = 0f;
                }

                break;

            case State.ReturnHome:
                Swim(Vector3.ClampMagnitude((originalRoamCenter - body.position) * 2f, roamSpeed));

                if (Vector3.Distance(body.position, originalRoamCenter) <= 0.2f)
                {
                    state = State.Roam;
                    PickRoamTarget();
                }

                break;

            case State.Pursue:
                Swim((boxBody.worldCenterOfMass - body.worldCenterOfMass).normalized * pursuitSpeed);

                if (distance <= chargeRange)
                    BeginWindup();

                break;

            case State.Windup:
                body.linearVelocity = Vector3.zero;
                Face(boxBody.position - body.position);

                if (timer <= 0f)
                {
                    if (distance > chargeRange * 1.5f)
                    {
                        state = State.Pursue;
                        break;
                    }

                    chargeDirection = (boxBody.worldCenterOfMass - body.worldCenterOfMass).normalized;

                    if (chargeDirection.sqrMagnitude < 0.001f)
                        chargeDirection = transform.right;

                    state = State.Charge;
                    timer = chargeDuration;
                }

                break;

            case State.Charge:
                Swim(chargeDirection * chargeSpeed);

                if (timer <= 0f)
                    BeginRecovery(false);

                break;

            case State.Recover:
                body.linearVelocity = Vector3.zero;

                if (timer <= 0f)
                {
                    RestoreVisual();
                    state = hitSomething ? State.Retreat : State.Pursue;
                    timer = retreatDuration;
                }

                break;

            case State.Retreat:
                body.linearVelocity = -chargeDirection * retreatSpeed;

                if (timer <= 0f)
                {
                    body.linearVelocity = Vector3.zero;
                    state = State.Pursue;
                }

                break;
        }
    }

    private void LateUpdate()
    {
        if (suspended || shakeRoot == null)
            return;

        Vector3 offset = Vector3.zero;

        if (state == State.Recover && hitSomething)
        {
            Vector2 shake = Random.insideUnitCircle * shakeAmount * Mathf.Clamp01(timer / hitRecovery);
            offset = new Vector3(shake.x, shake.y, 0f);
        }

        shakeRoot.localPosition = visualPosition + offset;
    }

    private void BeginWindup()
    {
        state = State.Windup;
        timer = windupDuration;
        body.linearVelocity = Vector3.zero;
    }

    private void BeginRecovery(bool hit)
    {
        hitSomething = hit;
        state = State.Recover;
        timer = hit ? hitRecovery : missRecovery;
        body.linearVelocity = Vector3.zero;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (suspended || !isActiveAndEnabled || state != State.Charge)
            return;

        if (collision.rigidbody == playerBody && playerBody != null)
            return;

        BeginRecovery(true);
    }

    private void EndChase()
    {
        state = roamWhereChaseEnds ? State.Roam : State.ReturnHome;
        roamCenter = roamWhereChaseEnds ? body.position : originalRoamCenter;
        separationTime = 0f;
        reacquireTimer = reacquireDelay;
        hitSomething = false;
        body.linearVelocity = Vector3.zero;
        RestoreVisual();
        PickRoamTarget();
    }

    private void PickRoamTarget()
    {
        roamTarget = roamCenter + Random.insideUnitSphere * roamRadius;
        roamTimer = Mathf.Max(4f, roamRadius * 3f / roamSpeed);
        roamWait = Random.Range(0.3f, 1.2f);
    }

    private void Roam()
    {
        if (roamWait > 0f)
        {
            roamWait -= Time.fixedDeltaTime;
            Swim(Vector3.zero);
            return;
        }

        roamTimer -= Time.fixedDeltaTime;
        Vector3 delta = roamTarget - body.position;

        if (delta.sqrMagnitude < 0.04f || roamTimer <= 0f)
        {
            PickRoamTarget();
            Swim(Vector3.zero);
            return;
        }

        Swim(Vector3.ClampMagnitude(delta * 2f, roamSpeed));
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 center = Application.isPlaying ? roamCenter : (initialRoamCenter != null ? initialRoamCenter.position : transform.position);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(center, roamRadius);
    }

    private void Swim(Vector3 velocity)
    {
        body.linearVelocity = Vector3.MoveTowards(body.linearVelocity, velocity, acceleration * Time.fixedDeltaTime);
        Face(body.linearVelocity);
    }

    private void Face(Vector3 direction)
    {
        if (fishSprite == null)
            return;

        Vector3 right = worldCamera != null ? worldCamera.transform.right : Vector3.right;
        float horizontal = Vector3.Dot(direction, right);

        if (Mathf.Abs(horizontal) > 0.05f)
            fishSprite.flipX = spriteFacesRight ? horizontal < 0f : horizontal > 0f;
    }

    private void UpdatePause()
    {
        if (body == null || suspended == IsPaused)
            return;

        suspended = IsPaused;

        if (suspended)
        {
            savedVelocity = body.linearVelocity;
            body.linearVelocity = Vector3.zero;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.isKinematic = true;
        }
        else
        {
            body.isKinematic = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearVelocity = savedVelocity;
        }
    }

    private void RestoreVisual()
    {
        if (shakeRoot != null)
            shakeRoot.localPosition = visualPosition;
    }

    private void OnDisable()
    {
        activeFish.Remove(this);
        RestoreVisual();

        if (body == null)
            return;

        if (!body.isKinematic)
            body.linearVelocity = Vector3.zero;

        savedVelocity = Vector3.zero;
        state = State.Roam;
    }
}
