using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Globalization;

[RequireComponent(typeof(Rigidbody))]
public class DepthSystem : MonoBehaviour
{
    [Header("Depth")]
    [SerializeField] private Transform waterSurfaceReference;
    [SerializeField] private float metresPerUnityUnit = 1f;
    [SerializeField] private float maxSafeDepth = 20f;

    [Header("Depth Danger")]
    [SerializeField] private float gracePeriod = 0.5f;
    [SerializeField] private float blackoutDuration = 4f;
    [SerializeField] private float recoverySpeed = 2f;

    [Header("Respawning")]
    [SerializeField] private Transform homeRespawnPoint;
    [SerializeField] private MonoBehaviour playerMovementController;
    [SerializeField] private float lastChanceDuration = 0.75f;
    [SerializeField] private float postTeleportBlackHold = 0.25f;
    [SerializeField] private float fadeFromBlackDuration = 1f;
    [SerializeField] private float rescueFadeDuration = 0.5f;

    [Header("UI")]
    [SerializeField] private Slider depthSlider;
    // [SerializeField] private TMP_Text currentDepthText;
    // [SerializeField] private TMP_Text depthLimitText;
    [SerializeField] private TMP_Text depthText;
    [SerializeField] private TMP_Text dangerText;
    [SerializeField] private CanvasGroup blackoutCanvasGroup;

    [Header("Breath Flash")]
    [SerializeField] private CanvasGroup breathFlashCanvasGroup;
    [SerializeField] private float minimumDangerTimeForBreathFlash = 1.25f;
    [SerializeField, Range(0f, 1f)] private float breathFlashAlpha = 0.35f;

    private Rigidbody rb;
    private float dangerTimer;

    private bool wasTooDeep;
    private bool pendingBreathFlash;
    private bool isTransitioning;

    private Tween blackoutTween;
    private Sequence breathSequence;

    public float CurrentDepth { get; private set; }
    public float MaxSafeDepth => maxSafeDepth;
    public bool IsTooDeep => CurrentDepth > maxSafeDepth;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        if (waterSurfaceReference == null || homeRespawnPoint == null || blackoutCanvasGroup == null)
        {
            enabled = false;
            return;
        }

        blackoutCanvasGroup.alpha = 0f;
        blackoutCanvasGroup.blocksRaycasts = false;
        blackoutCanvasGroup.interactable = false;

        if (breathFlashCanvasGroup != null)
        {
            breathFlashCanvasGroup.alpha = 0f;
            breathFlashCanvasGroup.blocksRaycasts = false;
            breathFlashCanvasGroup.interactable = false;
        }

        if (dangerText != null)
            dangerText.gameObject.SetActive(false);

