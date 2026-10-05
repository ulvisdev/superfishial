using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Globalization;

[RequireComponent(typeof(CharacterController))]
public class DepthSystem : MonoBehaviour
{
    [Header("Depth")]
    [SerializeField] private Transform waterSurfaceReference;
    [SerializeField] private float metresPerUnityUnit = 1f;
    [SerializeField] private float maxSafeDepth = 20f;

    [Header("Story Rescue")]
    [SerializeField] private string exteriorRoomID = "worldspawn";
    [SerializeField] private string rescueRoomID = "doc_office";
    [SerializeField] private Transform rescueArrival;
    [SerializeField] private string rescueQuestFlag = "quest4_started";
    [SerializeField] private string rescuedFlag = "first_descend_failed";

    [Header("Treatment")]
    [SerializeField] private string treatmentFlag = "treatment_complete";
    [SerializeField] private float treatedMaxSafeDepth = 120f;

    [Header("Depth Danger")]
    [SerializeField] private float gracePeriod = 0.5f;
    [SerializeField] private float blackoutDuration = 4f;
    [SerializeField] private float recoverySpeed = 2f;

    [Header("Respawning")]
    [SerializeField] private Transform homeRespawnPoint;
    [SerializeField] private PlayerMovement playerMovementController;
    [SerializeField] private float lastChanceDuration = 0.75f;
    [SerializeField] private float postTeleportBlackHold = 0.25f;
    [SerializeField] private float fadeFromBlackDuration = 1f;
    [SerializeField] private float rescueFadeDuration = 0.5f;

    [Header("UI")]
    [SerializeField] private Slider depthSlider;
    [SerializeField] private TMP_Text depthText;
    [SerializeField] private TMP_Text dangerText;
    [SerializeField] private CanvasGroup blackoutCanvasGroup;

    [Header("Breath Flash")]
    [SerializeField] private CanvasGroup breathFlashCanvasGroup;
    [SerializeField] private float minimumDangerTimeForBreathFlash = 1.25f;
    [SerializeField, Range(0f, 1f)] private float breathFlashAlpha = 0.35f;

    private float dangerTimer;

    private bool wasTooDeep;
    private bool pendingBreathFlash;
    private bool isTransitioning;

    private float originalMaxSafeDepth;
    private PlayerFreeze rescueFreeze;
    private bool ownsRescueControl;
    private bool previousPause;
    private bool previousMovement;

    private Tween blackoutTween;
    private Sequence breathSequence;

    public float CurrentDepth { get; private set; }
    public float MaxSafeDepth => maxSafeDepth;
    public bool IsTooDeep => CurrentDepth > maxSafeDepth;

