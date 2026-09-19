using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class Quest6Barnacle : MonoBehaviour
{
    [Header("References")]
    public Quest6CleaningManager cleaningManager;
    public Image barnacleImage;

    [Header("Hits")]
    public int hitsRequired = 3;

    [Header("Hit Animation")]
    public float shakeDuration = 0.18f;
    public float shakeDistance = 5f;
    public float hitScaleAmount = 0.08f;
    public float hitRotationAmount = 8f;

    [Header("Removal")]
    public float popScale = 1.2f;
    public float fallSpeed = 180f;
    public float fallRotationSpeed = 220f;
    public float fadeSpeed = 2.5f;
    public float popUpSpeed = 120f;
    public float gravity = 500f;
    public float horizontalPopSpeed = 40f;

    int currentHits;
    float shakeTimer;
    bool removed;
    bool falling;

    Vector2 fallVelocity;
    Vector2 originalPosition;
    Vector3 originalScale;
    Quaternion originalRotation;
    Canvas canvas;

    public bool IsRemoved => removed;

    void Awake()
    {
        if (barnacleImage == null) barnacleImage = GetComponent<Image>();

        originalPosition = GetComponent<RectTransform>().anchoredPosition;
        originalScale = transform.localScale;
        originalRotation = transform.localRotation;
        canvas = GetComponentInParent<Canvas>();
    }

    void Update()
    {
        if (falling)
        {
            UpdateFalling();
            return;
        }

        if (removed || Mouse.current == null || cleaningManager == null || !cleaningManager.CanUseTools())
            return;

        if (Mouse.current.leftButton.wasPressedThisFrame && cleaningManager.IsScraperSelected() && IsScraperOverBarnacle())
            HitBarnacle();

        UpdateShake();
    }

    bool IsScraperOverBarnacle()
    {
        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        return RectTransformUtility.RectangleContainsScreenPoint(GetComponent<RectTransform>(), cleaningManager.GetScraperTipScreenPosition(), uiCamera);
    }

    void HitBarnacle()
    {
        currentHits++;
        shakeTimer = shakeDuration;

        SoundEffectManager.Play("StatueScrape", true);

        if (currentHits >= hitsRequired)
        {
            cleaningManager.SpawnBarnacleDebris(GetBarnacleScreenPosition(), 6, 10, true);
            SoundEffectManager.Play("StatueBarnaclePop", true);
            RemoveBarnacle();
            return;
        }

        cleaningManager.SpawnBarnacleDebris(GetBarnacleScreenPosition(), 2, 3, false);

        float progress = (float)currentHits / hitsRequired;
        transform.localScale = originalScale * (1f + hitScaleAmount * progress);
        transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-hitRotationAmount, hitRotationAmount));
    }

    Vector2 GetBarnacleScreenPosition()
    {
        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        return RectTransformUtility.WorldToScreenPoint(uiCamera, GetComponent<RectTransform>().position);
    }

    void UpdateShake()
    {
        if (shakeTimer <= 0f) return;

        shakeTimer -= Time.unscaledDeltaTime;

        float strength = shakeTimer / shakeDuration;
        float x = Random.Range(-shakeDistance, shakeDistance) * strength;
        float y = Random.Range(-shakeDistance, shakeDistance) * strength;

        GetComponent<RectTransform>().anchoredPosition = originalPosition + new Vector2(x, y);

        if (shakeTimer <= 0f) GetComponent<RectTransform>().anchoredPosition = originalPosition;
    }

    void RemoveBarnacle()
    {
        removed = true;
        falling = true;
        transform.localScale = originalScale * popScale;
        fallVelocity = new Vector2(Random.Range(-horizontalPopSpeed, horizontalPopSpeed), popUpSpeed);
    }

    void UpdateFalling()
    {
        RectTransform rect = GetComponent<RectTransform>();

        fallVelocity.y -= gravity * Time.unscaledDeltaTime;
        rect.anchoredPosition += fallVelocity * Time.unscaledDeltaTime;
        transform.Rotate(0f, 0f, fallRotationSpeed * Time.unscaledDeltaTime);

        Color color = barnacleImage.color;
        color.a = Mathf.MoveTowards(color.a, 0f, fadeSpeed * Time.unscaledDeltaTime);
        barnacleImage.color = color;

        if (color.a <= 0f)
        {
            falling = false;
            gameObject.SetActive(false);
        }
    }
}