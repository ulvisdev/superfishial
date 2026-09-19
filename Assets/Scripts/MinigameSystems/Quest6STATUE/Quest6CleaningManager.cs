using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public enum Quest6CleaningTool
{
    Sponge,
    Scraper
}

public class Quest6CleaningManager : MonoBehaviour
{
    [Header("Panels")]
    public GameObject instructionsPanel;
    public GameObject minigamePanel;

    bool minigameActive;

    [Header("Zoom")]
    public RectTransform statueViewport;
    public RectTransform statueContent;
    public float zoomScale = 1.3f;
    public float zoomSpeed = 8f;
    [Range(0f, 1f)] public float zoomFollowStrength = 0.35f;

    Vector3 normalScale;
    Vector2 normalPosition;
    Canvas canvas;

    [Header("Tools")]
    public Quest6CleaningTool currentTool = Quest6CleaningTool.Sponge;
    public RectTransform toolCursor;
    public RectTransform toolVisual;
    public RectTransform scraperTip;
    public Image toolCursorImage;
    public Sprite spongeSprite;
    public Sprite scraperSprite;

    RectTransform toolCursorParent;

    public Quest6DirtEraser dirtEraser;
    public float spongeVisualMultiplier = 1f;
    public Vector2 scraperCursorSize = new Vector2(128f, 128f);

    [Header("Sponge Animation")]
    public float spongeShakeDistance = 4f;
    public float spongeMinShakeSpeed = 10f;
    public float spongeMaxShakeSpeed = 28f;
    public float spongeRotationAmount = 5f;
    public float spongeMouseSpeedForMaxAnimation = 20f;
    public float spongeAnimationAcceleration = 8f;
    public float spongeAnimationDeceleration = 5f;

    [Header("Scraper Animation")]
    public float scraperJabDistance = 12f;
    public float scraperJabDuration = 0.12f;
    public Vector2 scraperJabDirection = new Vector2(0f, 1f);

    float spongeAnimationIntensity;
    float spongeAnimationPhase;
    float scraperJabTimer;

    void Start()
    {
        normalScale = statueContent.localScale;
        normalPosition = statueContent.anchoredPosition;
        canvas = statueViewport.GetComponentInParent<Canvas>();
        toolCursorParent = toolCursor.parent as RectTransform;
        toolCursor.gameObject.SetActive(false);
    }

    void Update()
    {
        if (!minigameActive || !minigamePanel.activeSelf || Mouse.current == null) return;

        HandleToolSelection();
        UpdateToolCursor();
        UpdateToolAnimation();
        UpdateZoom();
    }

    void UpdateToolAnimation()
    {
        if (currentTool == Quest6CleaningTool.Sponge)
        {
            float mouseSpeed = Mouse.current.delta.ReadValue().magnitude;
            float targetIntensity = Mouse.current.leftButton.isPressed ? Mathf.Clamp01(mouseSpeed / spongeMouseSpeedForMaxAnimation) : 0f;
            float changeSpeed = targetIntensity > spongeAnimationIntensity ? spongeAnimationAcceleration : spongeAnimationDeceleration;

            spongeAnimationIntensity = Mathf.MoveTowards(spongeAnimationIntensity, targetIntensity, changeSpeed * Time.unscaledDeltaTime);

            float animationSpeed = Mathf.Lerp(spongeMinShakeSpeed, spongeMaxShakeSpeed, spongeAnimationIntensity);
            spongeAnimationPhase += animationSpeed * Time.unscaledDeltaTime;

            float wave = Mathf.Sin(spongeAnimationPhase);
            toolVisual.anchoredPosition = new Vector2(0f, wave * spongeShakeDistance * spongeAnimationIntensity);
            toolVisual.localRotation = Quaternion.Euler(0f, 0f, wave * spongeRotationAmount * spongeAnimationIntensity);

            return;
        }

        spongeAnimationIntensity = Mathf.MoveTowards(spongeAnimationIntensity, 0f, spongeAnimationDeceleration * Time.unscaledDeltaTime);

        if (currentTool == Quest6CleaningTool.Scraper && Mouse.current.leftButton.wasPressedThisFrame) scraperJabTimer = scraperJabDuration;

        if (currentTool == Quest6CleaningTool.Scraper && scraperJabTimer > 0f)
        {
            scraperJabTimer -= Time.unscaledDeltaTime;
            float t = 1f - Mathf.Clamp01(scraperJabTimer / scraperJabDuration);
            float jab = Mathf.Sin(t * Mathf.PI);
            toolVisual.anchoredPosition = scraperJabDirection.normalized * jab * scraperJabDistance;
            toolVisual.localRotation = Quaternion.identity;
            return;
        }

        toolVisual.anchoredPosition = Vector2.Lerp(toolVisual.anchoredPosition, Vector2.zero, 20f * Time.unscaledDeltaTime);
        toolVisual.localRotation = Quaternion.Lerp(toolVisual.localRotation, Quaternion.identity, 20f * Time.unscaledDeltaTime);
    }

