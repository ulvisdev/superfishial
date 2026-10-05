using System.Collections;
using UnityEngine;

[DefaultExecutionOrder(-1000)]
public class StartupCurtain : MonoBehaviour
{
    public static StartupCurtain Instance { get; private set; }
    public static bool IsHolding => Instance != null && Instance.holding;

    [Header("Screen")]
    [SerializeField] private CanvasGroup curtain;
    [SerializeField] private float continueRevealSeconds = 0.6f;

    [Header("Opening")]
    [SerializeField] private StoryCutscene openingCutscene;

    [Header("Input")]
    [SerializeField] private Behaviour playerInput;

    private bool holding;
    private bool inputWasEnabled;
    private bool ownsFreeze;
    private PlayerFreeze freeze;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;

        if (curtain == null)
        {
            Debug.LogError("Startup Curtain needs its own CanvasGroup.", this);
            enabled = false;
            return;
        }

        holding = true;
        curtain.alpha = 1f;
        curtain.blocksRaycasts = true;

        if (playerInput != null)
        {
            inputWasEnabled = playerInput.enabled;
            playerInput.enabled = false;
        }
    }

    private IEnumerator Start()
    {
        while (holding && (SaveController.Instance == null || !SaveController.Instance.IsReady || SaveController.Instance.IsLoading || StoryState.Instance == null || !StoryState.Instance.IsReady))
            yield return null;

        if (!holding)
            yield break;

        bool needsOpening = openingCutscene != null && !string.IsNullOrWhiteSpace(openingCutscene.completedFlag) && !StoryState.Instance.HasFlag(openingCutscene.completedFlag) && (openingCutscene.condition == null || openingCutscene.condition.IsMet());

        if (needsOpening)
        {
            float startedWaiting = Time.unscaledTime;
            bool warned = false;

            while (holding)
            {
                if (!warned && Time.unscaledTime - startedWaiting > 10f)
                {
                    warned = true;
                    Debug.LogError("Startup is waiting for the opening. Check automatic Cutscenes, opening references, validation errors and the ReleaseForCutscene hook.", this);
                }

                yield return null;
            }

            yield break;
        }

        freeze = PlayerFreeze.Instance;

        if (freeze != null)
        {
            freeze.FreezePlayer();
            ownsFreeze = true;
        }

        float duration = Mathf.Max(0.01f, continueRevealSeconds);
        float elapsed = 0f;

        while (holding && elapsed < duration)
        {
            curtain.alpha = Mathf.SmoothStep(1f, 0f, elapsed / duration);
            yield return null;
            elapsed += Time.unscaledDeltaTime;
        }

        Release();
    }

    public void ReleaseForCutscene(StoryCutscene cutscene)
    {
        if (holding && cutscene != null)
            Release();
    }

    private void Release()
    {
        if (!holding)
            return;

        holding = false;

        if (curtain != null)
        {
            curtain.alpha = 0f;
            curtain.blocksRaycasts = false;
        }

        if (playerInput != null)
            playerInput.enabled = inputWasEnabled;

        if (ownsFreeze && freeze != null)
            freeze.UnfreezePlayer();

        ownsFreeze = false;
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        Release();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
