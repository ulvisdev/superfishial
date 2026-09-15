using UnityEngine;

public class Quest3MinigameSkateboard : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform playArea;
    [SerializeField] private RectTransform hitbox;
    [SerializeField] private Quest3MiniPlayer miniPlayer;
    [SerializeField] private Quest3MinigameManager minigameManager;

    [Header("Entrance")]
    [SerializeField] private float appearDelay = 1.5f;
    [SerializeField] private float offscreenPadding = 100f;
    [SerializeField] private float entranceSpeed = 250f;

    [Header("Movement")]
    [SerializeField] private float driftSpeed = 25f;
    [SerializeField] private float leftStopPadding = 80f;

    [Header("Obstacle Push")]
    [SerializeField] private float pushSpeed = 250f;
    [SerializeField] private float pushAcceleration = 1200f;
    [SerializeField] private float pushDeceleration = 500f;
    [SerializeField] private float pushHoldDuration = 0.18f;
    [SerializeField] private float pushInvulnerabilityDuration = 0.25f;
    [SerializeField] private float verticalEdgePadding = 40f;

    [Header("Floating")]
    [SerializeField] private float bobAmount = 10f;
    [SerializeField] private float bobSpeed = 2.5f;
    [SerializeField] private float wobbleAngle = 8f;
    [SerializeField] private float wobbleSpeed = 2f;

    private RectTransform rectTransform;
    private Vector2 settledPosition;
    private Vector3 startRotation;

    private float appearTimer;
    private float baseY;
    private float animationTime;
    private float verticalVelocity;
    private float pushDirection;
    private float pushTimer;
    private float pushInvulnerabilityTimer;

    private bool running = false;
    private bool settled = false;
    private bool catchable = false;
    private bool caught = false;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        settledPosition = rectTransform.anchoredPosition;
        startRotation = rectTransform.localEulerAngles;
        baseY = settledPosition.y;
    }

    void Update()
    {
        if (!running || caught)
            return;

        float deltaTime = Time.unscaledDeltaTime;

        if (pushInvulnerabilityTimer > 0f)
            pushInvulnerabilityTimer -= deltaTime;

        animationTime += deltaTime;

        if (!settled)
        {
            appearTimer += deltaTime;

            if (appearTimer < appearDelay)
                return;

            MoveIntoScreen(deltaTime);
            UpdateFloating();
            return;
        }

        UpdatePushMovement(deltaTime);
        UpdateDrift(deltaTime);
        UpdateFloating();

        if (catchable && RectsOverlap(miniPlayer.GetHitbox(), hitbox))
            CatchSkateboard();
    }

    public void PauseForReset()
    {
        running = false;
    }

    private void MoveIntoScreen(float deltaTime)
    {
        Vector2 position = rectTransform.anchoredPosition;
        position.x = Mathf.MoveTowards(position.x, settledPosition.x, entranceSpeed * deltaTime);
        rectTransform.anchoredPosition = position;

        if (Mathf.Abs(position.x - settledPosition.x) > 0.1f)
            return;

        settled = true;
        catchable = true;
        baseY = settledPosition.y;
    }

    private void UpdateDrift(float deltaTime)
    {
        Vector2 position = rectTransform.anchoredPosition;

        float minimumX = playArea.rect.xMin + rectTransform.rect.width * 0.5f + leftStopPadding;
        position.x = Mathf.MoveTowards(position.x, minimumX, driftSpeed * deltaTime);

        rectTransform.anchoredPosition = position;
    }

    private void UpdatePushMovement(float deltaTime)
    {
        float targetVelocity = 0f;

        if (pushTimer > 0f)
        {
            pushTimer -= deltaTime;
            targetVelocity = pushDirection * pushSpeed;
        }

        float rate = pushTimer > 0f ? pushAcceleration : pushDeceleration;
        verticalVelocity = Mathf.MoveTowards(verticalVelocity, targetVelocity, rate * deltaTime);

        baseY += verticalVelocity * deltaTime;

        float halfHeight = rectTransform.rect.height * 0.5f;
        float minimumY = playArea.rect.yMin + halfHeight + verticalEdgePadding;
        float maximumY = playArea.rect.yMax - halfHeight - verticalEdgePadding;

        baseY = Mathf.Clamp(baseY, minimumY, maximumY);

        if (baseY <= minimumY && verticalVelocity < 0f)
            verticalVelocity = 0f;

        if (baseY >= maximumY && verticalVelocity > 0f)
            verticalVelocity = 0f;
    }

    private void UpdateFloating()
    {
        Vector2 position = rectTransform.anchoredPosition;

        if (settled)
            position.y = baseY + Mathf.Sin(animationTime * bobSpeed) * bobAmount;

        rectTransform.anchoredPosition = position;

        float wobble = Mathf.Sin(animationTime * wobbleSpeed) * wobbleAngle;
        rectTransform.localEulerAngles = startRotation + new Vector3(0f, 0f, wobble);
    }

    public void PushFromObstacle(RectTransform obstacle)
    {
        if (!CanBePushed())
            return;

        pushDirection = obstacle.position.y <= rectTransform.position.y ? 1f : -1f;
        pushTimer = pushHoldDuration;
        pushInvulnerabilityTimer = pushInvulnerabilityDuration;

        Debug.Log("Skateboard pushed: " + pushDirection);
    }

    public bool CanBePushed()
    {
        return running && settled && !caught && pushInvulnerabilityTimer <= 0f;
    }

    public RectTransform GetHitbox()
    {
        return hitbox;
    }

    public void BeginMinigame()
    {
        ResetTarget();
    }

    public void StopMinigame()
    {
        running = false;
    }

    public void ResetTarget()
    {
        running = true;
        settled = false;
        catchable = false;
        caught = false;

        appearTimer = 0f;
        animationTime = 0f;
        verticalVelocity = 0f;
        pushDirection = 0f;
        pushTimer = 0f;
        pushInvulnerabilityTimer = 0f;
        baseY = settledPosition.y;

        float offscreenX = playArea.rect.xMax + rectTransform.rect.width * 0.5f + offscreenPadding;

        rectTransform.anchoredPosition = new Vector2(offscreenX, settledPosition.y);
        rectTransform.localEulerAngles = startRotation;

        gameObject.SetActive(true);
    }

    private void CatchSkateboard()
    {
        if (caught)
            return;

        caught = true;
        running = false;

        if (minigameManager != null)
            minigameManager.CompleteMinigame();
    }

    private bool RectsOverlap(RectTransform first, RectTransform second)
    {
        return GetWorldRect(first).Overlaps(GetWorldRect(second));
    }

    private Rect GetWorldRect(RectTransform rectTransform)
    {
        Vector3[] corners = new Vector3[4];
        rectTransform.GetWorldCorners(corners);

        float minX = corners[0].x;
        float maxX = corners[0].x;
        float minY = corners[0].y;
        float maxY = corners[0].y;

        for (int i = 1; i < 4; i++)
        {
            minX = Mathf.Min(minX, corners[i].x);
            maxX = Mathf.Max(maxX, corners[i].x);
            minY = Mathf.Min(minY, corners[i].y);
            maxY = Mathf.Max(maxY, corners[i].y);
        }

        return new Rect(minX, minY, maxX - minX, maxY - minY);
    }
}