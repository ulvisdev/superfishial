using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
// [RequireComponent(typeof(CapsuleCollider))]
public class PlayerMovement : MonoBehaviour
{
    private enum MovementState
    {
        Grounded,
        Swimming
    }

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

    [Header("Swimming")]
    [SerializeField] private float swimHorizontalSpeed = 5f;
    [SerializeField] private float swimVerticalSpeed = 4f;
    [SerializeField] private float swimAcceleration = 14f;
    [SerializeField] private float swimDeceleration = 20f;
    [SerializeField] private float takeoffSpeed = 2.5f;

    [Header("Swimming Rotation")]
    // [SerializeField] private float rotationOffset = -90f;
    [SerializeField] private float swimRotationSpeed = 300f;
    // [SerializeField] private float swimIdleRotation = 0f;
    [SerializeField] private float swimIdleRotationSpeed = 200f;
    [SerializeField] private float swimIdleSpeedThreshold = 0.15f;

    [Header("Ground Detection")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float groundCheckDistance = 0.1f;
    [SerializeField] private float groundCheckWidth = 0.8f;
    [SerializeField] private float groundGraceTime = 0.08f;

    private Rigidbody rb;

    [Header("Body Collider")]
    [SerializeField] private CapsuleCollider capsule;
    [SerializeField] private float colliderRotationSpeed = 300f;

    private int facingDirection;
    private bool facingLeft;

    private float swimHeading;
    private bool swimHeadingActive;

    private MovementState currentState;

    private float horizontalInput;
    private float depthInput;
    private float verticalInput;

    private bool isGrounded;
    private float timeSinceGrounded;

    private bool movementEnabled = true;
    private bool preserveAnimationAfterUnfreeze = false;
    private float previousAnimatorSpeed = 1f;
    private bool snapColliderOnReversal;

    //private Vector2 lastSwimDirection = Vector2.up;

    [Header("Standing Clearance")]
    [SerializeField] private LayerMask standingObstacleLayers = ~0;
    [SerializeField] private float standingClearanceTolerance = 0.01f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (capsule == null)
            capsule = GetComponentInChildren<CapsuleCollider>();

        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;
    }

    private void Start()
    {
        isGrounded = CheckGrounded() && CanStandUpright();

        if (isGrounded)
        {
            currentState = MovementState.Grounded;
        }
        else
        {
            currentState = MovementState.Swimming;
        }
    }

    private void Update()
    {
        if (!movementEnabled || PauseController.IsGamePaused)
        {
            rb.linearVelocity = Vector3.zero;
            return;
        }

        ReadInput();
        UpdateFacingDirection();
        UpdateSwimmingRotation();
        UpdateSpriteFlip();
        UpdateAnimator();
    }

