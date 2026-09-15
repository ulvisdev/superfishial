using UnityEngine;
using UnityEngine.InputSystem;

public class Quest3MiniPlayer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform playArea;
    [SerializeField] private RectTransform hitbox;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 300f;
    [SerializeField] private float acceleration = 900f;
    [SerializeField] private float deceleration = 1200f;

    [Header("Hitbox")]
    [SerializeField] private Vector2 hitboxSize = new Vector2(55f, 30f);

    private RectTransform rectTransform;
    private Vector2 startPosition;
    private Vector3 startScale;
    private Vector2 currentVelocity;

    private bool movementEnabled = false;
    private bool facingRight = true;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        startPosition = rectTransform.anchoredPosition;
        startScale = rectTransform.localScale;

        if (hitbox != null)
            hitbox.sizeDelta = hitboxSize;
    }

    void Update()
    {
        if (!movementEnabled)
            return;

        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
            return;

        Vector2 input = Vector2.zero;

        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
            input.y += 1f;

        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
            input.y -= 1f;

        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
            input.x -= 1f;

        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
            input.x += 1f;

        if (input.sqrMagnitude > 1f)
            input.Normalize();

        if (input.x > 0f)
            SetFacingDirection(true);
        else if (input.x < 0f)
            SetFacingDirection(false);

        Vector2 targetVelocity = input * moveSpeed;
        float movementRate = input.sqrMagnitude > 0f ? acceleration : deceleration;

        currentVelocity = Vector2.MoveTowards(currentVelocity, targetVelocity, movementRate * Time.unscaledDeltaTime);
        rectTransform.anchoredPosition += currentVelocity * Time.unscaledDeltaTime;

        ClampToPlayArea();
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

        float halfWidth = rectTransform.rect.width * 0.5f;
        float halfHeight = rectTransform.rect.height * 0.5f;
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
        rectTransform.anchoredPosition = startPosition;
        currentVelocity = Vector2.zero;
        movementEnabled = true;
    }

    public void StopMinigame()
    {
        movementEnabled = false;
        currentVelocity = Vector2.zero;
    }

    public void ResetPosition()
    {
        rectTransform.anchoredPosition = startPosition;
        currentVelocity = Vector2.zero;
    }

    public RectTransform GetHitbox()
    {
        return hitbox;
    }
}