    public Vector2 GetScraperTipScreenPosition()
    {
        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        return RectTransformUtility.WorldToScreenPoint(uiCamera, scraperTip.position);
    }

    void HandleToolSelection()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current.digit1Key.wasPressedThisFrame) SelectTool(Quest6CleaningTool.Sponge);
        if (Keyboard.current.digit2Key.wasPressedThisFrame) SelectTool(Quest6CleaningTool.Scraper);
    }

    void UpdateToolCursor()
    {
        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(toolCursorParent, Mouse.current.position.ReadValue(), uiCamera, out Vector2 localPoint))
            toolCursor.anchoredPosition = localPoint;

        float zoomFactor = statueContent.localScale.x / normalScale.x;
        toolVisual.localScale = Vector3.one * zoomFactor;
    }

    void SelectTool(Quest6CleaningTool tool)
    {
        currentTool = tool;

        if (currentTool == Quest6CleaningTool.Sponge)
        {
            toolCursorImage.sprite = spongeSprite;
            toolVisual.sizeDelta = dirtEraser.GetSpongeUISize() * spongeVisualMultiplier;
        }
        else
        {
            toolCursorImage.sprite = scraperSprite;
            toolVisual.sizeDelta = scraperCursorSize;
        }
    }

    void UpdateZoom()
    {
        bool zooming = Mouse.current.rightButton.isPressed;

        Vector3 targetScale = normalScale;
        Vector2 targetPosition = normalPosition;

        if (zooming)
        {
            targetScale = normalScale * zoomScale;
            Camera uiCamera = null;

            if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                uiCamera = canvas.worldCamera;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(statueViewport, Mouse.current.position.ReadValue(), uiCamera, out Vector2 mouseLocal))
                targetPosition = normalPosition - mouseLocal * (zoomScale - 1f) * zoomFollowStrength;
        }

        statueContent.localScale = Vector3.Lerp(statueContent.localScale, targetScale, zoomSpeed * Time.unscaledDeltaTime);
        statueContent.anchoredPosition = Vector2.Lerp(statueContent.anchoredPosition, targetPosition, zoomSpeed * Time.unscaledDeltaTime);
    }

    public void OpenMinigame()
    {
        minigameActive = true;

        instructionsPanel.SetActive(true);
        minigamePanel.SetActive(false);

        if (PlayerFreeze.Instance != null)
            PlayerFreeze.Instance.FreezePlayer();

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void StartCleaning()
    {
        instructionsPanel.SetActive(false);
        minigamePanel.SetActive(true);

        SelectTool(Quest6CleaningTool.Sponge);

        toolCursor.gameObject.SetActive(true);

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.None;

        Debug.Log("Quest 6 cleaning started.");
    }

    public void CloseMinigame()
    {
        minigameActive = false;

        instructionsPanel.SetActive(false);
        minigamePanel.SetActive(false);

        toolCursor.gameObject.SetActive(false);
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        if (PlayerFreeze.Instance != null)
            PlayerFreeze.Instance.UnfreezePlayer();
    }

    public bool IsSpongeSelected()
    {
        return currentTool == Quest6CleaningTool.Sponge;
    }

    public bool IsScraperSelected()
    {
        return currentTool == Quest6CleaningTool.Scraper;
    }
}