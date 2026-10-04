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

    public IEnumerator Reveal(bool useBubbles)
    {
        yield return Animate(false, useBubbles);
        Clear();
    }

    private IEnumerator Animate(bool cover, bool useBubbles)
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
            black.color = new Color(0f, 0f, 0f, cover ? Mathf.SmoothStep(0f, 1f, t) : Mathf.SmoothStep(1f, 0f, t));

            if (valid)
                bubbles.sprite = bubbleFrames[Mathf.Min(last, first + Mathf.FloorToInt(t * (last - first + 1)))];

            yield return null;
            elapsed += Time.unscaledDeltaTime;
        }

        black.color = new Color(0f, 0f, 0f, cover ? 1f : 0f);

        if (valid)
            bubbles.sprite = bubbleFrames[last];
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