    private void Awake()
    {
        originalMaxSafeDepth = maxSafeDepth;

        if (playerMovementController == null)
            playerMovementController = GetComponent<PlayerMovement>();

        if (waterSurfaceReference == null || homeRespawnPoint == null || blackoutCanvasGroup == null || playerMovementController == null)
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

    private void OnEnable()
    {
        StoryState.Changed += ApplyTreatment;
        ApplyTreatment();
    }

    private void Start()
    {
        ApplyTreatment();
    }

    private void ApplyTreatment()
    {
        StoryState state = StoryState.Instance;

        if (state == null || !state.IsReady)
            return;

        bool treated = state.HasFlag(treatmentFlag);
        SetMaxSafeDepth(treated ? treatedMaxSafeDepth : originalMaxSafeDepth);

        if (treated && !state.HasFlag("can_enter_depths"))
            state.SetFlag("can_enter_depths");
    }

    private bool CanEvaluatePressure()
    {
        if (StoryState.Instance == null || !StoryState.Instance.IsReady || SaveController.Instance == null || !SaveController.Instance.IsReady || SaveController.Instance.IsLoading)
            return false;

        if (PauseController.IsGamePaused || StoryCutsceneController.IsPlaying || RoomTravelController.IsTravelling || NPC.ActiveNPC != null || Time.timeScale <= 0f)
            return false;

        if (PlayerFreeze.Instance != null && PlayerFreeze.Instance.IsFrozen)
            return false;

        return RoomTravelController.Instance != null && RoomTravelController.Instance.CurrentRoomID == exteriorRoomID;
    }

    private void Update()
    {
        CalculateDepth();
        UpdateDepthUI();

        if (isTransitioning)
            return;

        if (!CanEvaluatePressure())
        {
            dangerTimer = 0f;
            wasTooDeep = false;
            pendingBreathFlash = false;
            blackoutCanvasGroup.alpha = 0f;

            if (dangerText != null)
                dangerText.gameObject.SetActive(false);

            return;
        }

        UpdateDepthDanger();
    }

    private void CalculateDepth()
    {
        float distanceBelowSurface = waterSurfaceReference.position.y - transform.position.y;

        CurrentDepth = Mathf.Max(0f, distanceBelowSurface * metresPerUnityUnit);
    }

    private void UpdateDepthUI()
    {
        if (depthText != null)
            depthText.text = $"Depth: {CurrentDepth.ToString("0.0", CultureInfo.InvariantCulture)}m / {maxSafeDepth.ToString("0.#", CultureInfo.InvariantCulture)}m";

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

        float remainingChance = lastChanceDuration;

        while (remainingChance > 0f)
        {
            if (!CanEvaluatePressure() || !IsTooDeep)
            {
                yield return RecoverFromBlackout();
                yield break;
            }

            remainingChance -= Time.deltaTime;
            yield return null;
        }

        if (!CanEvaluatePressure() || !IsTooDeep)
        {
            yield return RecoverFromBlackout();
            yield break;
        }

        if (dangerText != null)
            dangerText.gameObject.SetActive(false);

        previousPause = PauseController.IsGamePaused;
        previousMovement = playerMovementController.IsMovementEnabled;
        rescueFreeze = PlayerFreeze.Instance;
        ownsRescueControl = true;

        if (rescueFreeze != null)
            rescueFreeze.FreezePlayer();
        else
            playerMovementController.SetMovementEnabled(false);

        PauseController.SetPause(true);
        playerMovementController.StopImmediately();
        StoryState state = StoryState.Instance;
        RoomTravelController travel = RoomTravelController.Instance;
        bool storyRescue = state != null && state.HasFlag(rescueQuestFlag) && !state.HasFlag(treatmentFlag);
        bool rescuedAtClinic = false;

        if (storyRescue && travel != null)
            rescuedAtClinic = travel.Place(rescueRoomID, rescueArrival);

        if (!rescuedAtClinic)
        {
            if (storyRescue)
                Debug.LogError("Story rescue could not reach Doc Cat. Check Rescue Room ID and RoomTravelController Player.", this);

            playerMovementController.TeleportTo(homeRespawnPoint.position);

            if (travel != null)
                travel.SelectRoom(exteriorRoomID);
        }

        dangerTimer = 0f;
        wasTooDeep = false;
        pendingBreathFlash = false;

        CalculateDepth();
        UpdateDepthUI();

        yield return null;

        if (postTeleportBlackHold > 0f)
            yield return new WaitForSecondsRealtime(postTeleportBlackHold);

        blackoutTween?.Kill();
        blackoutTween = blackoutCanvasGroup.DOFade(0f, fadeFromBlackDuration).SetEase(Ease.InOutSine).SetUpdate(true);
        PlayBreathFlash();

        yield return blackoutTween.WaitForCompletion();

        blackoutCanvasGroup.alpha = 0f;
        blackoutTween = null;
        isTransitioning = false;
        ReleaseRescueControl();

        if (rescuedAtClinic)
            state.SetFlag(rescuedFlag);

        if (SaveController.Instance != null)
            SaveController.Instance.RequestSave();
    }

    private void ReleaseRescueControl()
    {
        if (!ownsRescueControl)
            return;

        ownsRescueControl = false;
        PauseController.SetPause(previousPause);

        if (rescueFreeze != null)
            rescueFreeze.UnfreezePlayer();
        else if (playerMovementController != null)
            playerMovementController.SetMovementEnabled(previousMovement);
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
            breathSequence.Append(breathFlashCanvasGroup.DOFade(breathFlashAlpha, 0.25f).SetEase(Ease.Linear)).Append(breathFlashCanvasGroup.DOFade(0f, 0.3f).SetEase(Ease.Linear));
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
        StoryState.Changed -= ApplyTreatment;
        StopAllCoroutines();
        blackoutTween?.Kill();
        breathSequence?.Kill();
        isTransitioning = false;
        ReleaseRescueControl();

        if (blackoutCanvasGroup != null)
            blackoutCanvasGroup.alpha = 0f;

        if (breathFlashCanvasGroup != null)
            breathFlashCanvasGroup.alpha = 0f;
    }

    private void OnValidate()
    {
        treatedMaxSafeDepth = Mathf.Max(1f, treatedMaxSafeDepth);
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