    private void FixedUpdate()
    {
        if (!movementEnabled || PauseController.IsGamePaused)
        {
            rb.linearVelocity = Vector3.zero;
            return;
        }

        UpdateGroundCheck();
        UpdateMovementState();

        if (currentState == MovementState.Grounded)
            GroundMovement();
        else
            SwimMovement();

        UpdateBodyColliderRotation();
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

    private bool CanStandUpright()
    {
        if (capsule == null) return false;

        Transform body = capsule.transform;
        Vector3 scale = body.lossyScale;

        float radius = capsule.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        float height = Mathf.Max(capsule.height * Mathf.Abs(scale.y), radius * 2f);
        float halfSegment = height * 0.5f - radius;

        Quaternion uprightRotation = body.parent != null ? body.parent.rotation : Quaternion.identity;
        Vector3 uprightAxis = uprightRotation * Vector3.up;
        Vector3 uprightCentre = body.position + uprightRotation * Vector3.Scale(capsule.center, scale);

        float uprightBottom = uprightCentre.y - radius - halfSegment * Mathf.Abs(uprightAxis.y);
        float lift = Mathf.Max(0f, GetCapsuleBottomY() - uprightBottom);

        uprightCentre += Vector3.up * lift;

        Vector3 bottomPoint = uprightCentre - uprightAxis * halfSegment;
        Vector3 topPoint = uprightCentre + uprightAxis * halfSegment;
        float checkRadius = Mathf.Max(0.001f, radius - Mathf.Clamp(standingClearanceTolerance, 0f, radius * 0.1f));

        Collider[] obstacles = Physics.OverlapCapsule(bottomPoint, topPoint, checkRadius, standingObstacleLayers, QueryTriggerInteraction.Ignore);

        foreach (Collider obstacle in obstacles)
        {
            if (obstacle.attachedRigidbody == rb) continue;
            if (obstacle.transform.IsChildOf(transform)) continue;
            if (Physics.GetIgnoreLayerCollision(capsule.gameObject.layer, obstacle.gameObject.layer)) continue;
            if (Physics.GetIgnoreCollision(capsule, obstacle)) continue;

            return false;
        }

        return true;
    }

    private Vector3 GetFacingDirection()
    {
        float vertical = currentState == MovementState.Swimming ? verticalInput : 0f;
        Vector3 direction = new Vector3(horizontalInput, vertical, depthInput);

        if (direction.sqrMagnitude > 0.01f) return direction;

        direction = rb.linearVelocity;
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

    private void UpdateBodyColliderRotation()
    {
        if (capsule == null) return;

        if (currentState == MovementState.Grounded)
        {
            capsule.transform.localRotation = Quaternion.identity;
            snapColliderOnReversal = false;
            return;
        }

        if (facingDirection == 0 && visual != null)
        {
            capsule.transform.localRotation = visual.localRotation * Quaternion.Euler(0f, 0f, 90f);
            snapColliderOnReversal = false;
            return;
        }

        Vector3 direction = GetFacingDirection();
        if (direction.sqrMagnitude < 0.001f) return;

        Vector3 localDirection = transform.InverseTransformDirection(direction.normalized);
        Quaternion targetRotation = Quaternion.FromToRotation(Vector3.up, localDirection);

        capsule.transform.localRotation = Quaternion.RotateTowards(capsule.transform.localRotation, targetRotation, colliderRotationSpeed * Time.fixedDeltaTime);
        snapColliderOnReversal = false;
    }

    private void GroundMovement()
    {
        Vector3 currentVelocity = rb.linearVelocity;

        Vector3 targetVelocity = new Vector3(horizontalInput * walkSpeed, currentVelocity.y, depthInput * walkSpeed);

        float movementRate = Mathf.Abs(horizontalInput) > 0.01f || Mathf.Abs(depthInput) > 0.01f ? walkAcceleration : walkDeceleration;
        currentVelocity.x = Mathf.MoveTowards(currentVelocity.x, targetVelocity.x, movementRate * Time.fixedDeltaTime);
        currentVelocity.z = Mathf.MoveTowards(currentVelocity.z, targetVelocity.z, movementRate * Time.fixedDeltaTime);
        currentVelocity.y += Physics.gravity.y * groundedGravity * Time.fixedDeltaTime;

        rb.linearVelocity = currentVelocity;
    }

    private void SwimMovement()
    {
        Vector3 inputDirection = new Vector3(horizontalInput, verticalInput, depthInput);

        if (inputDirection.magnitude > 1f)
            inputDirection.Normalize();

        Vector3 targetVelocity = new Vector3(inputDirection.x * swimHorizontalSpeed, inputDirection.y * swimVerticalSpeed, inputDirection.z * swimHorizontalSpeed);

        float movementRate = inputDirection.magnitude > 0.01f ? swimAcceleration : swimDeceleration;
        rb.linearVelocity = Vector3.MoveTowards(rb.linearVelocity, targetVelocity, movementRate * Time.fixedDeltaTime);
    }

    private void UpdateMovementState()
    {
        if (currentState == MovementState.Grounded)
        {
            if (verticalInput > 0.1f)
            {
                StartSwimming();
                return;
            }

            if (timeSinceGrounded > groundGraceTime)
            {
                currentState = MovementState.Swimming;
                return;
            }
        }

        if (currentState == MovementState.Swimming)
        {
            bool tryingToMoveDown = verticalInput < -0.05f;
            bool fallingOntoGround = rb.linearVelocity.y <= 0.1f;

            if (isGrounded && (tryingToMoveDown || fallingOntoGround))
                EnterGroundedState();
        }
    }

    private void EnterGroundedState()
    {
        if (currentState == MovementState.Grounded) return;
        if (!CanStandUpright()) return;

        SnapUprightForLanding();
        currentState = MovementState.Grounded;

        Vector3 velocity = rb.linearVelocity;
        velocity.y = 0f;
        rb.linearVelocity = velocity;

        timeSinceGrounded = 0f;
        isGrounded = true;
    }

    private void StartSwimming()
    {
        currentState = MovementState.Swimming;

        Vector3 velocity = rb.linearVelocity;
        velocity.y = takeoffSpeed;
        rb.linearVelocity = velocity;
    }

    private void UpdateGroundCheck()
    {
        isGrounded = CheckGrounded();

        if (isGrounded)
            timeSinceGrounded = 0f;
        else
            timeSinceGrounded += Time.fixedDeltaTime;
    }

    private bool CheckGrounded()
    {
        Bounds bounds = capsule.bounds;

        Vector3 checkPosition = new Vector3(bounds.center.x, bounds.min.y - groundCheckDistance / 2f, bounds.center.z);
        Vector3 checkSize = new Vector3(bounds.size.x * groundCheckWidth, groundCheckDistance, bounds.size.z * groundCheckWidth);

        return Physics.CheckBox(checkPosition, checkSize / 2f, Quaternion.identity, groundLayer, QueryTriggerInteraction.Ignore);
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
            snapColliderOnReversal = true;
        }

        swimHeading = Mathf.MoveTowardsAngle(swimHeading, targetHeading, swimRotationSpeed * Time.deltaTime);

        float horizontalHeading = Mathf.Cos(swimHeading * Mathf.Deg2Rad);

        if (horizontalHeading < -0.001f) facingLeft = true;
        if (horizontalHeading > 0.001f) facingLeft = false;

        float spriteRotation = swimHeading - (facingLeft ? 180f : 0f);
        visual.localRotation = Quaternion.Euler(0f, 0f, spriteRotation);
    }

    private float GetCapsuleBottomY()
    {
        Transform body = capsule.transform;
        Vector3 scale = body.lossyScale;

        float radius = capsule.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        float height = Mathf.Max(capsule.height * Mathf.Abs(scale.y), radius * 2f);
        float halfSegment = height * 0.5f - radius;

        Vector3 centre = body.TransformPoint(capsule.center);
        float verticalExtent = radius + halfSegment * Mathf.Abs(body.up.y);

        return centre.y - verticalExtent;
    }

    private void SnapUprightForLanding()
    {
        if (capsule != null)
        {
            float previousBottom = GetCapsuleBottomY();

            capsule.transform.localRotation = Quaternion.identity;

            float uprightBottom = GetCapsuleBottomY();
            float lift = Mathf.Max(0f, previousBottom - uprightBottom);

            if (lift > 0f) rb.position += Vector3.up * lift;
        }

        // if (visual != null) visual.localRotation = Quaternion.identity;
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
                relevantSpeed = new Vector2(rb.linearVelocity.x, rb.linearVelocity.z).magnitude;
            else
                relevantSpeed = rb.linearVelocity.magnitude;

            if (hasInput && relevantSpeed < 0.01f)
                return;

            preserveAnimationAfterUnfreeze = false;
        }

        Vector3 velocity = rb.linearVelocity;

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

        if (!movementEnabled)
        {
            horizontalInput = 0f;
            depthInput = 0f;
            verticalInput = 0f;

            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        else
            preserveAnimationAfterUnfreeze = true;
    }

    public void SetAnimationFrozen(bool frozen)
    {
        if (animator == null)
            return;

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
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
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

    private void OnDrawGizmosSelected()
    {
        CapsuleCollider currentCapsule = capsule != null ? capsule : GetComponentInChildren<CapsuleCollider>();

        if (currentCapsule == null)
        {
            return;
        }

        Bounds bounds = currentCapsule.bounds;

        Vector3 checkPosition = new Vector3(bounds.center.x, bounds.min.y - groundCheckDistance / 2f, bounds.center.z);
        Vector3 checkSize = new Vector3(bounds.size.x * groundCheckWidth, groundCheckDistance, bounds.size.z * groundCheckWidth);

        Gizmos.DrawWireCube(checkPosition, checkSize);
    }
}