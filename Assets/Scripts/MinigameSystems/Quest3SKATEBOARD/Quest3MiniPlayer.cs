using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class Quest3MiniPlayer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform playArea;
    [SerializeField] private RectTransform hitbox;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 320f;
    [SerializeField] private float acceleration = 1600f;
    [SerializeField] private float deceleration = 2000f;
    [SerializeField] private float backwardSpeedMultiplier = 1.4f;
    [SerializeField] private float turnAcceleration = 3000f;

    [Header("Swimming")]
    [SerializeField] private Image swimImage;
    [SerializeField] private Sprite[] swimFrames;
    [SerializeField] private float swimFramesPerSecond = 10f;
    [SerializeField] private bool alwaysFaceRight = true;

    [Header("Hitbox")]
    [SerializeField] private Vector2 hitboxSize = new Vector2(55f, 30f);

    private RectTransform rectTransform;
    private Vector2 startPosition;
    private Vector3 startScale;
    private Vector2 currentVelocity;

    private float animationTime;

    private bool movementEnabled = false;
    private bool facingRight = true;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        startPosition = rectTransform.anchoredPosition;
        startScale = rectTransform.localScale;
        facingRight = startScale.x >= 0f;

        if (swimImage == null)
            swimImage = GetComponentInChildren<Image>(true);

        if (hitbox != null)
            hitbox.sizeDelta = hitboxSize;
    }

    void Update()
    {
        if (!movementEnabled)
            return;

        UpdateSwimming();

        Keyboard keyboard = Keyboard.current;

        Vector2 input = Vector2.zero;

        if (keyboard != null && (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed))
            input.y += 1f;

        if (keyboard != null && (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed))
            input.y -= 1f;

        if (keyboard != null && (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed))
            input.x -= 1f;

        if (keyboard != null && (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed))
            input.x += 1f;

        if (input.sqrMagnitude > 1f)
            input.Normalize();

        if (alwaysFaceRight || input.x > 0f)
            SetFacingDirection(true);
        else if (input.x < 0f)
            SetFacingDirection(false);

        Vector2 targetVelocity = input * moveSpeed;
        float movementRate = input.sqrMagnitude > 0f ? acceleration : deceleration;

        if (input.x < 0f)
            targetVelocity.x *= Mathf.Max(0f, backwardSpeedMultiplier);

        if (Vector2.Dot(currentVelocity, targetVelocity) < 0f)
            movementRate = Mathf.Max(movementRate, turnAcceleration);

        currentVelocity = Vector2.MoveTowards(currentVelocity, targetVelocity, movementRate * Time.unscaledDeltaTime);
        rectTransform.anchoredPosition += currentVelocity * Time.unscaledDeltaTime;

        ClampToPlayArea();
    }

    private void UpdateSwimming()
    {
        if (swimImage == null || swimFrames == null || swimFrames.Length == 0)
            return;

        animationTime = Mathf.Repeat(animationTime + Time.unscaledDeltaTime * Mathf.Max(1f, swimFramesPerSecond), swimFrames.Length);
        swimImage.sprite = swimFrames[Mathf.FloorToInt(animationTime)];
    }

    private void SetFacingDirection(bool right)
    {
        if (facingRight == right)
            return;

        facingRight = right;

        float xScale = Mathf.Abs(startScale.x) * (facingRight ? 1f : -1f);
        rectTransform.localScale = new Vector3(xScale, startScale.y, startScale.z);
    }

    private void ClampToPlayArea()
    {
        Vector2 position = rectTransform.anchoredPosition;
        Vector2 originalPosition = position;

        float halfWidth = rectTransform.rect.width * Mathf.Abs(rectTransform.localScale.x) * 0.5f;
        float halfHeight = rectTransform.rect.height * Mathf.Abs(rectTransform.localScale.y) * 0.5f;
        float minimumX = playArea.rect.xMin + halfWidth;
        float maximumX = playArea.rect.xMax - halfWidth;
        float minimumY = playArea.rect.yMin + halfHeight;
        float maximumY = playArea.rect.yMax - halfHeight;

        position.x = Mathf.Clamp(position.x, minimumX, maximumX);
        position.y = Mathf.Clamp(position.y, minimumY, maximumY);

        if (position.x != originalPosition.x)
            currentVelocity.x = 0f;

        if (position.y != originalPosition.y)
            currentVelocity.y = 0f;

        rectTransform.anchoredPosition = position;
    }

    public void PauseMinigame()
    {
        movementEnabled = false;
        currentVelocity = Vector2.zero;
    }

    public void ResumeMinigame()
    {
        movementEnabled = true;
    }

    public void BeginMinigame()
    {
        ResetPosition();
        movementEnabled = true;
    }

    public void StopMinigame()
    {
        movementEnabled = false;
        currentVelocity = Vector2.zero;
    }

    public void ResetPosition()
    {
        animationTime = 0f;
        SetFacingDirection(true);

        if (swimImage != null && swimFrames != null && swimFrames.Length > 0)
            swimImage.sprite = swimFrames[0];

        rectTransform.anchoredPosition = startPosition;
        currentVelocity = Vector2.zero;
    }

    public RectTransform GetHitbox()
    {
        return hitbox != null ? hitbox : rectTransform;
    }
}
