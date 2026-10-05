using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    private enum MovementState { Grounded, Swimming }

    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Transform visual;

    [Header("Input")]
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference swimUpAction;
    [SerializeField] private InputActionReference swimDownAction;

    [Header("Walking")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float walkAcceleration = 35f;
    [SerializeField] private float walkDeceleration = 50f;
    [SerializeField] private float groundedGravity = 3f;
    [SerializeField, Min(0.1f)] private float groundStickSpeed = 2f;

    [Header("Swimming")]
    [SerializeField] private float swimHorizontalSpeed = 5f;
    [SerializeField] private float swimVerticalSpeed = 4f;
    [SerializeField] private float swimAcceleration = 14f;
    [SerializeField] private float swimDeceleration = 20f;
    [SerializeField] private float takeoffSpeed = 2.5f;
    [SerializeField, Min(0f)] private float takeoffGroundDelay = 0.15f;

    [Header("Swimming Rotation - Visual Only")]
    [SerializeField] private float swimRotationSpeed = 300f;
    [SerializeField] private float swimIdleRotationSpeed = 200f;
    [SerializeField] private float swimIdleSpeedThreshold = 0.15f;

    [Header("Controller Shape")]
    [SerializeField, Min(0.01f)] private float bodyRadius = 0.12f;
    [SerializeField, Min(0.02f)] private float standingHeight = 0.875f;
    [SerializeField, Min(0.02f)] private float swimmingHeight = 0.3f;
    [SerializeField] private Vector3 bodyCentre = Vector3.zero;
    [SerializeField, Min(0.001f)] private float controllerSkinWidth = 0.012f;

    [Header("Terrain")]
    [SerializeField, Min(0f)] private float maximumStepHeight = 0.18f;
    [SerializeField, Min(0f)] private float maximumStepDown = 0.2f;
    [SerializeField, Range(0f, 89f)] private float maximumGroundAngle = 55f;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float groundCheckDistance = 0.04f;
    [SerializeField, Min(0f)] private float groundGraceTime = 0.08f;

    [Header("Standing Clearance")]
    [SerializeField] private LayerMask standingObstacleLayers = ~0;
    [SerializeField, Min(0f)] private float standingClearanceTolerance = 0.005f;

    [Header("Swim Boost")]
    [SerializeField] private bool enableSwimBoost = true;
    [SerializeField, Min(0.1f)] private float boostChargeTime = 3f;
    [SerializeField, Min(1f)] private float boostSpeedMultiplier = 1.6f;
    [SerializeField, Range(0.5f, 1f)] private float boostRequiredSpeed = 0.9f;
    [SerializeField, Range(1f, 45f)] private float boostTurnTolerance = 15f;
    [SerializeField, Min(0.1f)] private float boostCoastDeceleration = 5f;

    [Header("Boost Roll")]
    [SerializeField] private Sprite boostSideSprite;
    [SerializeField] private Sprite boostBackSprite;
    [SerializeField] private Sprite boostUpsideDownSprite;
    [SerializeField] private Sprite boostBellySprite;
    [SerializeField] private bool correctUpsideDownFacing = true;
    [SerializeField, Min(0.08f)] private float boostSpinDuration = 0.48f;
    [SerializeField] private bool reverseDepthSpin;

    [Header("Boost Bubbles")]
    [SerializeField] private Sprite bubbleSprite;
    [SerializeField] private Material bubbleMaterial;
    [SerializeField, Min(0f)] private float bubbleTailOffset = 0.4f;
    [SerializeField, Min(0f)] private float bubbleSpread = 0.12f;
    [SerializeField, Min(0f)] private float bubbleTrailRate = 28f;
    [SerializeField, Min(0f)] private float bubbleSpinRate = 100f;
    [SerializeField, Min(0)] private int bubbleBurstCount = 24;
    [SerializeField] private Vector2 bubbleSize = new Vector2(0.035f, 0.08f);
    [SerializeField] private Vector2 bubbleLifetime = new Vector2(0.5f, 1.1f);
    [SerializeField, Min(0f)] private float bubbleRiseSpeed = 0.35f;
    [SerializeField, Min(0f)] private float bubbleBackwardSpeed = 0.35f;

    [Header("Landing and Takeoff")]
    [SerializeField] private ParticleSystem landingSand;
    [SerializeField, Min(0)] private int landingSandCount = 18;
    [SerializeField, Min(0)] private int takeoffBubbleCount = 14;
    [SerializeField, Min(0f)] private float landingSandHeightOffset = 0.03f;

    [Header("Idle Sinking")]
    [SerializeField] private float idleSinkDelay = 5f;
    [SerializeField] private float idleSinkSpeed = 0.3f;
    [SerializeField] private float idleSinkAcceleration = 0.15f;


    private float swimIdleTimer;
    private bool IsIdleSinking => IsSwimming && swimIdleTimer > idleSinkDelay;

    private bool isBoosting;
    private bool boostMomentum;
    private float boostCharge;
    private Vector3 boostHeading;
    private float boostSpinRemaining;
    private float bubbleEmissionRemainder;
    private bool boostVisualApplied;
    private Sprite unmodifiedSprite;
    private bool unmodifiedFlipX;
    private Quaternion unmodifiedVisualRotation;
    private ParticleSystem boostBubbles;
    private Material runtimeBubbleMaterial;

    public bool IsBoosting => isBoosting;
    public float BoostCharge01 => Mathf.Clamp01(boostCharge / Mathf.Max(0.1f, boostChargeTime));

    private CharacterController controller;
    private MovementState currentState;
    private Vector3 velocity;
    private Vector3 actualVelocity;
    private float horizontalInput;
    private float depthInput;
    private float verticalInput;
    private float timeSinceGrounded;
    private float groundIgnoreTimer;
    private bool isGrounded;
    private bool moveTouchedWalkableGround;
    private int facingDirection;
    private bool facingLeft;
    private float swimHeading;
    private bool swimHeadingActive;
    private bool movementEnabled = true;
    private bool preserveAnimationAfterUnfreeze;
    private bool animationFrozen;
    private float previousAnimatorSpeed = 1f;
    private readonly RaycastHit[] groundHits = new RaycastHit[32];
    private readonly Collider[] clearanceHits = new Collider[32];

    public bool IsMovementEnabled => movementEnabled;
    public Vector3 Velocity => actualVelocity;
    public bool IsSwimming => currentState == MovementState.Swimming;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (GetComponent<Rigidbody>() != null)
        {
            Debug.LogError("PlayerMovement: remove the Player Rigidbody before using this CharacterController version.", this);
            enabled = false;
            return;
        }

        foreach (Collider body in GetComponentsInChildren<Collider>())
        {
            if (body == controller || body.isTrigger || !body.enabled) continue;
            Debug.LogError("PlayerMovement: disable the old solid body collider. Keep interaction triggers enabled.", body);
            enabled = false;
            return;
        }

        if (Vector3.Distance(transform.lossyScale, Vector3.one) > 0.001f || Vector3.Dot(transform.up, Vector3.up) < 0.999f)
        {
            Debug.LogError("PlayerMovement: the Player root must be upright with world scale (1, 1, 1). Scale or rotate Visual instead.", this);
            enabled = false;
            return;
        }

        controller.stepOffset = 0f;
        controller.radius = Mathf.Max(0.01f, bodyRadius);
        standingHeight = Mathf.Max(standingHeight, controller.radius * 2f);
        swimmingHeight = Mathf.Clamp(swimmingHeight, controller.radius * 2f, standingHeight);
        controller.height = swimmingHeight;
        controller.center = bodyCentre;
        controller.skinWidth = Mathf.Clamp(controllerSkinWidth, 0.001f, controller.radius * 0.5f);
        controller.slopeLimit = maximumGroundAngle;
        controller.minMoveDistance = 0f;
        controller.detectCollisions = true;
        controller.enableOverlapRecovery = true;
        currentState = MovementState.Swimming;

        CreateBoostBubbles();
    }

    private void Start()
    {
        if (!enabled)
            return;

        if (CanOccupyStandingShape(Vector3.zero))
        {
            controller.height = standingHeight;
            if (TryFindGround(groundCheckDistance, out _))
            {
                currentState = MovementState.Grounded;
                isGrounded = true;
                controller.stepOffset = Mathf.Min(maximumStepHeight, standingHeight - 0.001f);
            }
            else controller.height = swimmingHeight;
        }
    }

    private void Update()
    {
        if (!movementEnabled || PauseController.IsGamePaused)
        {
            StopImmediately();

            if (boostBubbles != null && boostBubbles.isPlaying)
                boostBubbles.Pause();

            if (landingSand != null && landingSand.isPlaying)
                landingSand.Pause();

            return;
        }

        if (!controller.enabled || Time.deltaTime <= 0f)
            return;

        RestoreBoostVisual();

        if (boostBubbles != null && boostBubbles.isPaused)
            boostBubbles.Play();

        if (landingSand != null && landingSand.isPaused)
            landingSand.Play();

        ReadInput();
        float deltaTime = Mathf.Min(Time.deltaTime, 0.1f);
        boostSpinRemaining = Mathf.Max(0f, boostSpinRemaining - deltaTime);
        UpdateBoostCharge(deltaTime);

        int count = Mathf.Max(1, Mathf.CeilToInt(deltaTime / 0.02f));
        Vector3 start = transform.position;
        float postureLift = 0f;

        for (int i = 0; i < count; i++)
            postureLift += SimulateMovement(deltaTime / count);

        actualVelocity = (transform.position - start - Vector3.up * postureLift) / deltaTime;

        UpdateFacingDirection();
        UpdateSwimmingRotation();
        UpdateSpriteFlip();
        UpdateAnimator();
        UpdateBoostBubbles(deltaTime, start);
    }

    private float SimulateMovement(float deltaTime)
    {
        groundIgnoreTimer = Mathf.Max(0f, groundIgnoreTimer - deltaTime);
        float postureLift = 0f;

        if (currentState == MovementState.Grounded && verticalInput > 0.1f)
            EnterSwimmingState(true);

        if (currentState == MovementState.Swimming && groundIgnoreTimer <= 0f && verticalInput <= 0.1f && velocity.y <= 0.1f)
        {
            if (TryFindGround(groundCheckDistance, out _))
                TryEnterGroundedState(out postureLift);
        }

        bool walking = currentState == MovementState.Grounded;

        bool hasMovementInput = new Vector3(horizontalInput, verticalInput, depthInput).sqrMagnitude > 0.0001f;
        swimIdleTimer = walking || hasMovementInput ? 0f : swimIdleTimer + deltaTime;

        controller.stepOffset = walking ? Mathf.Min(maximumStepHeight, controller.height - 0.001f) : 0f;

        if (walking)
        {
            Vector3 input = Vector3.ClampMagnitude(new Vector3(horizontalInput, 0f, depthInput), 1f);
            float rate = input.sqrMagnitude > 0.0001f ? walkAcceleration : walkDeceleration;
            Vector3 horizontal = Vector3.MoveTowards(new Vector3(velocity.x, 0f, velocity.z), input * walkSpeed, rate * deltaTime);
            velocity.x = horizontal.x;
            velocity.z = horizontal.z;
            velocity.y = isGrounded ? -groundStickSpeed : velocity.y + Physics.gravity.y * groundedGravity * deltaTime;
        }
        else
        {
            Vector3 input = Vector3.ClampMagnitude(new Vector3(horizontalInput, verticalInput, depthInput), 1f);
            Vector3 target = new Vector3(input.x * swimHorizontalSpeed, input.y * swimVerticalSpeed, input.z * swimHorizontalSpeed);

            if (isBoosting)
                target *= boostSpeedMultiplier;

            float rate = input.sqrMagnitude > 0.0001f ? swimAcceleration : swimDeceleration;

            if (!isBoosting && velocity.magnitude <= target.magnitude + 0.1f)
                boostMomentum = false;
            if (!isBoosting && boostMomentum && velocity.magnitude > target.magnitude + 0.1f && (input.sqrMagnitude < 0.0001f || Vector3.Dot(velocity.normalized, target.normalized) > 0.7f))
                rate = boostCoastDeceleration;

            if (IsIdleSinking)
            {
                target = Vector3.down * idleSinkSpeed;
                rate = idleSinkAcceleration;
            }

            velocity = Vector3.MoveTowards(velocity, target, rate * deltaTime);
        }

        moveTouchedWalkableGround = false;
        CollisionFlags flags = controller.Move(velocity * deltaTime);
        bool supported = moveTouchedWalkableGround && (flags & CollisionFlags.Below) != 0;

        if ((flags & CollisionFlags.Above) != 0 && velocity.y > 0f)
            velocity.y = 0f;
        if ((flags & CollisionFlags.Below) != 0 && velocity.y < 0f)
            velocity.y = 0f;

        if (walking && !supported && verticalInput <= 0.1f && TryFindGround(maximumStepDown, out float drop))
        {
            moveTouchedWalkableGround = false;
            flags = controller.Move(Vector3.down * (drop + controller.skinWidth));
            supported = moveTouchedWalkableGround && (flags & CollisionFlags.Below) != 0;
        }

        isGrounded = supported;
        if (walking)
        {
            timeSinceGrounded = supported ? 0f : timeSinceGrounded + deltaTime;

            if (timeSinceGrounded > groundGraceTime)
                EnterSwimmingState(false);
        }
        else if (supported && verticalInput <= 0.1f && groundIgnoreTimer <= 0f)
            if (TryEnterGroundedState(out float lift)) postureLift += lift;

        return postureLift;
    }

    private void EnterSwimmingState(bool takingOff)
    {
        bool leavingGround = currentState == MovementState.Grounded;

        currentState = MovementState.Swimming;
        controller.stepOffset = 0f;
        controller.height = swimmingHeight;
        isGrounded = false;
        timeSinceGrounded = 0f;

        if (leavingGround)
        {
            Vector3 direction = new Vector3(horizontalInput, verticalInput, depthInput);

            if (direction.sqrMagnitude < 0.001f)
                direction = velocity;
            if (direction.sqrMagnitude < 0.001f)
                direction = Vector3.up;

            EmitBoostBubbles(takeoffBubbleCount, transform.position, direction.normalized, true);
        }

        if (!takingOff)
            return;

        groundIgnoreTimer = takeoffGroundDelay;
        velocity.y = takeoffSpeed;
    }

    private bool TryEnterGroundedState(out float lift)
    {

        lift = 0f;
        if (currentState == MovementState.Grounded)
            return true;

        float rise = Mathf.Max(0f, (standingHeight - controller.height) * 0.5f);

        if (!CanOccupyStandingShape(Vector3.up * rise))
            return false;

        controller.enabled = false;
        transform.position += Vector3.up * rise;
        controller.height = standingHeight;
        controller.enabled = true;
        controller.stepOffset = Mathf.Min(maximumStepHeight, standingHeight - 0.001f);
        currentState = MovementState.Grounded;

        ResetBoost();

        isGrounded = true;
        timeSinceGrounded = 0f;
        velocity.y = 0f;
        lift = rise;

        PlayLandingSand();

        return true;
    }

    private void PlayLandingSand()
    {
        if (landingSand == null || landingSandCount <= 0)
            return;

        Vector3 feet = transform.TransformPoint(controller.center) - Vector3.up * (controller.height * 0.5f);
        landingSand.transform.SetPositionAndRotation(feet + Vector3.up * landingSandHeightOffset, Quaternion.LookRotation(Vector3.up));

        if (!landingSand.isPlaying)
            landingSand.Play();

        landingSand.Emit(landingSandCount);
    }

    private bool IsObstacle(Collider obstacle)
    {
        if (obstacle == null || obstacle == controller || obstacle.transform.IsChildOf(transform))
            return false;
        if (Physics.GetIgnoreLayerCollision(gameObject.layer, obstacle.gameObject.layer))
            return false;

        return !Physics.GetIgnoreCollision(controller, obstacle);
    }

    private bool IsWalkable(Collider obstacle, Vector3 normal)
    {
        return IsObstacle(obstacle) && (groundLayer.value & (1 << obstacle.gameObject.layer)) != 0 && normal.y >= Mathf.Cos(maximumGroundAngle * Mathf.Deg2Rad);
    }

    private bool CanOccupyStandingShape(Vector3 offset)
    {
        Vector3 centre = transform.TransformPoint(bodyCentre) + offset;
        float segment = standingHeight * 0.5f - controller.radius;
        Vector3 bottom = centre - Vector3.up * segment;
        Vector3 top = centre + Vector3.up * segment;
        float radius = Mathf.Max(0.001f, controller.radius - controller.skinWidth - Mathf.Min(standingClearanceTolerance, controller.radius * 0.1f));
        int mask = standingObstacleLayers.value | groundLayer.value;
        int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, clearanceHits, mask, QueryTriggerInteraction.Ignore);
        Collider[] hits = clearanceHits;
        if (count == clearanceHits.Length)
        {
            hits = Physics.OverlapCapsule(bottom, top, radius, mask, QueryTriggerInteraction.Ignore);
            count = hits.Length;
        }

        for (int i = 0; i < count; i++)
            if (IsObstacle(hits[i])) return false;

        return true;
    }

    private bool TryFindGround(float distance, out float drop)
    {
        drop = 0f;
        float radius = controller.radius * 0.9f;
        float lift = controller.skinWidth + 0.01f;
        Vector3 centre = transform.TransformPoint(controller.center);
        Vector3 feet = centre - Vector3.up * (controller.height * 0.5f);
        Vector3 origin = feet + Vector3.up * (radius + lift);
        int mask = standingObstacleLayers.value | groundLayer.value;
        int count = Physics.SphereCastNonAlloc(origin, radius, Vector3.down, groundHits, distance + lift, mask, QueryTriggerInteraction.Ignore);
        RaycastHit[] hits = groundHits;

        if (count == groundHits.Length)
        {
            hits = Physics.SphereCastAll(origin, radius, Vector3.down, distance + lift, mask, QueryTriggerInteraction.Ignore);
            count = hits.Length;
        }

        RaycastHit nearest = default;
        float nearestDistance = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            if (!IsObstacle(hits[i].collider) || hits[i].distance >= nearestDistance) continue;
            nearest = hits[i];
            nearestDistance = hits[i].distance;
        }

        if (!IsWalkable(nearest.collider, nearest.normal))
            return false;

        drop = Mathf.Max(0f, nearest.distance - lift);
        return drop <= distance;
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (IsWalkable(hit.collider, hit.normal))
            moveTouchedWalkableGround = true;

        if (currentState != MovementState.Swimming)
            return;

        float intoSurface = Vector3.Dot(velocity, hit.normal);

        if (intoSurface < 0f)
            velocity -= hit.normal * intoSurface;
    }

    private void ReadInput()
    {
        if (!movementEnabled)
        {
            horizontalInput = 0f;
            depthInput = 0f;
            verticalInput = 0f;
            return;
        }

        Vector2 moveInput = Vector2.zero;

        if (moveAction != null)
            moveInput = moveAction.action.ReadValue<Vector2>();

        horizontalInput = moveInput.x;
        depthInput = moveInput.y;

        verticalInput = 0f;

        if (swimUpAction != null && swimUpAction.action.IsPressed())
            verticalInput += 1f;

        if (swimDownAction != null && swimDownAction.action.IsPressed())
            verticalInput -= 1f;

    }

    private Vector3 GetFacingDirection()
    {
        if (IsIdleSinking)
            return Vector3.zero;

        float vertical = currentState == MovementState.Swimming ? verticalInput : 0f;
        Vector3 direction = new Vector3(horizontalInput, vertical, depthInput);

        if (direction.sqrMagnitude > 0.01f)
            return direction;

        direction = actualVelocity;
        if (currentState == MovementState.Grounded)
            direction.y = 0f;

        return direction.magnitude > swimIdleSpeedThreshold ? direction : Vector3.zero;
    }

    private void UpdateFacingDirection()
    {
        Vector3 direction = GetFacingDirection();
        if (direction.sqrMagnitude < 0.001f) return;

        float sideStrength = new Vector2(direction.x, direction.y).magnitude;
        float depthStrength = Mathf.Abs(direction.z);

        bool useDepth = facingDirection == 0 ? depthStrength > sideStrength * 1.15f : depthStrength > sideStrength * 0.85f;

        if (useDepth)
        {
            facingDirection = direction.z < 0f ? 1 : 2;
            return;
        }

        facingDirection = 0;

        if (currentState == MovementState.Grounded)
        {
            if (direction.x < -0.01f) facingLeft = true;
            if (direction.x > 0.01f) facingLeft = false;
        }
    }

    private void UpdateSwimmingRotation()
    {
        if (visual == null)
            return;

        if (currentState == MovementState.Grounded)
        {
            swimHeadingActive = false;
            float groundedRotation = Mathf.MoveTowardsAngle(visual.localEulerAngles.z, 0f, swimIdleRotationSpeed * Time.deltaTime);
            visual.localRotation = Quaternion.Euler(0f, 0f, groundedRotation);
            return;
        }

        if (facingDirection != 0)
        {
            swimHeadingActive = false;
            float depthRotation = Mathf.MoveTowardsAngle(visual.localEulerAngles.z, 0f, swimRotationSpeed * Time.deltaTime);
            visual.localRotation = Quaternion.Euler(0f, 0f, depthRotation);
            return;
        }

        Vector3 direction = GetFacingDirection();
        Vector2 visibleDirection = new Vector2(direction.x, direction.y);

        if (visibleDirection.sqrMagnitude < 0.001f)
            return;

        float targetHeading = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        if (!swimHeadingActive)
        {
            swimHeading = targetHeading;
            swimHeadingActive = true;
        }

        bool horizontalOnly = Mathf.Abs(horizontalInput) > 0.01f && Mathf.Abs(verticalInput) < 0.01f && Mathf.Abs(depthInput) < 0.01f;
        bool oppositeHeading = Mathf.Abs(Mathf.DeltaAngle(swimHeading, targetHeading)) > 175f;
        if (horizontalOnly && oppositeHeading)
        {
            swimHeading = targetHeading;
        }

        swimHeading = Mathf.MoveTowardsAngle(swimHeading, targetHeading, swimRotationSpeed * Time.deltaTime);

        float horizontalHeading = Mathf.Cos(swimHeading * Mathf.Deg2Rad);

        if (horizontalHeading < -0.001f) facingLeft = true;
        if (horizontalHeading > 0.001f) facingLeft = false;

        float spriteRotation = swimHeading - (facingLeft ? 180f : 0f);
        visual.localRotation = Quaternion.Euler(0f, 0f, spriteRotation);
    }

    private void UpdateAnimator()
    {
        if (animator == null)
            return;

        if (preserveAnimationAfterUnfreeze)
        {
            bool hasInput = Mathf.Abs(horizontalInput) > 0.01f || Mathf.Abs(depthInput) > 0.01f || Mathf.Abs(verticalInput) > 0.01f;
            float relevantSpeed;

            if (currentState == MovementState.Grounded)
                relevantSpeed = new Vector2(actualVelocity.x, actualVelocity.z).magnitude;
            else
                relevantSpeed = actualVelocity.magnitude;

            if (hasInput && relevantSpeed < 0.01f)
                return;

            preserveAnimationAfterUnfreeze = false;
        }

        Vector3 velocity = actualVelocity;

        float groundSpeed = new Vector2(velocity.x, velocity.z).magnitude;

        // float swimSpeed = velocity.magnitude;
        float swimSpeed = IsIdleSinking ? 0f : velocity.magnitude;

        animator.SetFloat("GroundSpeed", groundSpeed);
        animator.SetFloat("SwimSpeed", swimSpeed);
        animator.SetBool("IsSwimming", currentState == MovementState.Swimming);

        float animationSpeed = currentState == MovementState.Swimming ? swimSpeed : groundSpeed;

        animator.SetInteger("Facing", facingDirection);
        animator.SetBool("Moving", animationSpeed > swimIdleSpeedThreshold);
    }

    public void SetMovementEnabled(bool enabled)
    {
        movementEnabled = enabled;
        if (!enabled)
        {
            horizontalInput = 0f;
            depthInput = 0f;
            verticalInput = 0f;
            StopImmediately();
        }
        else preserveAnimationAfterUnfreeze = true;
    }

    public void SetAnimationFrozen(bool frozen)
    {
        if (animator == null || animationFrozen == frozen)
            return;

        animationFrozen = frozen;

        if (frozen)
        {
            previousAnimatorSpeed = animator.speed;
            animator.speed = 0f;
        }
        else
            animator.speed = previousAnimatorSpeed;
    }

    public void StopImmediately()
    {
        swimIdleTimer = 0f;
        velocity = Vector3.zero;
        actualVelocity = Vector3.zero;
    }

    private void UpdateSpriteFlip()
    {
        if (spriteRenderer == null)
            return;

        spriteRenderer.flipX = facingDirection == 0 && !facingLeft;
    }

    private void OnEnable()
    {
        moveAction?.action.Enable();
        swimUpAction?.action.Enable();
        swimDownAction?.action.Enable();
    }

    private void OnDisable()
    {
        ResetBoost();

        if (landingSand != null)
            landingSand.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        if (boostBubbles != null)
            boostBubbles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        moveAction?.action.Disable();
        swimUpAction?.action.Disable();
        swimDownAction?.action.Disable();
    }

    public void TeleportTo(Vector3 position)
    {
        StopImmediately();

        ResetBoost();

        if (landingSand != null)
            landingSand.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        if (boostBubbles != null)
            boostBubbles.Clear();

        horizontalInput = 0f;
        depthInput = 0f;
        verticalInput = 0f;
        timeSinceGrounded = 0f;
        groundIgnoreTimer = 0f;
        isGrounded = false;
        moveTouchedWalkableGround = false;
        swimHeadingActive = false;

        bool wasEnabled = controller.enabled;
        controller.enabled = false;
        controller.stepOffset = 0f;
        controller.height = swimmingHeight;
        transform.SetPositionAndRotation(position, Quaternion.identity);
        currentState = MovementState.Swimming;
        controller.enabled = wasEnabled;

        Physics.SyncTransforms();

        if (wasEnabled && CanOccupyStandingShape(Vector3.zero))
        {
            controller.height = standingHeight;

            if (TryFindGround(groundCheckDistance, out _))
            {
                currentState = MovementState.Grounded;
                isGrounded = true;
                controller.stepOffset = Mathf.Min(maximumStepHeight, standingHeight - 0.001f);
            }
            else controller.height = swimmingHeight;
        }

        preserveAnimationAfterUnfreeze = true;
    }

    private void UpdateBoostCharge(float deltaTime)
    {
        if (!enableSwimBoost || !IsSwimming)
        {
            ResetBoost();
            return;
        }

        Vector3 input = Vector3.ClampMagnitude(new Vector3(horizontalInput, verticalInput, depthInput), 1f);

        if (input.magnitude < 0.5f)
        {
            CancelBoost();

            boostCharge = Mathf.MoveTowards(boostCharge, 0f, deltaTime * 2f);
            return;
        }

        Vector3 target = new Vector3(input.x * swimHorizontalSpeed, input.y * swimVerticalSpeed, input.z * swimHorizontalSpeed);
        Vector3 direction = target.normalized;

        if (boostHeading.sqrMagnitude < 0.001f)
            boostHeading = direction;

        float turn = Vector3.Angle(boostHeading, direction);

        if (turn > boostTurnTolerance)
        {
            CancelBoost();

            float retained = turn < 75f ? Mathf.Pow(Mathf.Max(0f, Vector3.Dot(boostHeading, direction)), 2f) : 0f;
            boostCharge = Mathf.Min(boostCharge * retained, boostChargeTime * 0.8f);
            boostHeading = direction;
        }

        float forwardSpeed = Vector3.Dot(actualVelocity, direction);
        float alignment = actualVelocity.sqrMagnitude > 0.001f ? Vector3.Dot(actualVelocity.normalized, direction) : 0f;
        bool fastEnough = forwardSpeed >= target.magnitude * boostRequiredSpeed && alignment > 0.96f;

        if (isBoosting)
            return;

        boostCharge = fastEnough ? Mathf.Min(boostChargeTime, boostCharge + deltaTime) : Mathf.MoveTowards(boostCharge, 0f, deltaTime);

        if (boostCharge < boostChargeTime)
            return;

        isBoosting = true;
        boostMomentum = true;
        boostHeading = direction;
        boostSpinRemaining = boostSpinDuration;

        EmitBoostBubbles(bubbleBurstCount, transform.position, direction, true);
        SoundEffectManager.Play("SwimBoost", true);
    }

    private void CancelBoost()
    {
        isBoosting = false;
        boostSpinRemaining = 0f;
        bubbleEmissionRemainder = 0f;
    }

    private void ResetBoost()
    {
        CancelBoost();
        boostMomentum = false;
        boostCharge = 0f;
        boostHeading = Vector3.zero;
        RestoreBoostVisual();
    }

    private void LateUpdate()
    {
        if (!movementEnabled || PauseController.IsGamePaused || animationFrozen || Time.deltaTime <= 0f)
            return;
        if (boostSpinRemaining <= 0f || !isBoosting || spriteRenderer == null)
            return;

        int frame = Mathf.Clamp(Mathf.FloorToInt((1f - boostSpinRemaining / Mathf.Max(0.08f, boostSpinDuration)) * 4f), 0, 3);

        unmodifiedSprite = spriteRenderer.sprite;
        unmodifiedFlipX = spriteRenderer.flipX;

        if (visual != null)
            unmodifiedVisualRotation = visual.localRotation;

        boostVisualApplied = true;

        if (facingDirection == 0)
        {
            Sprite frameSprite = frame == 0 ? boostSideSprite : frame == 1 ? boostBackSprite : frame == 2 ? boostUpsideDownSprite : boostBellySprite;

            if (frameSprite != null)
                spriteRenderer.sprite = frameSprite;
            if (frame == 2 && frameSprite != null && correctUpsideDownFacing)
                spriteRenderer.flipX = !spriteRenderer.flipX;
        }
        else if (visual != null)
        {
            float sign = (facingDirection == 1 ? 1f : -1f) * (reverseDepthSpin ? -1f : 1f);
            visual.localRotation = unmodifiedVisualRotation * Quaternion.Euler(0f, 0f, frame * 90f * sign);
        }
    }

    private void RestoreBoostVisual()
    {
        if (!boostVisualApplied)
            return;

        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = unmodifiedSprite;
            spriteRenderer.flipX = unmodifiedFlipX;
        }

        if (visual != null)
            visual.localRotation = unmodifiedVisualRotation;

        boostVisualApplied = false;
    }

    private void CreateBoostBubbles()
    {
        if (bubbleSprite == null || bubbleMaterial == null)
            return;

        GameObject bubbleObject = new GameObject("Swim Boost Bubbles");

        bubbleObject.layer = gameObject.layer;
        bubbleObject.transform.SetParent(transform, false);
        boostBubbles = bubbleObject.AddComponent<ParticleSystem>();
        boostBubbles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = boostBubbles.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Shape;
        main.maxParticles = 400;
        main.startSpeed = 0f;
        main.gravityModifier = 0f;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;

        var emission = boostBubbles.emission;
        emission.enabled = false;

        var shape = boostBubbles.shape;
        shape.enabled = false;

        var texture = boostBubbles.textureSheetAnimation;
        texture.enabled = true;
        texture.mode = ParticleSystemAnimationMode.Sprites;
        texture.AddSprite(bubbleSprite);

        var colour = boostBubbles.colorOverLifetime;
        colour.enabled = true;
        Gradient fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.55f), new GradientAlphaKey(0f, 1f) });
        colour.color = fade;

        var size = boostBubbles.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1f));

        ParticleSystemRenderer bubbleRenderer = boostBubbles.GetComponent<ParticleSystemRenderer>();
        runtimeBubbleMaterial = new Material(bubbleMaterial);

        if (runtimeBubbleMaterial.HasProperty("_BaseMap"))
            runtimeBubbleMaterial.SetTexture("_BaseMap", bubbleSprite.texture);
        if (runtimeBubbleMaterial.HasProperty("_MainTex"))
            runtimeBubbleMaterial.SetTexture("_MainTex", bubbleSprite.texture);

        bubbleRenderer.sharedMaterial = runtimeBubbleMaterial;
        bubbleRenderer.renderMode = ParticleSystemRenderMode.Billboard;

        if (spriteRenderer != null)
        {
            bubbleRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
            bubbleRenderer.sortingOrder = spriteRenderer.sortingOrder - 1;
        }

        boostBubbles.Play();
    }

    private void UpdateBoostBubbles(float deltaTime, Vector3 frameStart)
    {
        if (!isBoosting)
            return;

        Vector3 input = Vector3.ClampMagnitude(new Vector3(horizontalInput, verticalInput, depthInput), 1f);
        Vector3 target = new Vector3(input.x * swimHorizontalSpeed, input.y * swimVerticalSpeed, input.z * swimHorizontalSpeed);

        float forwardSpeed = Vector3.Dot(actualVelocity, target.normalized);

        if (!IsSwimming || forwardSpeed < target.magnitude * 0.65f || Vector3.Dot(actualVelocity.normalized, target.normalized) < 0.7f)
        {
            ResetBoost();
            return;
        }

        if (boostBubbles == null)
            return;

        bool spinning = boostSpinRemaining > 0f;
        bubbleEmissionRemainder += (spinning ? bubbleSpinRate : bubbleTrailRate) * deltaTime;

        int count = Mathf.FloorToInt(bubbleEmissionRemainder);
        bubbleEmissionRemainder -= count;

        for (int i = 0; i < count; i++)
            EmitBoostBubbles(1, Vector3.Lerp(frameStart, transform.position, (i + 1f) / Mathf.Max(1, count)), actualVelocity.normalized, spinning);
    }

    private void EmitBoostBubbles(int count, Vector3 origin, Vector3 direction, bool burst)
    {
        if (boostBubbles == null || count <= 0)
            return;

        if (!boostBubbles.isPlaying)
            boostBubbles.Play();

        for (int i = 0; i < count; i++)
        {
            ParticleSystem.EmitParams particle = new ParticleSystem.EmitParams();
            float behind = burst ? Random.Range(0.2f, 1.2f) : 1f;
            particle.position = origin + transform.TransformVector(bodyCentre) - direction * bubbleTailOffset * behind + Random.insideUnitSphere * bubbleSpread * (burst ? 1.6f : 1f);
            particle.velocity = Vector3.up * bubbleRiseSpeed - direction * bubbleBackwardSpeed * Random.Range(0.5f, 1.2f) + Random.insideUnitSphere * 0.08f;
            particle.startLifetime = Random.Range(Mathf.Max(0.05f, bubbleLifetime.x), Mathf.Max(0.05f, Mathf.Max(bubbleLifetime.x, bubbleLifetime.y)));
            particle.startSize = Random.Range(Mathf.Max(0.001f, bubbleSize.x), Mathf.Max(0.001f, Mathf.Max(bubbleSize.x, bubbleSize.y)));
            particle.startColor = Color.white;
            boostBubbles.Emit(particle, 1);
        }
    }

    private void OnDestroy()
    {
        if (runtimeBubbleMaterial != null)
            Destroy(runtimeBubbleMaterial);
    }
}