using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
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
    [SerializeField] private float rotationOffset = -90f;
    [SerializeField] private float swimRotationSpeed = 300f;
    [SerializeField] private float swimIdleRotation = 0f;
    [SerializeField] private float swimIdleRotationSpeed = 200f;
    [SerializeField] private float swimIdleSpeedThreshold = 0.15f;

    [Header("Ground Detection")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float groundCheckDistance = 0.1f;
    [SerializeField] private float groundCheckWidth = 0.8f;
    [SerializeField] private float groundGraceTime = 0.08f;

    private Rigidbody rb;
    private CapsuleCollider capsule;

    private MovementState currentState;

    private float horizontalInput;
    private float depthInput;
    private float verticalInput;

    private bool isGrounded;
    private float timeSinceGrounded;

    private bool movementEnabled = true;

    private Vector2 lastSwimDirection = Vector2.up;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();

        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;
    }

    private void Start()
    {
        isGrounded = CheckGrounded();

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
        ReadInput();
        UpdateSwimmingRotation();

        if (PauseController.IsGamePaused)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        UpdateSpriteFlip();
        UpdateAnimator();
    }

    private void FixedUpdate()
    {

        UpdateGroundCheck();
        UpdateMovementState();

        if (!movementEnabled)
        {
            return;
        }

        if (currentState == MovementState.Grounded)
        {
            GroundMovement();
        }
        else
        {
            SwimMovement();
        }
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
        if (currentState == MovementState.Grounded)
            return;

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
        if (visual == null)
        {
            return;
        }

        float targetRotation;
        float rotationSpeed;

        if (currentState == MovementState.Grounded)
        {
            targetRotation = 0f;
            rotationSpeed = swimIdleRotationSpeed;
        }
        else
        {
            Vector2 visibleInput = new Vector2(horizontalInput, verticalInput);
            Vector2 visibleVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y);

            bool hasInput = visibleInput.sqrMagnitude > 0.01f;
            bool isStillMoving = visibleVelocity.magnitude > swimIdleSpeedThreshold;

            if (hasInput || isStillMoving)
            {
                Vector2 direction;

                if (hasInput)
                    direction = visibleInput;
                else
                    direction = visibleVelocity;

                if (direction.sqrMagnitude > 0.01f)
                    lastSwimDirection = direction.normalized;

                targetRotation = Mathf.Atan2(lastSwimDirection.y, lastSwimDirection.x) * Mathf.Rad2Deg + rotationOffset;
                rotationSpeed = swimRotationSpeed;
            }
            else
            {
                targetRotation = swimIdleRotation;
                rotationSpeed = swimIdleRotationSpeed;
            }
        }

        float currentRotation = visual.localEulerAngles.z;
        float newRotation = Mathf.MoveTowardsAngle(currentRotation, targetRotation, rotationSpeed * Time.deltaTime);

        visual.localRotation = Quaternion.Euler(0f, 0f, newRotation);
    }

    private void UpdateAnimator()
    {
        if (animator == null)
        {
            return;
        }

        Vector3 velocity = rb.linearVelocity;

        float groundSpeed = new Vector2(velocity.x, velocity.z).magnitude;
        float swimSpeed = velocity.magnitude;

        animator.SetFloat("GroundSpeed", groundSpeed);
        animator.SetFloat("SwimSpeed", swimSpeed);
        animator.SetBool("IsSwimming", currentState == MovementState.Swimming);
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
        }
    }

    public void StopImmediately()
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    private void UpdateSpriteFlip()
    {
        if (spriteRenderer == null)
            return;

        if (horizontalInput < -0.01f)
            spriteRenderer.flipX = true;

        if (horizontalInput > 0.01f)
            spriteRenderer.flipX = false;
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
        CapsuleCollider currentCapsule = GetComponent<CapsuleCollider>();

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