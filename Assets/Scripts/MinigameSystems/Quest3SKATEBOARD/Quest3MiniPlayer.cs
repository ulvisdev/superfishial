using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class Quest3MiniPlayer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform playArea;
    [SerializeField] private RectTransform hitbox;
    [SerializeField] private Animator animator;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 300f;

    [Header("Hitbox")]
    [SerializeField] private Vector2 idleHitboxSize = new Vector2(35f, 125f);
    [SerializeField] private Vector2 swimHitboxSize = new Vector2(125f, 35f);

    private RectTransform rectTransform;
    private Vector2 startPosition;
    private Vector3 startScale;

    private bool movementEnabled = false;
    private bool isSwimming = false;
    private bool facingRight = true;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        startPosition = rectTransform.anchoredPosition;
        startScale = rectTransform.localScale;

        UpdateVisuals(false);
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

        bool swimmingNow = input.sqrMagnitude > 0f;

        if (input.x > 0f)
            SetFacingDirection(true);
        else if (input.x < 0f)
            SetFacingDirection(false);

        if (swimmingNow != isSwimming)
            UpdateVisuals(swimmingNow);

        rectTransform.anchoredPosition += input * moveSpeed * Time.unscaledDeltaTime;

        ClampToPlayArea();
    }

    private void UpdateVisuals(bool swimming)
    {
        isSwimming = swimming;

        if (animator != null)
            animator.SetBool("isSwimming", swimming);

        if (hitbox != null)
            hitbox.sizeDelta = swimming ? swimHitboxSize : idleHitboxSize;
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

        float halfWidth = rectTransform.rect.width * 0.5f;
        float halfHeight = rectTransform.rect.height * 0.5f;

        float minimumX = playArea.rect.xMin + halfWidth;
        float maximumX = playArea.rect.xMax - halfWidth;
        float minimumY = playArea.rect.yMin + halfHeight;
        float maximumY = playArea.rect.yMax - halfHeight;

        position.x = Mathf.Clamp(position.x, minimumX, maximumX);
        position.y = Mathf.Clamp(position.y, minimumY, maximumY);

        rectTransform.anchoredPosition = position;
    }

    public void BeginMinigame()
    {
        rectTransform.anchoredPosition = startPosition;
        movementEnabled = true;
        UpdateVisuals(false);
    }

    public void StopMinigame()
    {
        movementEnabled = false;
        UpdateVisuals(false);
    }

    public RectTransform GetHitbox()
    {
        return hitbox;
    }
}