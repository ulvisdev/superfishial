using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

[System.Serializable]
public class CutsceneRoomExit
{
    public StoryCutscene cutscene;
    public string roomID;
    public Transform arrivalOverride;
}

public class StoryCutsceneController : MonoBehaviour
{
    public static bool IsPlaying { get; private set; }

    [Header("Canvas")]
    [SerializeField] private GameObject cutsceneCanvas;
    [SerializeField] private Image background;
    [SerializeField] private Image journalHands;
    [SerializeField] private RectTransform completionBanner;
    [SerializeField] private TMP_Text completionText;
    [SerializeField] private Image bannerImage;
    [SerializeField] private GameObject continuePrompt;
    [SerializeField] private Image fadeOverlay;

    [Header("Room Handoff")]
    [SerializeField] private CutsceneRoomExit[] roomExits;

    [Header("Transitions")]
    [SerializeField] private ScreenTransition sharedTransition;
    [SerializeField] private float exitCoverSeconds = 0.7f;
    [SerializeField] private float revealSeconds = 0.9f;
    [SerializeField] private float coveredHoldSeconds = 0.25f;
    [SerializeField] private float fadeSeconds = 0.4f;
    [SerializeField] private Image bubbleOverlay;
    [SerializeField] private Sprite[] bubbleFrames;
    [SerializeField] private int bubbleCoveredFrame;
    [SerializeField] private float bubbleFramesPerSecond = 18f;

