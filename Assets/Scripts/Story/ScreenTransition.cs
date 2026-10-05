using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ScreenTransition : MonoBehaviour
{
    [SerializeField] private CanvasGroup overlay;
    [SerializeField] private Image black;
    [SerializeField] private Image bubbles;
    [SerializeField] private Sprite[] bubbleFrames;
    [SerializeField] private int coveredFrame;
    [SerializeField] private float bubbleFPS = 18f;
    [SerializeField] private float coverSeconds = 0.7f;
    [SerializeField] private float coveredHoldSeconds = 0.25f;
    [SerializeField] private float revealSeconds = 0.9f;
    [SerializeField] private float resumeBeforeEndSeconds = 0.15f;
    [SerializeField] private Color transitionColor = Color.black;

    public bool IsConfigured => overlay != null && black != null;

    private void Awake()
    {
        Clear();
    }

    public IEnumerator Cover(bool useBubbles)
    {
        yield return Animate(true, useBubbles);
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, coveredHoldSeconds));
    }

    private void SetTransitionAlpha(float alpha)
    {
        Color color = transitionColor;
        color.a = alpha;
        black.color = color;
    }

    public IEnumerator Reveal(bool useBubbles, System.Action onReady = null)
    {
        yield return Animate(false, useBubbles, onReady);
        Clear();
    }

    private IEnumerator Animate(bool cover, bool useBubbles, System.Action onReady = null)
    {
        overlay.alpha = 1f;
        overlay.blocksRaycasts = true;
        bool valid = useBubbles && bubbles != null && bubbleFrames != null && coveredFrame > 0 && coveredFrame < bubbleFrames.Length - 1;

        if (valid)
            foreach (Sprite frame in bubbleFrames)
                valid &= frame != null;

        if (bubbles != null)
            bubbles.gameObject.SetActive(valid);

        int first = cover ? 0 : coveredFrame;
        int last = cover ? coveredFrame : (bubbleFrames == null ? 0 : bubbleFrames.Length - 1);
        float duration = Mathf.Max(0.01f, cover ? coverSeconds : revealSeconds);

        if (valid)
            duration = Mathf.Max(duration, (last - first + 1) / Mathf.Max(1f, bubbleFPS));

        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            SetTransitionAlpha(cover ? Mathf.SmoothStep(0f, 1f, t) : Mathf.SmoothStep(1f, 0f, t));

            if (valid)
                bubbles.sprite = bubbleFrames[Mathf.Min(last, first + Mathf.FloorToInt(t * (last - first + 1)))];

            if (!cover && elapsed >= Mathf.Max(0f, duration - resumeBeforeEndSeconds))
            {
                onReady?.Invoke();
                onReady = null;
            }

            yield return null;
            elapsed += Time.unscaledDeltaTime;
        }

        SetTransitionAlpha(cover ? 1f : 0f);

        if (valid)
            bubbles.sprite = bubbleFrames[last];

        if (!cover)
            onReady?.Invoke();
    }

    public void Clear()
    {
        if (overlay != null)
        {
            overlay.alpha = 0f;
            overlay.blocksRaycasts = false;
        }

        if (bubbles != null)
            bubbles.gameObject.SetActive(false);
    }
}
