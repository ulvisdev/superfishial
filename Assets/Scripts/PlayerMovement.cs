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
    [SerializeField, Min(0.001f)] private float groundCheckDistance = 0.04f;
    [SerializeField, Min(0f)] private float groundGraceTime = 0.08f;

    [Header("Standing Clearance")]
    [SerializeField] private LayerMask standingObstacleLayers = ~0;
    [SerializeField, Min(0f)] private float standingClearanceTolerance = 0.005f;

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
    }

    private void Start()
    {
        if (!enabled) return;
        // Preserve the original standing spawn position if the taller shape fits here.
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
            return;
        }

        if (!controller.enabled || Time.deltaTime <= 0f) return;
        ReadInput();
        float deltaTime = Mathf.Min(Time.deltaTime, 0.1f);
        // Small movement slices make fast movement less sensitive to frame rate.
        int count = Mathf.Max(1, Mathf.CeilToInt(deltaTime / 0.02f));
        Vector3 start = transform.position;
        float postureLift = 0f;
        for (int i = 0; i < count; i++) postureLift += SimulateMovement(deltaTime / count);
        actualVelocity = (transform.position - start - Vector3.up * postureLift) / deltaTime;

        UpdateFacingDirection();
        UpdateSwimmingRotation();
        UpdateSpriteFlip();
        UpdateAnimator();
    }

    private float SimulateMovement(float deltaTime)
    {
        groundIgnoreTimer = Mathf.Max(0f, groundIgnoreTimer - deltaTime);
        float postureLift = 0f;

        if (currentState == MovementState.Grounded && verticalInput > 0.1f) EnterSwimmingState(true);
        if (currentState == MovementState.Swimming && groundIgnoreTimer <= 0f && verticalInput <= 0.1f && velocity.y <= 0.1f)
        {
            if (TryFindGround(groundCheckDistance, out _)) TryEnterGroundedState(out postureLift);
        }

        bool walking = currentState == MovementState.Grounded;
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
            float rate = input.sqrMagnitude > 0.0001f ? swimAcceleration : swimDeceleration;
            velocity = Vector3.MoveTowards(velocity, target, rate * deltaTime);
        }

        moveTouchedWalkableGround = false;
        CollisionFlags flags = controller.Move(velocity * deltaTime);
        bool supported = moveTouchedWalkableGround && (flags & CollisionFlags.Below) != 0;
        if ((flags & CollisionFlags.Above) != 0 && velocity.y > 0f) velocity.y = 0f;
        if ((flags & CollisionFlags.Below) != 0 && velocity.y < 0f) velocity.y = 0f;

        // Descend only to nearby verified ground; never snap while swimming upward.
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
            if (timeSinceGrounded > groundGraceTime) EnterSwimmingState(false);
        }
        else if (supported && verticalInput <= 0.1f && groundIgnoreTimer <= 0f)
        {
            if (TryEnterGroundedState(out float lift)) postureLift += lift;
        }

        return postureLift;
    }

    private void EnterSwimmingState(bool takingOff)
    {
        currentState = MovementState.Swimming;
        controller.stepOffset = 0f;
        controller.height = swimmingHeight;
        isGrounded = false;
        timeSinceGrounded = 0f;
        if (!takingOff) return;
        groundIgnoreTimer = takeoffGroundDelay;
        velocity.y = takeoffSpeed;
    }

    private bool TryEnterGroundedState(out float lift)
    {
        lift = 0f;
        if (currentState == MovementState.Grounded) return true;
        float rise = Mathf.Max(0f, (standingHeight - controller.height) * 0.5f);
        if (!CanOccupyStandingShape(Vector3.up * rise)) return false;

        // Keep the capsule's feet at the same height when expanding from swimming.
        // The full expanded volume was checked above, including the headroom.
        controller.enabled = false;
        transform.position += Vector3.up * rise;
        controller.height = standingHeight;
        controller.enabled = true;
        controller.stepOffset = Mathf.Min(maximumStepHeight, standingHeight - 0.001f);
        currentState = MovementState.Grounded;
        isGrounded = true;
        timeSinceGrounded = 0f;
        velocity.y = 0f;
        lift = rise;
        return true;
    }

    private bool IsObstacle(Collider obstacle)
    {
        if (obstacle == null || obstacle == controller || obstacle.transform.IsChildOf(transform)) return false;
        if (Physics.GetIgnoreLayerCollision(gameObject.layer, obstacle.gameObject.layer)) return false;
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

        if (!IsWalkable(nearest.collider, nearest.normal)) return false;
        drop = Mathf.Max(0f, nearest.distance - lift);
        return drop <= distance;
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (IsWalkable(hit.collider, hit.normal)) moveTouchedWalkableGround = true;
        if (currentState != MovementState.Swimming) return;
        float intoSurface = Vector3.Dot(velocity, hit.normal);
        if (intoSurface < 0f) velocity -= hit.normal * intoSurface;
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
        {
            moveInput = moveAction.action.ReadValue<Vector2>();
        }

        horizontalInput = moveInput.x;
        depthInput = moveInput.y;

        verticalInput = 0f;

        if (swimUpAction != null && swimUpAction.action.IsPressed())
        {
            verticalInput += 1f;
        }

        if (swimDownAction != null && swimDownAction.action.IsPressed())
        {
            verticalInput -= 1f;
        }

    }

    private Vector3 GetFacingDirection()
    {
        float vertical = currentState == MovementState.Swimming ? verticalInput : 0f;
        Vector3 direction = new Vector3(horizontalInput, vertical, depthInput);

        if (direction.sqrMagnitude > 0.01f) return direction;

        direction = actualVelocity;
        if (currentState == MovementState.Grounded) direction.y = 0f;

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
        if (visual == null) return;

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

        if (visibleDirection.sqrMagnitude < 0.001f) return;

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
        float swimSpeed = velocity.magnitude;

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
        if (animator == null || animationFrozen == frozen) return;
        animationFrozen = frozen;
        if (frozen)
        {
            previousAnimatorSpeed = animator.speed;
            animator.speed = 0f;
        }
        else animator.speed = previousAnimatorSpeed;
    }

    public void StopImmediately()
    {
        velocity = Vector3.zero;
        actualVelocity = Vector3.zero;
    }

    private void UpdateSpriteFlip()
    {
        if (spriteRenderer == null) return;
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
        moveAction?.action.Disable();
        swimUpAction?.action.Disable();
        swimDownAction?.action.Disable();
    }

    public void TeleportTo(Vector3 position)
    {
        StopImmediately();

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

}