using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StoryJournalController : MonoBehaviour
{
    [Header("Pages")]
    [SerializeField] private StoryJournalPage[] pages;
    [SerializeField] private Image pageImage;
    [SerializeField] private Sprite emptyJournalSprite;
    [SerializeField] private GameObject emptyMessage;

    [Header("Navigation")]
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private TMP_Text pageNumber;

    [Header("Unity Animation")]
    [SerializeField] private float turnSeconds = 0.45f;

    [Header("Optional Frame Animation")]
    [SerializeField] private Image turnOverlay;
    [SerializeField] private Sprite[] turnFrames;
    [SerializeField] private int artworkSwapFrame;
    [SerializeField] private float framesPerSecond = 18f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip pageTurnSound;

    [Header("Automatic Selection")]
    [SerializeField] private bool focusCurrentQuest = true;

    private StoryJournalPage lastCurrentQuestPage;
    private bool focusOnOpen;

    private readonly List<StoryJournalPage> available = new();
    private StoryJournalPage selected;
    private Vector3 originalScale;
    private bool turning;
    private bool refreshPending;
    private bool configured;
    private bool previousIgnorePause;

    private void Awake()
    {
        configured = pageImage != null && previousButton != null && nextButton != null && previousButton != nextButton && turnOverlay != pageImage;

        if (!configured)
        {
            Debug.LogError("Journal needs a Page Image and two different navigation Buttons. Turn Overlay must be a separate Image.", this);
            enabled = false;
            return;
        }

        originalScale = pageImage.rectTransform.localScale;
    }

    private void SelectCurrentQuest()
    {
        if (!focusCurrentQuest || StoryState.Instance == null || !StoryState.Instance.IsReady || QuestController.Instance == null)
            return;

        StoryJournalPage current = null;

        for (int i = available.Count - 1; i >= 0; i--)
        {
            Quest quest = available[i].quest;

            if (quest != null && QuestController.Instance.IsQuestActive(quest.questID))
            {
                current = available[i];
                break;
            }
        }

        if (current != null && (focusOnOpen || current != lastCurrentQuestPage))
            selected = current;

        lastCurrentQuestPage = current;
        focusOnOpen = false;
    }

    private void OnEnable()
    {
        if (!configured)
            return;

        StoryState.Changed += Refresh;
        previousButton.onClick.AddListener(PreviousPage);
        nextButton.onClick.AddListener(NextPage);

        if (audioSource != null)
        {
            previousIgnorePause = audioSource.ignoreListenerPause;
            audioSource.ignoreListenerPause = true;
        }

        ResetVisuals();
        focusOnOpen = true;
        Refresh();
    }

    private void OnDisable()
    {
        StoryState.Changed -= Refresh;

        if (!configured)
            return;

        previousButton.onClick.RemoveListener(PreviousPage);
        nextButton.onClick.RemoveListener(NextPage);
        StopAllCoroutines();
        turning = false;
        refreshPending = false;
        ResetVisuals();

        if (audioSource != null)
        {
            audioSource.Stop();
            audioSource.ignoreListenerPause = previousIgnorePause;
        }
    }

    public void Refresh()
    {
        if (!configured || !isActiveAndEnabled)
            return;

        if (turning)
        {
            refreshPending = true;
            return;
        }

        int previousIndex = Mathf.Max(0, available.IndexOf(selected));
        available.Clear();

        if (StoryState.Instance != null && StoryState.Instance.IsReady && pages != null)
            foreach (StoryJournalPage page in pages)
                if (page != null && !available.Contains(page) && (page.unlockCondition == null || page.unlockCondition.IsMet()))
                    available.Add(page);

        if (!available.Contains(selected))
            selected = available.Count == 0 ? null : available[Mathf.Min(previousIndex, available.Count - 1)];

        SelectCurrentQuest();
        
        ShowSelected();
        UpdateButtons();
    }

    public void NextPage()
    {
        Turn(1);
    }

    public void PreviousPage()
    {
        Turn(-1);
    }

    private void Turn(int direction)
    {
        if (!configured || !isActiveAndEnabled || turning)
            return;

        Refresh();
        int index = available.IndexOf(selected) + direction;

        if (selected == null || index < 0 || index >= available.Count)
            return;

        StartCoroutine(AnimateTurn(available[index], direction));
    }

    private IEnumerator AnimateTurn(StoryJournalPage target, int direction)
    {
        turning = true;
        UpdateButtons();

        if (audioSource != null && pageTurnSound != null)
            audioSource.PlayOneShot(pageTurnSound);

        if (HasFrameAnimation())
            yield return AnimateFrames(target, direction);
        else
        {
            yield return ScalePage(1f, 0f, Mathf.Max(0.01f, turnSeconds) * 0.5f);
            selected = target;
            ShowSelected();
            yield return ScalePage(0f, 1f, Mathf.Max(0.01f, turnSeconds) * 0.5f);
        }

        ResetVisuals();
        turning = false;

        if (refreshPending)
        {
            refreshPending = false;
            Refresh();
        }
        else
            UpdateButtons();
    }

    private bool HasFrameAnimation()
    {
        if (turnOverlay == null || turnFrames == null || turnFrames.Length < 3 || artworkSwapFrame <= 0 || artworkSwapFrame >= turnFrames.Length - 1)
            return false;

        foreach (Sprite frame in turnFrames)
            if (frame == null)
                return false;

        return true;
    }

    private IEnumerator AnimateFrames(StoryJournalPage target, int direction)
    {
        float rate = Mathf.Max(1f, framesPerSecond);
        float duration = turnFrames.Length / rate;
        float elapsed = 0f;
        bool swapped = false;
        turnOverlay.gameObject.SetActive(true);

        while (elapsed < duration)
        {
            int step = Mathf.Min(turnFrames.Length - 1, Mathf.FloorToInt(elapsed * rate));
            int frame = direction > 0 ? step : turnFrames.Length - 1 - step;
            turnOverlay.sprite = turnFrames[frame];

            if (!swapped && (direction > 0 ? frame >= artworkSwapFrame : frame <= artworkSwapFrame))
            {
                selected = target;
                ShowSelected();
                swapped = true;
            }

            yield return null;
            elapsed += Time.unscaledDeltaTime;
        }

        selected = target;
        ShowSelected();
    }

    private IEnumerator ScalePage(float from, float to, float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float width = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, elapsed / duration));
            pageImage.rectTransform.localScale = new Vector3(originalScale.x * width, originalScale.y, originalScale.z);
            yield return null;
            elapsed += Time.unscaledDeltaTime;
        }

        pageImage.rectTransform.localScale = new Vector3(originalScale.x * to, originalScale.y, originalScale.z);
    }

    private void ShowSelected()
    {
        pageImage.sprite = selected != null ? selected.GetArtwork() : emptyJournalSprite;
        pageImage.enabled = pageImage.sprite != null;

        if (emptyMessage != null)
            emptyMessage.SetActive(selected == null);

        if (pageNumber != null)
            pageNumber.text = selected == null ? "" : $"{available.IndexOf(selected) + 1} / {available.Count}";
    }

    private void UpdateButtons()
    {
        int index = available.IndexOf(selected);
        previousButton.interactable = !turning && index > 0;
        nextButton.interactable = !turning && index >= 0 && index < available.Count - 1;
    }

    private void ResetVisuals()
    {
        pageImage.rectTransform.localScale = originalScale;

        if (turnOverlay != null)
            turnOverlay.gameObject.SetActive(false);
    }
}
