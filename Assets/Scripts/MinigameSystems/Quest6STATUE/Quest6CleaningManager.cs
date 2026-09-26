using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using System.Collections;

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

    [Header("Completion")]
    public Quest6Statue worldStatue;
    public RectTransform continueArrow;
    [Range(0f, 1f)] public float requiredCleanPercent = 0.95f;
    public float completionDelay = 0.75f;
    public float arrowBobDistance = 5f;
    public float arrowBobSpeed = 4f;

    [Header("Completion Pop")]
    public float completionPopAmount = 0.05f;
    public float completionPopDuration = 0.25f;

    Quest6Barnacle[] barnacles;
    bool minigameComplete;
    bool canExit;
    float completionTimer;
    Vector2 continueArrowStartPosition;

    [Header("Completion Shine")]
    public RectTransform shineMask;
    public RectTransform shineStrip;
    public CanvasGroup shineCanvasGroup;
    public float shineDelay = 0.15f;
    public float shineDuration = 0.7f;
    public float shineMaxAlpha = 0.3f;

    [Header("Audio")]
    public float spongeMaxVolume = 0.5f;
    public float spongeMinPitch = 0.9f;
    public float spongeMaxPitch = 1.15f;

    [Header("Particles")]
    public RectTransform particleLayer;
    public Quest6UIParticle dirtParticlePrefab;
    public int dirtParticlesMin = 2;
    public int dirtParticlesMax = 5;
    public float dirtParticleInterval = 0.035f;
    public float dirtParticleSpread = 10f;
    public Quest6UIParticle barnacleDebrisPrefab;
    public int barnacleDebrisCount = 5;

    float dirtParticleTimer;

    [SerializeField] private QuestObjectiveCompletion questObjectiveCompletion;

    void Start()
    {
        normalScale = statueContent.localScale;
        normalPosition = statueContent.anchoredPosition;
        canvas = statueViewport.GetComponentInParent<Canvas>();
        toolCursorParent = toolCursor.parent as RectTransform;
        toolCursor.gameObject.SetActive(false);

        barnacles = minigamePanel.GetComponentsInChildren<Quest6Barnacle>(true);
        continueArrowStartPosition = continueArrow.anchoredPosition;
        continueArrow.gameObject.SetActive(false);
    }

    void Update()
    {
        if (!minigameActive || !minigamePanel.activeSelf || Mouse.current == null) return;

        if (minigameComplete)
        {
            UpdateCompletion();
            return;
        }

        HandleToolSelection();
        UpdateToolCursor();
        UpdateToolAnimation();
        UpdateSpongeAudio();
        UpdateZoom();
        CheckCompletion();
    }

    public bool CanUseTools()
    {
        return minigameActive && !minigameComplete;
    }

    IEnumerator PlayCompletionShine()
    {
        yield return new WaitForSecondsRealtime(shineDelay);

        shineStrip.gameObject.SetActive(true);

        // float startX = shineMask.rect.xMin - shineStrip.rect.width;
        float startX = shineMask.rect.xMin - shineStrip.rect.width * 0.15f;
        float endX = shineMask.rect.xMax + shineStrip.rect.width;
        float timer = 0f;

        while (timer < shineDuration)
        {
            timer += Time.unscaledDeltaTime;

            float progress = Mathf.Clamp01(timer / shineDuration);
            float smoothProgress = Mathf.SmoothStep(0f, 1f, progress);

            Vector2 position = shineStrip.anchoredPosition;
            position.x = Mathf.Lerp(startX, endX, smoothProgress);
            shineStrip.anchoredPosition = position;

            shineCanvasGroup.alpha = Mathf.Sin(progress * Mathf.PI) * shineMaxAlpha;

            yield return null;
        }

        shineCanvasGroup.alpha = 0f;
        shineStrip.gameObject.SetActive(false);
    }

    void CheckCompletion()
    {
        if (dirtEraser.GetCleanPercent() < requiredCleanPercent) return;

        foreach (Quest6Barnacle barnacle in barnacles)
        {
            if (!barnacle.IsRemoved) return;
        }

        CompleteMinigame();
    }

    void UpdateCompletion()
    {
        completionTimer += Time.unscaledDeltaTime;

        float popMultiplier = 1f;
        float popStart = shineDelay;
        float popEnd = shineDelay + completionPopDuration;
        if (completionTimer >= popStart && completionTimer <= popEnd)
        {
            float popProgress = Mathf.InverseLerp(popStart, popEnd, completionTimer);
            popMultiplier += Mathf.Sin(popProgress * Mathf.PI) * completionPopAmount;
        }
        Vector3 completionScale = normalScale * popMultiplier;
        statueContent.localScale = Vector3.Lerp(statueContent.localScale, completionScale, zoomSpeed * Time.unscaledDeltaTime);

        statueContent.anchoredPosition = Vector2.Lerp(statueContent.anchoredPosition, normalPosition, zoomSpeed * Time.unscaledDeltaTime);

        if (completionTimer < completionDelay) return;

        if (!canExit)
        {
            canExit = true;
            continueArrow.gameObject.SetActive(true);
        }

        float bob = Mathf.Sin((completionTimer - completionDelay) * arrowBobSpeed) * arrowBobDistance;
        continueArrow.anchoredPosition = continueArrowStartPosition + Vector2.up * bob;

        if (Mouse.current.leftButton.wasPressedThisFrame)
            ExitCompletedMinigame();
    }

    void ExitCompletedMinigame()
    {
        worldStatue.SetCleaned();
        CloseMinigame();
    }

    void CompleteMinigame()
    {
        minigameComplete = true;
        canExit = false;
        completionTimer = 0f;

        toolCursor.gameObject.SetActive(false);
        SoundEffectManager.StopLoop();
        continueArrow.gameObject.SetActive(false);

        dirtEraser.RevealCleanStatue();

        StartCoroutine(PlayCompletionShine());
    }

    public void TrySpawnDirtParticles(Vector2 screenPosition)
    {
        dirtParticleTimer -= Time.unscaledDeltaTime;
        if (dirtParticleTimer > 0f) return;

        dirtParticleTimer = dirtParticleInterval;

        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(particleLayer, screenPosition, uiCamera, out Vector2 localPoint)) return;

        int count = Random.Range(dirtParticlesMin, dirtParticlesMax + 1);

        for (int i = 0; i < count; i++)
        {
            Quest6UIParticle particle = Instantiate(dirtParticlePrefab, particleLayer);
            RectTransform particleRect = particle.GetComponent<RectTransform>();

            particleRect.anchoredPosition = localPoint + Random.insideUnitCircle * dirtParticleSpread;

            Vector2 velocity = new Vector2(Random.Range(-70f, 70f), Random.Range(-90f, 20f));
            float gravity = Random.Range(40f, 100f);
            float rotation = Random.Range(-300f, 300f);
            float lifetime = Random.Range(0.3f, 0.7f);
            float scale = Random.Range(0.4f, 1.25f);

            particle.Launch(velocity, gravity, rotation, lifetime, scale);
        }
    }

    void SpawnDirtParticle()
    {
        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(particleLayer, Mouse.current.position.ReadValue(), uiCamera, out Vector2 localPoint)) return;

        Quest6UIParticle particle = Instantiate(dirtParticlePrefab, particleLayer);
        RectTransform particleRect = particle.GetComponent<RectTransform>();
        particleRect.anchoredPosition = localPoint + Random.insideUnitCircle * 5f;

        Vector2 velocity = new Vector2(Random.Range(-25f, 25f), Random.Range(-70f, -30f));
        particle.Launch(velocity, 30f, Random.Range(-180f, 180f), Random.Range(0.3f, 0.55f), Random.Range(0.6f, 1.1f));
    }

    public void SpawnBarnacleDebris(Vector2 screenPosition, int minCount, int maxCount, bool strongBurst)
    {
        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(particleLayer, screenPosition, uiCamera, out Vector2 localPoint)) return;

        int count = Random.Range(minCount, maxCount + 1);

        for (int i = 0; i < count; i++)
        {
            Quest6UIParticle particle = Instantiate(barnacleDebrisPrefab, particleLayer);
            RectTransform particleRect = particle.GetComponent<RectTransform>();

            particleRect.anchoredPosition = localPoint + Random.insideUnitCircle * 4f;

            float horizontal = strongBurst ? Random.Range(-120f, 120f) : Random.Range(-55f, 55f);
            float vertical = strongBurst ? Random.Range(80f, 160f) : Random.Range(20f, 80f);
            float gravity = strongBurst ? 350f : 250f;
            float lifetime = strongBurst ? Random.Range(0.5f, 0.9f) : Random.Range(0.25f, 0.5f);

            particle.Launch(new Vector2(horizontal, vertical), gravity, Random.Range(-450f, 450f), lifetime, Random.Range(0.4f, 1f));
        }
    }

    void UpdateSpongeAudio()
    {
        bool moving = Mouse.current.delta.ReadValue().sqrMagnitude > 0.1f;
        bool scrubbing = CanUseTools() && currentTool == Quest6CleaningTool.Sponge && Mouse.current.leftButton.isPressed && moving;

        if (!scrubbing)
        {
            if (SoundEffectManager.IsLoopPlaying()) SoundEffectManager.StopLoop();
            return;
        }

        float volume = spongeAnimationIntensity * spongeMaxVolume;
        float pitch = Mathf.Lerp(spongeMinPitch, spongeMaxPitch, spongeAnimationIntensity);

        if (!SoundEffectManager.IsLoopPlaying()) SoundEffectManager.StartLoop("StatueSponge", volume, pitch);
        else SoundEffectManager.UpdateLoop(volume, pitch);
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

        SoundEffectManager.StopLoop();

        if (questObjectiveCompletion != null) 
            questObjectiveCompletion.CompleteObjective();

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