        CalculateDepth();
        UpdateDepthUI();
    }

    private void Update()
    {
        CalculateDepth();
        UpdateDepthUI();

        if (!isTransitioning)
            UpdateDepthDanger();
    }

    private void CalculateDepth()
    {
        float distanceBelowSurface = waterSurfaceReference.position.y - rb.position.y;
        CurrentDepth = Mathf.Max(0f, distanceBelowSurface * metresPerUnityUnit);
    }

    private void UpdateDepthUI()
    {
        depthText.text = $"Depth: {CurrentDepth.ToString("0.0", CultureInfo.InvariantCulture)}m / {maxSafeDepth.ToString("0.#", CultureInfo.InvariantCulture)}m";

        // if (depthLimitText != null)
        //     depthLimitText.text = $"Limit: {maxSafeDepth:0.#} m";

        if (depthSlider != null)
        {
            depthSlider.minValue = 0f;
            depthSlider.maxValue = Mathf.Max(1f, maxSafeDepth);
            depthSlider.value = Mathf.Clamp(CurrentDepth, 0f, maxSafeDepth);
        }
    }

    private void UpdateDepthDanger()
    {
        bool tooDeep = IsTooDeep;

        if (tooDeep)
        {
            dangerTimer += Time.deltaTime;
        }
        else
        {
            if (wasTooDeep && dangerTimer >= minimumDangerTimeForBreathFlash)
                pendingBreathFlash = true;

            dangerTimer = Mathf.MoveTowards(dangerTimer, 0f, recoverySpeed * Time.deltaTime);
        }

        if (dangerText != null)
            dangerText.gameObject.SetActive(tooDeep);

        float fadeAmount = Mathf.InverseLerp(gracePeriod, gracePeriod + blackoutDuration, dangerTimer);
        blackoutCanvasGroup.alpha = fadeAmount;

        if (!tooDeep && dangerTimer <= 0f && pendingBreathFlash)
        {
            pendingBreathFlash = false;
            PlayBreathFlash();
        }

        if (fadeAmount >= 1f)
            StartCoroutine(BlackoutRoutine());

        wasTooDeep = tooDeep;
    }

    private IEnumerator BlackoutRoutine()
    {
        if (isTransitioning)
            yield break;

        isTransitioning = true;
        blackoutCanvasGroup.alpha = 1f;

        // movement still works while the screen is black

        float remainingChance = lastChanceDuration;

        while (remainingChance > 0f)
        {
            if (!IsTooDeep)
            {
                yield return RecoverFromBlackout();
                yield break;
            }

            remainingChance -= Time.deltaTime;
            yield return null;
        }

        if (!IsTooDeep)
        {
            yield return RecoverFromBlackout();
            yield break;
        }

        if (dangerText != null)
            dangerText.gameObject.SetActive(false);

        if (playerMovementController != null)
            playerMovementController.enabled = false;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.position = homeRespawnPoint.position;
        rb.rotation = Quaternion.identity;

        Physics2D.SyncTransforms();

        dangerTimer = 0f;
        wasTooDeep = false;
        pendingBreathFlash = false;

        CalculateDepth();
        UpdateDepthUI();

        yield return new WaitForFixedUpdate();
        yield return null;

        if (postTeleportBlackHold > 0f)
            yield return new WaitForSecondsRealtime(postTeleportBlackHold);

        // gameplay resumes before the screen fades back in

        if (playerMovementController != null)
            playerMovementController.enabled = true;

        blackoutTween?.Kill();
        blackoutTween = blackoutCanvasGroup.DOFade(0f, fadeFromBlackDuration).SetEase(Ease.InOutSine).SetUpdate(true);
        PlayBreathFlash();

        yield return blackoutTween.WaitForCompletion();

        blackoutCanvasGroup.alpha = 0f;
        blackoutTween = null;
        isTransitioning = false;
    }

    private IEnumerator RecoverFromBlackout()
    {
        dangerTimer = 0f;
        wasTooDeep = false;
        pendingBreathFlash = false;

        if (dangerText != null)
            dangerText.gameObject.SetActive(false);

        blackoutTween?.Kill();
        blackoutTween = blackoutCanvasGroup.DOFade(0f, rescueFadeDuration).SetEase(Ease.OutSine).SetUpdate(true);
        PlayBreathFlash();

        yield return blackoutTween.WaitForCompletion();

        blackoutCanvasGroup.alpha = 0f;
        blackoutTween = null;
        isTransitioning = false;
    }

private void PlayBreathFlash()
{
    if (breathFlashCanvasGroup == null)
        return;

    breathSequence?.Kill();
    breathFlashCanvasGroup.DOKill();
    breathFlashCanvasGroup.alpha = 0f;

    breathSequence = DOTween.Sequence();
    for (int i = 0; i < 4; i++)
        breathSequence.Append(breathFlashCanvasGroup.DOFade(breathFlashAlpha, 0.25f)
            .SetEase(Ease.Linear)).Append(breathFlashCanvasGroup.DOFade(0f, 0.3f).SetEase(Ease.Linear));
    breathSequence.SetUpdate(true);
}

    public void SetMaxSafeDepth(float newDepth)
    {
        maxSafeDepth = Mathf.Max(1f, newDepth);
        UpdateDepthUI();
    }

    public void AddDepthCapacity(float additionalDepth)
    {
        maxSafeDepth = Mathf.Max(1f, maxSafeDepth + additionalDepth);
        UpdateDepthUI();
    }

    private void OnDisable()
    {
        blackoutTween?.Kill();
        breathSequence?.Kill();
    }

    private void OnValidate()
    {
        metresPerUnityUnit = Mathf.Max(0.01f, metresPerUnityUnit);
        maxSafeDepth = Mathf.Max(1f, maxSafeDepth);
        gracePeriod = Mathf.Max(0f, gracePeriod);
        blackoutDuration = Mathf.Max(0.1f, blackoutDuration);
        recoverySpeed = Mathf.Max(0.01f, recoverySpeed);
        lastChanceDuration = Mathf.Max(0f, lastChanceDuration);
        postTeleportBlackHold = Mathf.Max(0f, postTeleportBlackHold);
        fadeFromBlackDuration = Mathf.Max(0.1f, fadeFromBlackDuration);
        rescueFadeDuration = Mathf.Max(0.1f, rescueFadeDuration);
    }
}