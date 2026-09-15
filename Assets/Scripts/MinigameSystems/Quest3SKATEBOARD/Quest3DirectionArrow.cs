using System.Collections;
using UnityEngine;

public class Quest3DirectionArrow : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveDistance = 0.3f;

    [Header("Direction (1 = right, -1 = left)")]
    [SerializeField] private float direction = 1f;

    [SerializeField] private float forwardDuration = 0.4f;
    [SerializeField] private float returnDuration = 0.55f;
    [SerializeField] private float pauseBetweenPulses = 0.15f;

    [Header("Pop")]
    [SerializeField] private float popScale = 1.15f;
    [SerializeField] private float popDuration = 0.12f;

    private Vector3 startPosition;
    private Vector3 startScale;

    private Coroutine animationRoutine;

    void OnEnable()
    {
        startPosition = transform.localPosition;
        startScale = transform.localScale;
        animationRoutine = StartCoroutine(AnimateArrow());
    }

    void OnDisable()
    {
        if (animationRoutine != null)
            StopCoroutine(animationRoutine);

        transform.localPosition = startPosition;
        transform.localScale = startScale;
    }

    private IEnumerator AnimateArrow()
    {
        while (true)
        {
            Vector3 targetPosition = startPosition + Vector3.right * moveDistance * direction;
            yield return MoveArrow(startPosition, targetPosition, forwardDuration);
            yield return Pop();
            yield return MoveArrow(targetPosition, startPosition, returnDuration);
            yield return new WaitForSeconds(pauseBetweenPulses);
        }
    }

    private IEnumerator MoveArrow(Vector3 from, Vector3 to, float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = Mathf.SmoothStep(0f, 1f, t);
            transform.localPosition = Vector3.Lerp(from, to, t);
            yield return null;
        }

        transform.localPosition = to;
    }

    private IEnumerator Pop()
    {
        Vector3 enlargedScale = startScale * popScale;
        float halfDuration = popDuration / 2f;
        float elapsed = 0f;

        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / halfDuration);
            t = Mathf.SmoothStep(0f, 1f, t);
            transform.localScale = Vector3.Lerp(startScale, enlargedScale, t);
            yield return null;
        }

        elapsed = 0f;

        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / halfDuration);
            t = Mathf.SmoothStep(0f, 1f, t);
            transform.localScale = Vector3.Lerp(enlargedScale, startScale, t);
            yield return null;
        }

        transform.localScale = startScale;
    }
}