    [Header("Audio")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource effectsSource;
    [SerializeField] private AudioSource[] gameplayAudio;

    [Header("Gameplay")]
    [SerializeField] private PlayerFreeze playerFreeze;
    [SerializeField] private GameObject[] hideDuringCutscene;
    [SerializeField] private Behaviour[] disableDuringCutscene;

    [Header("Automatic Playback")]
    [SerializeField] private StoryCutscene[] cutscenes;

    [Header("Testing")]
    [SerializeField] private StoryCutscene testCutscene;

    private bool ownsPlayback;
    private bool previousPause;
    private float previousTimeScale;
    private bool[] hiddenStates;
    private bool[] behaviourStates;
    private bool[] audioStates;
    private bool oldMusicIgnorePause;
    private bool oldEffectsIgnorePause;
    private float nextCheck;

    private void Start()
    {
        if (playerFreeze == null)
            playerFreeze = PlayerFreeze.Instance;

        if (cutsceneCanvas != null && !transform.IsChildOf(cutsceneCanvas.transform))
            cutsceneCanvas.SetActive(false);
    }

    private void Update()
    {
        if (IsPlaying || Time.unscaledTime < nextCheck)
            return;

        nextCheck = Time.unscaledTime + 0.1f;

        if (!CanStart() || cutscenes == null)
            return;

        foreach (StoryCutscene cutscene in cutscenes)
        {
            if (cutscene == null || string.IsNullOrWhiteSpace(cutscene.completedFlag) || StoryState.Instance.HasFlag(cutscene.completedFlag))
                continue;

            if (cutscene.condition != null && !cutscene.condition.IsMet())
                continue;

            if (!ValidateSetup(cutscene))
            {
                enabled = false;
                return;
            }

            StartCoroutine(Play(cutscene, true));
            return;
        }
    }

    private bool CanStart()
    {
        return !RoomTravelController.IsTravelling && StoryState.Instance != null && StoryState.Instance.IsReady && SaveController.Instance != null && SaveController.Instance.IsReady && !SaveController.Instance.IsLoading && NPC.ActiveNPC == null && !PauseController.IsGamePaused && Time.timeScale > 0f && playerFreeze != null && !playerFreeze.IsFrozen;
    }

    [ContextMenu("Play Test Cutscene")]
    public void PlayTestCutscene()
    {
        if (!Application.isPlaying || IsPlaying || !CanStart())
        {
            Debug.LogWarning("Start Play mode and close dialogue or menus before testing a cutscene.", this);
            return;
        }

        if (ValidateSetup(testCutscene))
            StartCoroutine(Play(testCutscene, false));
    }

    private bool ValidateSetup(StoryCutscene cutscene)
    {
        if (cutscene == null || cutsceneCanvas == null || background == null || fadeOverlay == null || playerFreeze == null || transform.IsChildOf(cutsceneCanvas.transform))
            return SetupError("Assign the cutscene, canvas, background, fade overlay and Player Freeze. Put this controller outside CutsceneCanvas.");

        if (cutscene.presentation == StoryCutscenePresentation.Journal && (cutscene.background == null || cutscene.journalHands == null || journalHands == null || completionBanner == null || completionText == null))
            return SetupError("Journal cutscenes need both artwork sprites, Journal Hands, Completion Banner and Completion Text.");

        if (cutscene.completionBanner != null && bannerImage == null)
            return SetupError("Assign Banner Image to display the cutscene's banner sprite.");

        if (cutscene.presentation == StoryCutscenePresentation.Frames)
        {
            if (cutscene.frames == null || cutscene.frames.Length == 0)
                return SetupError("Frame cutscenes need at least one frame.");

            foreach (Sprite frame in cutscene.frames)
                if (frame == null)
                    return SetupError("Remove empty entries from the cutscene frame list.");
        }

        CutsceneRoomExit exit = FindExit(cutscene);

        if (exit != null && (RoomTravelController.Instance == null || !RoomTravelController.Instance.HasRoom(exit.roomID)))
            return SetupError("Assign a valid destination room for this cutscene before playing it.");

        if (sharedTransition != null && !sharedTransition.IsConfigured)
            return SetupError("Assign the shared transition overlay and black Image.");

        if (musicSource != null && musicSource == effectsSource)
            return SetupError("Use separate Audio Sources for music and effects.");

        if (hideDuringCutscene != null)
            foreach (GameObject target in hideDuringCutscene)
                if (target != null && (transform.IsChildOf(target.transform) || cutsceneCanvas.transform.IsChildOf(target.transform)))
                    return SetupError("Hide During Cutscene must not contain this controller, the cutscene canvas, or their parents.");

        if (disableDuringCutscene != null)
            foreach (Behaviour target in disableDuringCutscene)
                if (target != null && (target == this || target == playerFreeze || target == musicSource || target == effectsSource))
                    return SetupError("Do not suspend the cutscene controller, Player Freeze or cutscene Audio Sources.");

        if (gameplayAudio != null)
            foreach (AudioSource source in gameplayAudio)
                if (source != null && (source == musicSource || source == effectsSource))
                    return SetupError("Gameplay Audio must not contain the cutscene Audio Sources.");

        return true;
    }

    private bool SetupError(string message)
    {
        Debug.LogError(message, this);
        return false;
    }

    private IEnumerator Play(StoryCutscene cutscene, bool saveCompletion)
    {
        ownsPlayback = true;
        IsPlaying = true;
        previousPause = PauseController.IsGamePaused;
        previousTimeScale = Time.timeScale;
        playerFreeze.FreezePlayer();
        PauseController.SetPause(true);
        Time.timeScale = 0f;
        SuspendGameplay();
        cutsceneCanvas.SetActive(true);
        background.gameObject.SetActive(false);

        if (journalHands != null)
            journalHands.gameObject.SetActive(false);

        if (completionBanner != null)
            completionBanner.gameObject.SetActive(false);

        if (continuePrompt != null)
            continuePrompt.SetActive(false);

        fadeOverlay.gameObject.SetActive(true);
        fadeOverlay.raycastTarget = true;
        SetFade(0f);

        if (bubbleOverlay != null)
            bubbleOverlay.gameObject.SetActive(false);

        bool bubbles = sharedTransition != null ? cutscene.transition == StoryCutsceneTransition.Bubbles : CanUseBubbles(cutscene);
        yield return Transition(true, bubbles);
        background.sprite = cutscene.presentation == StoryCutscenePresentation.Frames ? cutscene.frames[0] : cutscene.background;
        background.gameObject.SetActive(true);

        if (cutscene.presentation == StoryCutscenePresentation.Journal)
        {
            journalHands.sprite = cutscene.journalHands;
            journalHands.rectTransform.anchoredPosition = cutscene.journalStart;
            journalHands.gameObject.SetActive(true);
            completionBanner.anchoredPosition = cutscene.bannerStart;
            completionText.text = cutscene.completionText;
            completionText.gameObject.SetActive(cutscene.completionBanner == null);

            if (bannerImage != null)
            {
                bannerImage.sprite = cutscene.completionBanner;
                bannerImage.gameObject.SetActive(cutscene.completionBanner != null);
            }
        }

        if (musicSource != null && cutscene.music != null)
        {
            musicSource.clip = cutscene.music;
            musicSource.loop = cutscene.loopMusic;
            musicSource.volume = cutscene.musicVolume;
            musicSource.Play();
        }

        yield return Transition(false, bubbles);

        if (cutscene.presentation == StoryCutscenePresentation.Journal)
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, cutscene.journalHoldSeconds));
            PlayEffect(cutscene.journalSound);
            yield return Move(journalHands.rectTransform, cutscene.journalStart, cutscene.journalEnd, cutscene.journalMoveSeconds);
            journalHands.gameObject.SetActive(false);
            completionBanner.gameObject.SetActive(true);
            PlayEffect(cutscene.bannerSound);
            yield return Move(completionBanner, cutscene.bannerStart, cutscene.bannerEnd, cutscene.bannerMoveSeconds);
        }
        else
            yield return PlayFrames(background, cutscene.frames, 0, cutscene.frames.Length - 1, cutscene.framesPerSecond);

        yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, cutscene.minimumHoldSeconds));

        if (cutscene.waitForInput)
        {
            if (continuePrompt != null)
                continuePrompt.SetActive(true);

            yield return null;

            while (AnyButton(false))
                yield return null;

            while (!AnyButton(true))
                yield return null;
        }

        if (continuePrompt != null)
            continuePrompt.SetActive(false);

        yield return Transition(true, bubbles, true);
        CutsceneRoomExit exit = FindExit(cutscene);

        if (exit != null && !RoomTravelController.Instance.Place(exit.roomID, exit.arrivalOverride))
        {
            Debug.LogError("Cutscene room handoff failed. Assign Player on RoomTravelController.", this);
            RestoreGameplay();
            yield break;
        }

        RestoreHiddenObjects();
        yield return null;
        yield return null;
        background.gameObject.SetActive(false);

        if (completionBanner != null)
            completionBanner.gameObject.SetActive(false);

        yield return Transition(false, bubbles);
        RestoreGameplay();

        if (saveCompletion && StoryState.Instance != null)
        {
            StoryState.Instance.SetFlag(cutscene.completedFlag);

            if (SaveController.Instance != null)
                SaveController.Instance.SaveGame();
        }
    }

    private bool CanUseBubbles(StoryCutscene cutscene)
    {
        if (cutscene.transition != StoryCutsceneTransition.Bubbles)
            return false;

        bool valid = bubbleOverlay != null && bubbleFrames != null && bubbleFrames.Length >= 3 && bubbleCoveredFrame > 0 && bubbleCoveredFrame < bubbleFrames.Length - 1;

        if (valid)
            foreach (Sprite frame in bubbleFrames)
                valid &= frame != null;

        if (!valid)
            Debug.LogWarning("Bubble frames are not ready. This cutscene will use a fade.", this);

        return valid;
    }

    private CutsceneRoomExit FindExit(StoryCutscene cutscene)
    {
        if (roomExits != null)
            foreach (CutsceneRoomExit exit in roomExits)
                if (exit != null && exit.cutscene == cutscene)
                    return exit;

        return null;
    }

    private IEnumerator Transition(bool cover, bool bubbles, bool exiting = false)
    {
        if (sharedTransition != null)
        {
            if (cover)
                yield return sharedTransition.Cover(bubbles);
            else
                yield return sharedTransition.Reveal(bubbles);

            yield break;
        }
        if (bubbles)
        {
            bubbleOverlay.gameObject.SetActive(true);
            int first = cover ? 0 : bubbleCoveredFrame;
            int last = cover ? bubbleCoveredFrame : bubbleFrames.Length - 1;
            yield return PlayFrames(bubbleOverlay, bubbleFrames, first, last, bubbleFramesPerSecond);

            if (!cover)
                bubbleOverlay.gameObject.SetActive(false);

            yield break;
        }

        float duration = Mathf.Max(0.01f, cover ? (exiting ? exitCoverSeconds : fadeSeconds) : revealSeconds);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float progress = Mathf.Clamp01(elapsed / duration);
            SetFade(cover ? Mathf.SmoothStep(0f, 1f, progress) : Mathf.SmoothStep(1f, 0f, progress));
            yield return null;
            elapsed += Time.unscaledDeltaTime;
        }

        SetFade(cover ? 1f : 0f);

        if (cover)
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, coveredHoldSeconds));
    }

    private IEnumerator PlayFrames(Image target, Sprite[] frames, int first, int last, float fps)
    {
        float elapsed = 0f;
        float rate = Mathf.Max(1f, fps);
        float duration = (last - first + 1) / rate;

        while (elapsed < duration)
        {
            target.sprite = frames[Mathf.Min(last, first + Mathf.FloorToInt(elapsed * rate))];
            yield return null;
            elapsed += Time.unscaledDeltaTime;
        }

        target.sprite = frames[last];
    }

    private IEnumerator Move(RectTransform target, Vector2 from, Vector2 to, float seconds)
    {
        float duration = Mathf.Max(0.01f, seconds);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            target.anchoredPosition = Vector2.LerpUnclamped(from, to, Mathf.SmoothStep(0f, 1f, elapsed / duration));
            yield return null;
            elapsed += Time.unscaledDeltaTime;
        }

        target.anchoredPosition = to;
    }

    private void SetFade(float alpha)
    {
        fadeOverlay.color = new Color(0f, 0f, 0f, alpha);
    }

    private void PlayEffect(AudioClip clip)
    {
        if (effectsSource != null && clip != null)
            effectsSource.PlayOneShot(clip);
    }

    private bool AnyButton(bool pressedThisFrame)
    {
        foreach (InputDevice device in InputSystem.devices)
        {
            if (!(device is Keyboard) && !(device is Mouse) && !(device is Gamepad))
                continue;

            foreach (InputControl control in device.allControls)
                if (control is ButtonControl button && (pressedThisFrame ? button.wasPressedThisFrame : button.isPressed))
                    return true;
        }

        return false;
    }

    private void SuspendGameplay()
    {
        hiddenStates = new bool[hideDuringCutscene == null ? 0 : hideDuringCutscene.Length];
        behaviourStates = new bool[disableDuringCutscene == null ? 0 : disableDuringCutscene.Length];
        audioStates = new bool[gameplayAudio == null ? 0 : gameplayAudio.Length];

        for (int i = 0; i < hiddenStates.Length; i++)
        {
            if (hideDuringCutscene[i] == null)
                continue;

            hiddenStates[i] = hideDuringCutscene[i].activeSelf;
            hideDuringCutscene[i].SetActive(false);
        }

        for (int i = 0; i < behaviourStates.Length; i++)
        {
            if (disableDuringCutscene[i] == null)
                continue;

            behaviourStates[i] = disableDuringCutscene[i].enabled;
            disableDuringCutscene[i].enabled = false;
        }

        for (int i = 0; i < audioStates.Length; i++)
        {
            if (gameplayAudio[i] == null)
                continue;

            audioStates[i] = gameplayAudio[i].isPlaying;

            if (audioStates[i])
                gameplayAudio[i].Pause();
        }

        if (musicSource != null)
        {
            oldMusicIgnorePause = musicSource.ignoreListenerPause;
            musicSource.ignoreListenerPause = true;
        }

        if (effectsSource != null)
        {
            oldEffectsIgnorePause = effectsSource.ignoreListenerPause;
            effectsSource.ignoreListenerPause = true;
        }
    }

    private void RestoreHiddenObjects()
    {
        for (int i = 0; hiddenStates != null && i < hiddenStates.Length; i++)
            if (hideDuringCutscene[i] != null)
                hideDuringCutscene[i].SetActive(hiddenStates[i]);
    }

    private void RestoreGameplay()
    {
        if (!ownsPlayback)
            return;

        if (musicSource != null)
        {
            musicSource.Stop();
            musicSource.ignoreListenerPause = oldMusicIgnorePause;
        }

        if (effectsSource != null)
        {
            effectsSource.Stop();
            effectsSource.ignoreListenerPause = oldEffectsIgnorePause;
        }

        if (sharedTransition != null)
            sharedTransition.Clear();

        if (cutsceneCanvas != null)
            cutsceneCanvas.SetActive(false);

        Time.timeScale = previousTimeScale;
        PauseController.SetPause(previousPause);

        if (playerFreeze != null)
            playerFreeze.UnfreezePlayer();

        for (int i = 0; hiddenStates != null && i < hiddenStates.Length; i++)
            if (hideDuringCutscene[i] != null)
                hideDuringCutscene[i].SetActive(hiddenStates[i]);

        for (int i = 0; behaviourStates != null && i < behaviourStates.Length; i++)
            if (disableDuringCutscene[i] != null)
                disableDuringCutscene[i].enabled = behaviourStates[i];

        for (int i = 0; audioStates != null && i < audioStates.Length; i++)
            if (gameplayAudio[i] != null && audioStates[i])
                gameplayAudio[i].UnPause();

        ownsPlayback = false;
        IsPlaying = false;
        nextCheck = Time.unscaledTime + 0.1f;
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        RestoreGameplay();
    }
}
