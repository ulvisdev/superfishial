using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovementBACKUP : MonoBehaviour
{
    private enum MovementState
    {
        Grounded,
        Swimming
    }

    [Header("Input")]
    [SerializeField] private InputActionReference moveAction;

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

    [Tooltip(
        "-90 if the top of the sprite is its forward direction. " +
        "Use 0 if the sprite naturally faces right."
        )]

    [SerializeField] private float rotationOffset = -90f;

    [SerializeField, Range(0f, 1f)]
    private float takeoffInputThreshold = 0.25f;

    [Header("Ground Detection")]
    [SerializeField]
    private Vector2 groundCheckSize =
        new Vector2(0.6f, 0.15f);

    [SerializeField] private float groundCheckDistance = 0.55f;
    [SerializeField] private LayerMask groundLayer;

    [Header("Animation")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Animator animator;

    private Rigidbody2D rb;
    private Vector2 moveInput;
    private Vector2 lastSwimDirection = Vector2.up;

    private MovementState movementState;
    private bool isGrounded;

    private static readonly int GroundSpeedHash = Animator.StringToHash("GroundSpeed");
    private static readonly int SwimSpeedHash = Animator.StringToHash("SwimSpeed");
    private static readonly int MoveXHash = Animator.StringToHash("MoveX");
    private static readonly int MoveYHash = Animator.StringToHash("MoveY");
    private static readonly int SwimmingHash = Animator.StringToHash("IsSwimming");
    private static readonly int GroundedHash = Animator.StringToHash("IsGrounded");

    [Header("Swim Idle")]
    [SerializeField] private float swimIdleSpeedThreshold = 0.15f;
    [SerializeField] private float swimIdleRotation = 0f;
    [SerializeField] private float activeSwimRotationSpeed = 540f;
    [SerializeField] private float idleRotationSpeed = 180f;

    [Header("Ground Stability")]
    [SerializeField] private float groundGraceTime = 0.12f;
    private float groundGraceTimer;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        rb.freezeRotation = false;
        rb.angularVelocity = 0f;

        movementState = MovementState.Grounded;
    }

    private void OnEnable()
    {
        if (moveAction != null)
            moveAction.action.Enable();
    }

    private void OnDisable()
    {
        if (moveAction != null)
            moveAction.action.Disable();
    }

    private void Update()
    {
        if (PauseController.IsGamePaused)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }
        if (moveAction != null)
        {
            moveInput = Vector2.ClampMagnitude(moveAction.action.ReadValue<Vector2>(), 1f);
        }

        UpdateSpriteFlip();
        UpdateAnimator();
    }

    private void FixedUpdate()
    {
        CheckGround();

        switch (movementState)
        {
            case MovementState.Grounded:
                HandleGroundedMovement();
                break;

            case MovementState.Swimming:
                HandleSwimmingMovement();
                break;
        }

        UpdateBodyRotation();
    }

    private void HandleGroundedMovement()
    {
        rb.gravityScale = groundedGravity;

        if (!isGrounded)
        {
            EnterSwimmingState();
            return;
        }

        if (moveInput.y > takeoffInputThreshold)
        {
            EnterSwimmingState();

            Vector2 velocity = rb.linearVelocity;
            velocity.y = Mathf.Max(velocity.y, takeoffSpeed);
            rb.linearVelocity = velocity;

            return;
        }

        float targetSpeed = moveInput.x * walkSpeed;
        float acceleration = Mathf.Abs(moveInput.x) > 0.01f ? walkAcceleration : walkDeceleration;

        float horizontalSpeed = Mathf.MoveTowards(rb.linearVelocity.x, targetSpeed, acceleration * Time.fixedDeltaTime);
        rb.linearVelocity = new Vector2(horizontalSpeed, rb.linearVelocity.y);
    }

    private void HandleSwimmingMovement()
    {
        rb.gravityScale = 0f;
        Vector2 targetVelocity = new Vector2(moveInput.x * swimHorizontalSpeed, moveInput.y * swimVerticalSpeed);

        float acceleration = moveInput.sqrMagnitude > 0.01f ? swimAcceleration : swimDeceleration;
        rb.linearVelocity = Vector2.MoveTowards(rb.linearVelocity, targetVelocity, acceleration * Time.fixedDeltaTime);

        bool tryingToMoveDown = moveInput.y < -0.05f;
        bool fallingOntoGround = rb.linearVelocity.y <= 0f;

        if (isGrounded && (tryingToMoveDown || fallingOntoGround))
        {
            EnterGroundedState();
            return;
        }
    }

    private void EnterSwimmingState()
    {
        movementState = MovementState.Swimming;
        rb.gravityScale = 0f;
    }

    private void EnterGroundedState()
    {
        // movementState = MovementState.Grounded;
        // rb.gravityScale = groundedGravity;
        // rb.linearVelocity = new Vector2(rb.linearVelocity.x, Mathf.Min(rb.linearVelocity.y, 0f));

        if (movementState == MovementState.Grounded)
            return;

        movementState = MovementState.Grounded;
        rb.gravityScale = groundedGravity;
        rb.angularVelocity = 0f;

        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);

        groundGraceTimer = groundGraceTime;
        isGrounded = true;
    }

    private void UpdateBodyRotation()
    {
        rb.angularVelocity = 0f;

        float targetAngle;
        float currentRotationSpeed;

        if (movementState == MovementState.Swimming)
        {
            bool hasMovementInput = moveInput.sqrMagnitude > 0.01f;
            bool isStillMoving = rb.linearVelocity.magnitude > swimIdleSpeedThreshold;
            bool isActivelySwimming = hasMovementInput || isStillMoving;

            if (isActivelySwimming)
            {
                Vector2 direction;

                if (hasMovementInput)
                {
                    direction = new Vector2(moveInput.x * swimHorizontalSpeed, moveInput.y * swimVerticalSpeed);
                }
                else
                {
                    direction = rb.linearVelocity;
                }

                if (direction.sqrMagnitude > 0.01f)
                {
                    lastSwimDirection = direction.normalized;
                }

                targetAngle = Mathf.Atan2(lastSwimDirection.y, lastSwimDirection.x) * Mathf.Rad2Deg + rotationOffset;
                currentRotationSpeed = activeSwimRotationSpeed;
            }
            else
            {
                targetAngle = swimIdleRotation;
                currentRotationSpeed = idleRotationSpeed;
            }
        }
        else
        {
            targetAngle = 0f;
            currentRotationSpeed = activeSwimRotationSpeed;
        }

        float newAngle = Mathf.MoveTowardsAngle(rb.rotation, targetAngle, currentRotationSpeed * Time.fixedDeltaTime);
        rb.MoveRotation(newAngle);
    }

    private void CheckGround()
    {
        Vector2 checkPosition = rb.position + Vector2.down * groundCheckDistance;
        bool groundDetected = Physics2D.OverlapBox(checkPosition, groundCheckSize, 0f, groundLayer) != null;

        if (groundDetected)
            groundGraceTimer = groundGraceTime;
        else
            groundGraceTimer -= Time.fixedDeltaTime;

        isGrounded = groundGraceTimer > 0f;
    }

    private void UpdateSpriteFlip()
    {
        if (spriteRenderer == null)
            return;

        if (movementState == MovementState.Swimming)
        {
            spriteRenderer.flipX = false;
            return;
        }

        if (moveInput.x > 0.05f)
        {
            spriteRenderer.flipX = false;
        }
        else if (moveInput.x < -0.05f)
        {
            spriteRenderer.flipX = true;
        }
    }

    private void UpdateAnimator()
    {
        if (animator == null)
            return;

        float groundSpeed = Mathf.Abs(rb.linearVelocity.x);
        float swimSpeed = rb.linearVelocity.magnitude;

        if (groundSpeed < 0.05f)
            groundSpeed = 0f;

        if (swimSpeed < 0.05f)
            swimSpeed = 0f;

        animator.SetFloat(GroundSpeedHash, groundSpeed);
        animator.SetFloat(SwimSpeedHash, swimSpeed);

        animator.SetFloat(MoveXHash, moveInput.x);
        animator.SetFloat(MoveYHash, moveInput.y);

        animator.SetBool(SwimmingHash, movementState == MovementState.Swimming);
        animator.SetBool(GroundedHash, isGrounded);
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 checkPosition = transform.position + Vector3.down * groundCheckDistance;

        Gizmos.DrawWireCube(checkPosition, groundCheckSize);
    }
}